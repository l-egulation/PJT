import time
import os
import operator
import sqlite3
import re
from typing import Annotated, List, TypedDict
from dotenv import load_dotenv
from langgraph.checkpoint.sqlite import SqliteSaver

from langchain_community.tools.tavily_search import TavilySearchResults
from langchain_google_genai import ChatGoogleGenerativeAI
from langchain_core.messages import AnyMessage, HumanMessage, SystemMessage
from langgraph.graph import StateGraph, START, END

import OpenDartReader
from bs4 import BeautifulSoup

load_dotenv()

# ===== 1. 설정 및 모델 =====
search_tool = TavilySearchResults(k=5)
model = ChatGoogleGenerativeAI(model="gemini-2.5-flash", temperature=0.2)

# DART API 클라이언트 생성 함수
def get_dart_client():
    dart_key = os.environ.get("DART_API_KEY")
    if dart_key:
        try:
            return OpenDartReader(dart_key)
        except Exception:
            return None
    return None

# ===== 2. State 정의 =====
class State(TypedDict):
    messages: Annotated[list[AnyMessage], operator.add]
    k_industry: str      # 국내 산업 분석
    g_industry: str      # 글로벌 산업 분석
    k_corporate: str     # 국내 기업 행보
    g_corporate: str     # 해외 기업 행보
    competition: str     # 경쟁사 비교
    dart_analysis: str    # 📄 DART 전자공시 '사업의 내용' 분석 (신규 추가)
    sources: Annotated[list, operator.add]  # 출처 누적

def get_query(message):
    if hasattr(message, "content"): return message.content
    return message.get("content", "") if isinstance(message, dict) else str(message)

# ===== 3. 노드(전문가) 정의 =====

# [NEW] DART 전자공시 분석관
def dart_analyst(state: State):
    time.sleep(9.0)
    topic = get_query(state["messages"][0])
    dart = get_dart_client()
    
    if not dart:
        return {"dart_analysis": "⚠️ DART API Key가 설정되지 않아 전자공시 분석을 스킵했습니다."}
    
    try:
        # 1. 최근 정기보고서(사업/반기/분기보고서) 검색
        reports = dart.list(topic, start='2024-01-01', kind='A') # A: 정기공시
        
        if reports is None or reports.empty:
            return {"dart_analysis": f"'{topic}' 기업의 DART 정기보고서를 찾을 수 없습니다."}
        
        # 가장 최근 보고서 선택
        latest_report = reports.iloc[0]
        rcept_no = latest_report['rcept_no']
        report_nm = latest_report['report_nm']
        
        # 2. 목차에서 '사업의 내용' 찾기
        sub_docs = dart.sub_docs(rcept_no)
        target_doc = None
        for _, row in sub_docs.iterrows():
            if '사업의 내용' in row['title']:
                target_doc = row
                break
                
        if target_doc is None:
            # 못 찾은 경우 전체 보고서 원문 사용
            xml_text = dart.xml(rcept_no)
        else:
            # '사업의 내용' 부분 파싱
            xml_text = dart.attach_doc(rcept_no, target_doc['title'])
            
        # HTML/XML 태그 제거하여 순수 텍스트 추출
        soup = BeautifulSoup(xml_text, 'html.parser')
        raw_text = soup.get_text(separator=' ', strip=True)
        
        # Gemini 입력 길이 한계 고려 (최대 15,000자 선에서 전처리)
        cleaned_text = raw_text[:15000]
        
        # 3. Gemini로 '사업의 내용' 정밀 요약
        prompt = f"""
다음은 DART 전자공시 시스템에 제출된 [{topic}]의 최신 정기보고서({report_nm}) 중 '2. 사업의 내용' 원문 텍스트야.

다음 항목을 중심으로 객관적 사실과 수치를 바탕으로 요약/분석해줘:
1. 주력 사업부문 및 주요 제품/서비스 매출 비중
2. 주요 원재료 및 가동률, 생산 능력 현황
3. 연구개발(R&D) 투자 및 핵심 보유 기술
4. 회사가 공시한 시장 환경 및 경쟁 우위 요소

[공시 원문 일부]
{cleaned_text}
"""
        res = model.invoke([
            SystemMessage(content="너는 대한민국 금융감독원 공시서류 분석 전문 회계사/증권사 애널리스트다."),
            HumanMessage(content=prompt)
        ])
        
        result_text = f"**[출처: DART {report_nm}]**\n\n" + res.content
        dart_source = [{"title": f"DART 전자공시: {topic} {report_nm}", "url": f"https://dart.fss.or.kr/dsaf001/main.do?rcpNo={rcept_no}"}]
        
        return {"dart_analysis": result_text, "sources": dart_source}
        
    except Exception as e:
        return {"dart_analysis": f"DART 공시 수집 중 오류 발생: {str(e)}"}

