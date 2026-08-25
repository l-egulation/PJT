<script setup>
import BottomNav from '@/components/BottomNav.vue'

defineProps({
  title: { type: String, default: '' },
  showBack: { type: Boolean, default: false },
  showNav: { type: Boolean, default: false },
})
</script>

<template>
  <main class="phone-frame" :class="{ 'has-app-header': title || showBack || $slots.action || $slots.title }">
    <header v-if="title || showBack || $slots.action || $slots.title" class="phone-header">
      <button v-if="showBack" class="icon-button" type="button" aria-label="뒤로 가기" @click="$router.back()">
        <span class="ui-icon" style="--icon: url('/icons/ui/angle-small-left.svg')" aria-hidden="true"></span>
      </button>
      <span v-else class="header-spacer"></span>
      <h1><slot name="title">{{ title }}</slot></h1>
      <slot name="action"><span class="header-spacer"></span></slot>
    </header>
    <section class="screen" :class="{ 'with-nav': showNav }">
      <slot />
    </section>
    <BottomNav v-if="showNav" />
  </main>
</template>
