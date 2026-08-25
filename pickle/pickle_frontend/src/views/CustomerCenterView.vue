<script setup>
import PhoneFrame from '@/components/PhoneFrame.vue'

const contacts = [
  { title: '이용 문의', desc: '서비스 사용 중 불편한 점을 알려주세요.' },
  { title: '계정 문의', desc: '로그인, 회원정보, 탈퇴 관련 도움을 받을 수 있어요.' },
  { title: '스냅 신고', desc: '부적절한 스냅이나 댓글은 상세 화면의 더보기에서 신고해 주세요.' },
]
</script>

<template>
  <PhoneFrame title="고객센터" show-back>
    <section class="customer-center-hero">
      <strong>무엇을 도와드릴까요?</strong>
      <p>피클 사용 중 궁금한 점이나 불편한 점을 확인해 주세요.</p>
    </section>

    <section class="customer-center-list">
      <article v-for="item in contacts" :key="item.title">
        <strong>{{ item.title }}</strong>
        <p>{{ item.desc }}</p>
      </article>
    </section>

    <section class="customer-center-note">
      <strong>운영 시간</strong>
      <p>평일 10:00 - 18:00</p>
      <small>현재는 안내 화면만 제공되며, 문의 접수 기능은 추후 연결 예정입니다.</small>
    </section>
  </PhoneFrame>
</template>