# [A] 국내 산업 분석
def k_industry_analyst(state: State):
    time.sleep(1.5) # API 호출 딜레이 추가
    topic = get_query(state["messages"][0])
    q = f"{topic} 국내 시장 점유율 정부 정책 산업 통찰력 2026"
    results = search_tool.invoke(q)
    context = "\n".join([r.get('content', '') for r in results if isinstance(r, dict)])
    sources = [{"title": r.get("title", "참고 기사"), "url": r.get("url", "")} for r in results if isinstance(r, dict) and r.get("url")]
    
    res = model.invoke([
        SystemMessage(content="너는 한국 산업 정책 및 시장 전문가다."), 
        HumanMessage(content=f"국내 관점에서 [{topic}] 산업의 정책과 시장 상황을 구체적 수치와 함께 분석해:\n{context}")
    ])
    return {"k_industry": res.content, "sources": sources}

# [B] 글로벌 산업 분석
def g_industry_analyst(state: State):
    time.sleep(3.0) # 겹치지 않게 약간 차등을 둠
    topic = get_query(state["messages"][0])
    q = f"global {topic} industry standards US EU China policy 2026 report"
    results = search_tool.invoke(q)
    context = "\n".join([r.get('content', '') for r in results if isinstance(r, dict)])
    sources = [{"title": r.get("title", "참고 기사"), "url": r.get("url", "")} for r in results if isinstance(r, dict) and r.get("url")]
    
    res = model.invoke([
        SystemMessage(content="너는 글로벌 시장 조사 분석가다."), 
        HumanMessage(content=f"전 세계적 관점에서 [{topic}] 산업의 기술 표준과 글로벌 트렌드를 분석해:\n{context}")
    ])
    return {"g_industry": res.content, "sources": sources}

# [C] 국내 기업 분석
def k_corporate_analyst(state: State):
    time.sleep(4.5)
    topic = get_query(state["messages"][0])
    q = f"{topic} 국내 공장 가동 실적 신규 투자 공시 2026"
    results = search_tool.invoke(q)
    context = "\n".join([r.get('content', '') for r in results if isinstance(r, dict)])
    sources = [{"title": r.get("title", "참고 기사"), "url": r.get("url", "")} for r in results if isinstance(r, dict) and r.get("url")]
    
    res = model.invoke([
        SystemMessage(content="너는 기업 국내 사업 분석가다."), 
        HumanMessage(content=f"[{topic}] 관련 기업의 한국 내 사업 성과와 투자 현황을 상세히 분석해:\n{context}")
    ])
    return {"k_corporate": res.content, "sources": sources}

# [D] 해외 기업 분석
def g_corporate_analyst(state: State):
    time.sleep(6.0)
    topic = get_query(state["messages"][0])
    q = f"{topic} overseas factory global partnership international sales 2026"
    results = search_tool.invoke(q)
    context = "\n".join([r.get('content', '') for r in results if isinstance(r, dict)])
    sources = [{"title": r.get("title", "참고 기사"), "url": r.get("url", "")} for r in results if isinstance(r, dict) and r.get("url")]
    
    res = model.invoke([
        SystemMessage(content="너는 기업 글로벌 전략 분석가다."), 
        HumanMessage(content=f"[{topic}] 관련 기업의 해외 현지 반응 및 글로벌 파트너십 성과를 분석해:\n{context}")
    ])
    return {"g_corporate": res.content, "sources": sources}

