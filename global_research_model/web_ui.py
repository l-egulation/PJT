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

# 커스텀 CSS로 UI 조금 더 예쁘게 다듬기
st.markdown("""
    <style>
    .main {
        background-color: #f5f7f9;
    }
    .stButton>button {
        width: 100%;
        border-radius: 5px;
        height: 3em;
        background-color: #FF4B4B;
        color: white;
    }
    .report-box {
        background-color: white;
        padding: 2rem;
        border-radius: 10px;
        border: 1px solid #e0e0e0;
    }
    </style>
    """, unsafe_allow_html=True)

# 헤더 영역
st.title("🏢 글로벌 6인 체제 리서치 센터")
st.subheader("산업 동향부터 기업 리스크까지, AI 전문가 팀이 실시간 분석합니다.")
st.divider()

# 사이드바 설정 (보안 및 설정)
with st.sidebar:
    st.header("🔑 보안 및 설정")
    
    # --- 핵심 수정 부분 (1/2): API 키 입력창 ---
    st.markdown("### 🛡️ 지갑 방어 모드")
    st.info("본인의 API Key를 입력하면 본인 토큰이 최우선으로 사용됩니다. 비워두면 시스템 설정을 따릅니다.")
    
    # 1. Upstage API Key 입력창
    user_upstage_key = st.text_input("Upstage API Key", type="password", help="비워두면 시스템 크레딧 사용")
    # 2. Tavily API Key 입력창
    user_tavily_key = st.text_input("Tavily API Key", type="password", help="비워두면 시스템 크레딧 사용")
    
    # --- 기존 Upstage 입력창은 아래로 이동하거나 삭제 ---
    # api_key_input = st.text_input("Upstage API Key (비워두면 시스템 설정 사용)", type="password") # 이전 코드
    
    st.divider()
    st.markdown("### 👥 분석 팀 구성")
    st.write("- 🇰🇷 국내 산업/기업 분석관")
    st.write("- 🌎 글로벌 트렌드 분석관")
    st.write("- ⚔️ 경쟁사 비교 분석관")
    st.write("- 🎓 수석 전략 리서처")

# 메인 입력창
col1, col2 = st.columns([4, 1])
with col1:
    target_topic = st.text_input(
        "분석할 기업명이나 산업 키워드를 입력하세요", 
        placeholder="예: 현대자동차, SK하이닉스, Physical AI 등"
    )
with col2:
    st.write(" ") # 수직 맞춤용
    run_button = st.button("분석 시작 🚀")

# 분석 로직 실행
if run_button:
    if not target_topic:
        st.warning("키워드를 입력해 주세요!")
    else:
        # 진행 상태 표시
        status_text = st.empty()
        progress_bar = st.progress(0)
        
        with st.container():
            status_text.info(f"🔍 '{target_topic}'에 대한 글로벌 데이터를 수집하고 있습니다...")
            progress_bar.progress(30)
            
            # --- 핵심 수정 부분 (2/2): API 키 우선순위 로직 ---
            with st.spinner("환경 변수 세팅 및 6인 전문가 분석 중..."):
                try:
                    # 1. 사용자 키가 있으면 환경 변수로 설정
                    if user_upstage_key:
                        os.environ["UPSTAGE_API_KEY"] = user_upstage_key
                    if user_tavily_key:
                        os.environ["TAVILY_API_KEY"] = user_tavily_key
                        
                    # 2. 에이전트 실행 (LangGraph 호출)
                    # thread_id를 고유하게 생성하여 대화 상태 유지 가능
                    config = {"configurable": {"thread_id": f"web_{int(time.time())}"}}
                    inputs = {"messages": [("user", target_topic)]}
                    
                    # 6인 체제 분석 시작
                    result = agent.invoke(inputs, config)
                    progress_bar.progress(70)
                    status_text.info("✍️ 수석 전략가가 종합 보고서를 작성하고 있습니다...")
                    
                    # 결과 추출 (마지막 메시지)
                    final_report = result["messages"][-1].content
                    progress_bar.progress(100)
                    status_text.success("✅ 분석 완료!")
                    
                    # 결과 출력
                    st.divider()
                    st.markdown('<div class="report-box">', unsafe_allow_html=True)
                    st.markdown(final_report)
                    st.markdown('</div>', unsafe_allow_html=True)
                
                except Exception as e:
                    st.error(f"분석 중 오류가 발생했습니다: {str(e)}")
                    st.info("API 키 설정이나 인터넷 연결을 확인해 주세요.")
                    # 오류 발생 시 환경 변수 초기화 (선택 사항)
                    if user_upstage_key:
                        del os.environ["UPSTAGE_API_KEY"]
                    if user_tavily_key:
                        del os.environ["TAVILY_API_KEY"]

# 푸터
st.divider()
st.caption("© 2026 LKJ Global Research Center. Powered by LangGraph & Upstage Solar.")