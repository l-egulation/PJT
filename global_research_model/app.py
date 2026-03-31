import os
import operator
import sqlite3
from typing import Annotated, List, TypedDict
from dotenv import load_dotenv
from langgraph.checkpoint.sqlite import SqliteSaver

from langchain_community.tools.tavily_search import TavilySearchResults
from langchain_upstage import ChatUpstage
from langchain_core.messages import AnyMessage, HumanMessage, SystemMessage
from langgraph.graph import StateGraph, START, END

load_dotenv()

# ===== 1. 설정 및 모델 (디테일을 위해 검색량 k=10으로 상향) =====
search_tool = TavilySearchResults(k=10)
model = ChatUpstage(model="solar-pro", temperature=0)

# ===== 2. State 정의 (신입 사원들 자리 만들기) =====
class State(TypedDict):
    messages: Annotated[list[AnyMessage], operator.add]
    k_industry: str      # 국내 산업 분석
    g_industry: str      # 글로벌 산업 분석
    k_corporate: str     # 국내 기업 행보
    g_corporate: str     # 해외 기업 행보
    competition: str     # 경쟁사 비교
    sources: list

# ===== 3. 노드(전문가) 정의 =====

def get_query(message):
    if hasattr(message, "content"): return message.content
    return message.get("content", "") if isinstance(message, dict) else str(message)

# [A] 국내 산업 분석 (K-Industry)
def k_industry_analyst(state: State):
    topic = get_query(state["messages"][0])
    q = f"{topic} 국내 시장 점유율 정부 정책 산업 통찰력 2026"
    results = search_tool.invoke(q)
    context = "\n".join([r.get('content', '') for r in results if isinstance(r, dict)])
    res = model.invoke([SystemMessage(content="너는 한국 산업 정책 및 시장 전문가다."), 
                        HumanMessage(content=f"국내 관점에서 [{topic}] 산업의 정책과 시장 상황을 아주 구체적(수치 포함)으로 분석해:\n{context}")])
    return {"k_industry": res.content}

# [B] 글로벌 산업 분석 (Global Industry)
def g_industry_analyst(state: State):
    topic = get_query(state["messages"][0])
    q = f"global {topic} industry standards US EU China policy 2026 report"
    results = search_tool.invoke(q)
    context = "\n".join([r.get('content', '') for r in results if isinstance(r, dict)])
    res = model.invoke([SystemMessage(content="너는 글로벌 시장 조사 분석가다."), 
                        HumanMessage(content=f"전 세계적 관점에서 [{topic}] 산업의 기술 표준과 글로벌 트렌드를 분석해:\n{context}")])
    return {"g_industry": res.content}

# [C] 국내 기업 분석 (K-Corporate)
def k_corporate_analyst(state: State):
    topic = get_query(state["messages"][0])
    q = f"{topic} 국내 공장 가동 실적 신규 투자 공시 2026"
    results = search_tool.invoke(q)
    context = "\n".join([r.get('content', '') for r in results if isinstance(r, dict)])
    res = model.invoke([SystemMessage(content="너는 기업 국내 사업 분석가다."), 
                        HumanMessage(content=f"[{topic}] 관련 기업의 한국 내 사업 성과와 투자 현황을 상세히 분석해:\n{context}")])
    return {"k_corporate": res.content}

# [D] 해외 기업 분석 (Global Corporate)
def g_corporate_analyst(state: State):
    topic = get_query(state["messages"][0])
    q = f"{topic} overseas factory global partnership international sales 2026"
    results = search_tool.invoke(q)
    context = "\n".join([r.get('content', '') for r in results if isinstance(r, dict)])
    res = model.invoke([SystemMessage(content="너는 기업 글로벌 전략 분석가다."), 
                        HumanMessage(content=f"[{topic}] 관련 기업의 해외 현지 반응 및 글로벌 파트너십 성과를 분석해:\n{context}")])
    return {"g_corporate": res.content}

# [E] 경쟁 분석 (Competition)
def competition_analyst(state: State):
    topic = get_query(state["messages"][0])
    q = f"{topic} main global competitors technology comparison 2026"
    results = search_tool.invoke(q)
    context = "\n".join([r.get('content', '') for r in results if isinstance(r, dict)])
    res = model.invoke([SystemMessage(content="너는 경쟁 정보 분석가다."), 
                        HumanMessage(content=f"[{topic}]의 경쟁사 대비 기술적 사양(Spec) 우위와 약점을 분석해:\n{context}")])
    return {"competition": res.content}