# [E] 경쟁 분석
def competition_analyst(state: State):
    time.sleep(7.5)
    topic = get_query(state["messages"][0])
    q = f"{topic} main global competitors technology comparison 2026"
    results = search_tool.invoke(q)
    context = "\n".join([r.get('content', '') for r in results if isinstance(r, dict)])
    sources = [{"title": r.get("title", "참고 기사"), "url": r.get("url", "")} for r in results if isinstance(r, dict) and r.get("url")]
    
    res = model.invoke([
        SystemMessage(content="너는 경쟁 정보 분석가다."), 
        HumanMessage(content=f"[{topic}]의 경쟁사 대비 기술적 사양(Spec) 우위와 약점을 분석해:\n{context}")
    ])
    return {"competition": res.content, "sources": sources}

# [F] 수석 전략가 (DART 공시 데이터 포함하여 종합 보고서 생성)
def chief_strategist(state: State):
    topic = get_query(state["messages"][0])
    
    prompt = f"""
너는 글로벌 톱티어 리서치 센터의 수석 전략가야. 
분야별 분석 데이터와 DART 공식 공시 분석 결과를 바턍으로 [{topic}]에 대한 '글로벌 종합 산업·기업 분석 보고서'를 작성해.

[입력 데이터]
- DART 공식 공시 분석 (사업의 내용): {state.get('dart_analysis', '공시 정보 없음')}
- 국내 산업 분석: {state['k_industry']}
- 글로벌 산업 분석: {state['g_industry']}
- 국내 기업 행보: {state['k_corporate']}
- 해외 기업 행보: {state['g_corporate']}
- 글로벌 경쟁 분석: {state['competition']}

[작성 가이드라인]
1. 가독성을 위해 불릿 포인트, 굵은 글씨(**강조**), 표(Table)를 적절히 배치해라.
2. 특히 DART 공시 데이터에서 나온 수치(매출 비중, 가동률, R&D 투자액 등)를 보고서 본문에 적극 반영해라.

작성 양식:
# 🏢 [{topic}] 글로벌 종합 분석 보고서

## 1. 📄 DART 공시 기반 핵심 사업 분석
(공시보고서의 '사업의 내용'에서 밝힌 매출 구조, 가동률, R&D 현황 정리)

## 2. 🌐 글로벌 산업 지형 & 패러다임 변화
(전 세계 트렌드와 국내 정책/시장 환경 비교)

## 3. 🚀 주요 사업 성과 및 글로벌 현황
(국내외 주력 프로젝트 및 실적)

## 4. ⚔️ 경쟁 구도 및 기술 차별점
(라이벌 기업과의 기술/사업 비교 표 포함)

## 5. 🚨 주요 리스크 및 최종 대응 전략
(위기요인 및 종합 전략 제언)
"""
    response = model.invoke([
        SystemMessage(content="너는 입체적 시각을 가진 수석 리서치 디렉터다."), 
        HumanMessage(content=prompt)
    ])
    
    return {"messages": [HumanMessage(content=response.content)]}

# ===== 4. 그래프 구성 =====
workflow = StateGraph(State)

workflow.add_node("dart_ind", dart_analyst)
workflow.add_node("k_ind", k_industry_analyst)
workflow.add_node("g_ind", g_industry_analyst)
workflow.add_node("k_corp", k_corporate_analyst)
workflow.add_node("g_corp", g_corporate_analyst)
workflow.add_node("comp", competition_analyst)
workflow.add_node("chief", chief_strategist)

# 시작 시 6개 분석관이 동시에 실행
workflow.add_edge(START, "dart_ind")
workflow.add_edge(START, "k_ind")
workflow.add_edge(START, "g_ind")
workflow.add_edge(START, "k_corp")
workflow.add_edge(START, "g_corp")
workflow.add_edge(START, "comp")

# 수석 전략가에게 보고
workflow.add_edge("dart_ind", "chief")
workflow.add_edge("k_ind", "chief")
workflow.add_edge("g_ind", "chief")
workflow.add_edge("k_corp", "chief")
workflow.add_edge("g_corp", "chief")
workflow.add_edge("comp", "chief")

workflow.add_edge("chief", END)

conn = sqlite3.connect("research_history.db", check_same_thread=False)
memory = SqliteSaver(conn)
agent = workflow.compile(checkpointer=memory)