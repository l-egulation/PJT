import streamlit as st
import sys
import os

current_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.append(current_dir)

from app import agent 

st.set_page_config(
    page_title="LKJ 글로벌 리서치 센터",
    page_icon="🏢",
    layout="wide"
)

if "final_report" not in st.session_state:
    st.session_state.final_report = None
if "sources" not in st.session_state:
    st.session_state.sources = []

st.title("🏢 글로벌 6인 체제 리서치 센터")
st.subheader("산업 동향부터 기업 리스크까지, AI 전문가 팀이 실시간 분석합니다.")
st.divider()

with st.sidebar:
    st.header("🔑 보안 및 설정")
    user_google_key = st.text_input("Gemini API Key", type="password")
    user_tavily_key = st.text_input("Tavily API Key", type="password")
    user_dart_key = st.text_input("DART Open API Key", type="password")
    
    st.divider()
    st.header("🕰️ 히스토리 관리")
    thread_id = st.text_input("세션 ID (저장/불러오기용)", value="user_01")
    
    if st.button("과거 리포트 불러오기 🔄"):
        config = {"configurable": {"thread_id": thread_id}}
        state = agent.get_state(config)
        if state.values and "messages" in state.values:
            st.session_state.final_report = state.values["messages"][-1].content
            st.session_state.sources = state.values.get("sources", [])
            st.success(f"'{thread_id}' 세션의 기록을 불러왔습니다.")
        else:
            st.error("해당 ID로 저장된 기록이 없습니다.")

col1, col2 = st.columns([4, 1])
with col1:
    target_topic = st.text_input("분석할 기업명이나 산업 키워드를 입력하세요", placeholder="예: 현대자동차")
with col2:
    st.write(" ") 
    run_button = st.button("분석 시작 🚀")

if run_button:
    if not target_topic:
        st.warning("키워드를 입력해 주세요!")
    else:
        status_text = st.empty()
        
        try:
            if user_google_key: os.environ["GOOGLE_API_KEY"] = user_google_key
            elif "GOOGLE_API_KEY" in st.secrets: os.environ["GOOGLE_API_KEY"] = st.secrets["GOOGLE_API_KEY"]
            
            if user_tavily_key: os.environ["TAVILY_API_KEY"] = user_tavily_key
            elif "TAVILY_API_KEY" in st.secrets: os.environ["TAVILY_API_KEY"] = st.secrets["TAVILY_API_KEY"]

            if user_dart_key: os.environ["DART_API_KEY"] = user_dart_key
            elif "DART_API_KEY" in st.secrets: os.environ["DART_API_KEY"] = st.secrets["DART_API_KEY"]
            
            config = {"configurable": {"thread_id": thread_id}}
            inputs = {"messages": [("user", target_topic)]}
            
            with st.spinner("DART 공시서류 및 실시간 뉴스를 분석 중입니다..."):
                result = agent.invoke(inputs, config)
                st.session_state.final_report = result["messages"][-1].content
                st.session_state.sources = result.get("sources", [])
            
            status_text.success("✅ 분석 완료!")
            
        except Exception as e:
            st.error(f"오류 발생: {str(e)}")

# [결과 출력 영역 - 깔끔한 스티커 컨테이너 사용]
if st.session_state.final_report:
    st.divider()
    st.warning("⚠️ 본 리포트는 실시간 검색 데이터를 바탕으로 생성되었습니다.")
    
    # 1. 본문 보고서 박스
    with st.container(border=True):
        st.markdown(st.session_state.final_report)
    
    # 2. 참고 뉴스 기사 출처 링크 (아코디언 형식)
    if st.session_state.sources:
        st.write("")
        with st.expander("📚 분석에 활용된 실시간 뉴스 기사 및 데이터 출처 보기"):
            seen_urls = set()
            count = 1
            for src in st.session_state.sources:
                url = src.get("url")
                title = src.get("title", "참고 기사")
                if url and url not in seen_urls:
                    st.markdown(f"**[{count}]** [{title}]({url})")
                    seen_urls.add(url)
                    count += 1

st.divider()
st.caption("© 2026 LKJ Global Research Center. Powered by LangGraph & Google Gemini 2.5 Flash.")