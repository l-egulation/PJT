<script setup>
import PhoneFrame from '@/components/PhoneFrame.vue'
</script>

<template>
  <PhoneFrame>
    <section class="page-heading closet-start-heading">
      <h1>옷장부터 채워볼까요?</h1>
    </section>

    <div class="closet-intro-art pickle-closet-intro" aria-hidden="true">
      <span class="closet-intro-glow"></span>
      <img src="/images/pickle-buddy-clothes.png?v=transparent-2" alt="" />
    </div>

    <div class="intro-copy">
      <strong>내 옷장을 채워주세요</strong>
      <p>나에게 꼭 맞는 코디를 추천할 수 있도록<br />사진이나 쇼핑몰 링크로 쉽게 등록할 수 있어요.</p>
    </div>

    <div class="button-stack">
      <button class="primary-button wide" type="button" @click="$router.push('/clothes/new')">
        <span class="button-icon ui-icon" style="--icon: url('/icons/ui/camera.svg')" aria-hidden="true"></span>
        사진으로 등록
      </button>
      <button class="secondary-button wide" type="button" @click="$router.push('/clothes/new')">
        <span class="button-icon ui-icon" style="--icon: url('/icons/ui/link.svg')" aria-hidden="true"></span>
        쇼핑몰 URL 참고 등록
      </button>
      <button class="text-button" type="button" @click="$router.push('/closet')">나중에 할게요</button>
    </div>
  </PhoneFrame>
</template>
