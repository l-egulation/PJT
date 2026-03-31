import streamlit as st
import sys
import os
import time

# 현재 파일이 있는 폴더를 파이썬이 찾을 수 있게 경로에 추가합니다.
current_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.append(current_dir)

from app import agent  # 사용자님이 만든 6인 체제 에이전트 불러오기

# 페이지 설정
st.set_page_config(
    page_title="LKJ 글로벌 리서치 센터",
    page_icon="🏢",
    layout="wide"
)

# [추가] 세션 상태 초기화 (리포트 저장용)
if "final_report" not in st.session_state:
    st.session_state.final_report = None

# 커스텀 CSS (기존 동일)
st.markdown("""
    <style>
    .main { background-color: #f5f7f9; }
    .stButton>button { width: 100%; border-radius: 5px; height: 3em; background-color: #FF4B4B; color: white; }
    .report-box { background-color: white; padding: 2rem; border-radius: 10px; border: 1px solid #e0e0e0; min-height: 400px; }
    </style>
    """, unsafe_allow_html=True)

# 헤더 영역 (기존 동일)
st.title("🏢 글로벌 6인 체제 리서치 센터")
st.subheader("산업 동향부터 기업 리스크까지, AI 전문가 팀이 실시간 분석합니다.")
st.divider()

# 사이드바 설정
with st.sidebar:
    st.header("🔑 보안 및 설정")
    
    # 🛡️ 지갑 방어 모드 (기존 동일)
    st.markdown("### 🛡️ 지갑 방어 모드")
    user_upstage_key = st.text_input("Upstage API Key", type="password")
    user_tavily_key = st.text_input("Tavily API Key", type="password")
    
    st.divider()
    
    # [추가] 🕰️ 히스토리 관리 섹션
    st.header("🕰️ 히스토리 관리")
    # 사용자가 직접 thread_id를 정하게 하면 나중에 다시 불러올 수 있습니다.
    thread_id = st.text_input("세션 ID (저장/불러오기용)", value="user_01")
    
    if st.button("과거 리포트 불러오기 🔄"):
        config = {"configurable": {"thread_id": thread_id}}
        # 오늘 배운 get_state 활용!
        state = agent.get_state(config)
        if state.values and "messages" in state.values:
            st.session_state.final_report = state.values["messages"][-1].content
            st.success(f"'{thread_id}' 세션의 기록을 불러왔습니다.")
        else:
            st.error("해당 ID로 저장된 기록이 없습니다.")

    st.divider()
    st.markdown("### 👥 분석 팀 구성")
    st.write("- 🇰🇷 국내 산업/기업 분석관...")

# 메인 입력창
col1, col2 = st.columns([4, 1])
with col1:
    target_topic = st.text_input("분석할 기업명이나 산업 키워드를 입력하세요", placeholder="예: 현대자동차")
with col2:
    st.write(" ") 
    run_button = st.button("분석 시작 🚀")

# 분석 로직 실행
if run_button:
    if not target_topic:
        st.warning("키워드를 입력해 주세요!")
    else:
        status_text = st.empty()
        progress_bar = st.progress(0)
        
        with st.container():
            status_text.info(f"🔍 '{target_topic}' 분석 시작...")
            
            try:
                if user_upstage_key: os.environ["UPSTAGE_API_KEY"] = user_upstage_key
                if user_tavily_key: os.environ["TAVILY_API_KEY"] = user_tavily_key
                
                # [수정] 고정된 thread_id 사용 (저장을 위해)
                config = {"configurable": {"thread_id": thread_id}}
                inputs = {"messages": [("user", target_topic)]}
                
                with st.spinner("전문가 팀이 협업 중입니다..."):
                    result = agent.invoke(inputs, config)
                    st.session_state.final_report = result["messages"][-1].content
                
                status_text.success("✅ 분석 완료!")
                progress_bar.progress(100)
                
            except Exception as e:
                st.error(f"오류 발생: {str(e)}")

# [수정] 결과 출력 영역 (세션 상태에 리포트가 있으면 출력)
if st.session_state.final_report:
    st.divider()
    st.warning("⚠️ 본 리포트는 실시간 검색 데이터를 바탕으로 생성되었으며, 실제 사실과 다를 수 있습니다. 출처 링크를 반드시 확인하세요.")
    st.markdown('<div class="report-box">', unsafe_allow_html=True)
    st.markdown(st.session_state.final_report)
    st.markdown('</div>', unsafe_allow_html=True)

# 푸터
st.divider()
st.caption("© 2026 LKJ Global Research Center. Powered by LangGraph & Upstage Solar.")