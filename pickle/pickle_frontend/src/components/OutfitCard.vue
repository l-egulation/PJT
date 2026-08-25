<script setup>
import { colorHex } from '@/utils/colors'

defineProps({
  outfit: { type: Object, default: () => ({ items: [] }) },
  compact: { type: Boolean, default: false },
})

const shapes = { top: 'shirt', outer: 'shirt', dress: 'shirt', bottom: 'pants', shoes: 'shoes', bag: 'bag', accessory: 'bag' }
</script>

<template>
  <article class="outfit-card" :class="{ compact }">
    <div v-if="outfit.items?.length" class="outfit-grid">
      <div v-for="item in outfit.items" :key="item.id" class="outfit-piece" :class="`outfit-piece--${shapes[item.category]}`">
        <img v-if="item.processed_image || item.image" :src="item.processed_image || item.image" :alt="item.name" />
        <i v-else class="piece-visual" :style="{ '--tone': colorHex(item.color) }"></i>
        <span>{{ item.name }}</span>
      </div>
    </div>
    <p v-else class="empty-state">옷장에 상의와 하의를 등록해 주세요.</p>
    <div class="outfit-copy">
      <div class="outfit-swatches">
        <i v-for="item in outfit.items?.slice(0, 5)" :key="item.id" :style="{ background: colorHex(item.color, '#ddd') }"></i>
      </div>
      <h2>{{ outfit.title || '오늘의 추천 코디' }}</h2>
      <p>{{ outfit.description || '내 옷장 데이터에서 고른 조합입니다.' }}</p>
    </div>
  </article>
</template>