## [F] 수석 전략가 (Chief - 산업/기업 종합 인사이트 도출)
def chief_strategist(state: State):
    topic = get_query(state["messages"][0])
    
    # 5명의 전문가 데이터를 유기적으로 결합하여 종합 보고서 작성
    prompt = f"""
너는 글로벌 톱티어 리서치 센터의 수석 전략가야. 
분야별 전문가들이 제출한 아래 데이터를 바탕으로 [{topic}]에 대한 '글로벌 종합 산업·기업 분석 보고서'를 작성해. 

[입력 데이터]
- 국내 산업: {state['k_industry']}
- 글로벌 산업: {state['g_industry']}
- 국내 기업: {state['k_corporate']}
- 해외 기업: {state['g_corporate']}
- 경쟁 분석: {state['competition']}

작성 양식:

1. 🌐 글로벌 산업 지형 및 패러다임 변화
   - 전 세계적 트렌드와 국내 상황을 대조하여 현재 산업이 어디로 흘러가고 있는지 분석해.
   - 주요 국가들의 정책 변화가 이 산업에 미치는 실질적인 영향력을 기술해.

2. 🚀 기업의 핵심 역량 및 사업 현황 (The Power)
   - 타겟 기업이 현재 주력하고 있는 핵심 프로젝트와 기술적 강점을 디테일하게 분석해.
   - 국내외 사업장에서 거두고 있는 실질적인 성과와 시장 내 위치(Positioning)를 정리해.

3. ⚔️ 경쟁 구도 및 차별화 포인트 (Competitive Edge)
   - 글로벌 라이벌 기업들과의 기술력/사업 모델 비교를 통해 이 기업만의 독보적인 엣지를 찾아내.
   - 경쟁사 대비 부족한 점이 있다면 무엇인지 객관적으로 기술해.

4. 🚨 주요 리스크 및 대응 전략 (Risks & Responses)
   - 현재 직면한 가장 뼈아픈 이슈(부정적 요인)를 짚어내고, 기업이 이를 어떻게 돌파(Response)하고 있는지 구체적 행보를 적어줘.

5. 💡 종합적 전략 인사이트 (Final Synthesis)
   - 모든 분석을 종합했을 때, 이 기업이 미래 시장을 선도하기 위해 견지해야 할 핵심 전략을 제시하며 마무리해.

※ 주의: 일반론은 지양하고, 수집된 데이터 속의 고유 명사, 수치, 프로젝트명을 최대한 활용하여 전문성을 높여라.
"""
    response = model.invoke([
        SystemMessage(content="너는 입체적 시각을 가진 글로벌 수석 리서처다."), 
        HumanMessage(content=prompt)
    ])
    
    # 출처 리스트는 유지 (신뢰성 확보)
    source_text = "\n\n" + "-"*40 + "\n"
    source_text += "📚 분석 근거 (Global Data Sources)\n"
    seen = set()
    count = 1
    for s in state.get("sources", []):
        if s['url'] not in seen and count <= 8:
            source_text += f"[{count}] {s.get('title', 'Reference')} : {s['url']}\n"
            seen.add(s['url'])
            count += 1
            
    return {"messages": [HumanMessage(content=response.content + source_text)]}

# ===== 4. 그래프 구성 (5인 병렬 체제) =====
workflow = StateGraph(State)

# 노드 등록
workflow.add_node("k_ind", k_industry_analyst)
workflow.add_node("g_ind", g_industry_analyst)
workflow.add_node("k_corp", k_corporate_analyst)
workflow.add_node("g_corp", g_corporate_analyst)
workflow.add_node("comp", competition_analyst)
workflow.add_node("chief", chief_strategist)

# 시작 시 5명이 동시에 조사 시작
workflow.add_edge(START, "k_ind")
workflow.add_edge(START, "g_ind")
workflow.add_edge(START, "k_corp")
workflow.add_edge(START, "g_corp")
workflow.add_edge(START, "comp")

# 모두 완료되면 수석 전략가에게 보고
workflow.add_edge("k_ind", "chief")
workflow.add_edge("g_ind", "chief")
workflow.add_edge("k_corp", "chief")
workflow.add_edge("g_corp", "chief")
workflow.add_edge("comp", "chief")

workflow.add_edge("chief", END)

# ===== 5. 체크포인터 설정 및 컴파일 (Phase 1 적용) =====

# 1. DB 연결 (파일 이름은 자유롭게 정하셔도 됩니다)
# check_same_thread=False는 스트림릿 같은 멀티스레드 환경에서 필수입니다!
conn = sqlite3.connect("research_history.db", check_same_thread=False)

# 2. 체크포인터 생성
memory = SqliteSaver(conn)

# 3. 체크포인터를 포함하여 그래프 컴파일
# 이제 이 agent는 모든 단계의 '상태'를 기억하게 됩니다.
agent = workflow.compile(checkpointer=memory)

# (참고) 나중에 외부에서 이 agent를 불러올 때 사용합니다.