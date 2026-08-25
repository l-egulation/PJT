<script setup>
import { useRoute } from 'vue-router'

const route = useRoute()
const navItems = [
  { to: '/home', label: '홈', icon: '/icons/nav/home.svg' },
  { to: '/planner', label: '캘린더', icon: '/icons/nav/calendar.svg' },
  { to: '/lookbook', label: '스냅', icon: '/icons/nav/lookbook.svg' },
  { to: '/my', label: 'MY', icon: '/icons/nav/my.svg' },
]

const leftItems = navItems.slice(0, 2)
const rightItems = navItems.slice(2)

function handleNavClick(item) {
  if (item.to === '/lookbook' && route.path === '/lookbook') {
    window.dispatchEvent(new CustomEvent('pickle:snap-main'))
  }
}
</script>

<template>
  <nav class="bottom-nav" aria-label="하단 메뉴">
    <RouterLink v-for="item in leftItems" :key="item.to" :to="item.to" @click="handleNavClick(item)">
      <span class="nav-icon" :style="{ '--icon': `url(${item.icon})` }" aria-hidden="true"></span>
      <span>{{ item.label }}</span>
    </RouterLink>

    <RouterLink class="bottom-nav-center" to="/recommend" aria-label="추천 조건 설정">
      <span class="bottom-nav-orb">
        <img src="/images/pickle-buddy-hanger-full.png" alt="" />
      </span>
    </RouterLink>

    <RouterLink v-for="item in rightItems" :key="item.to" :to="item.to" @click="handleNavClick(item)">
      <span class="nav-icon" :style="{ '--icon': `url(${item.icon})` }" aria-hidden="true"></span>
      <span>{{ item.label }}</span>
    </RouterLink>
  </nav>
</template>
