<script setup>
import { colorHex } from '@/utils/colors'

defineProps({
  item: { type: Object, required: true },
  selectable: { type: Boolean, default: false },
})

defineEmits(['select'])

const labels = {
  top: '상의',
  bottom: '하의',
  outer: '아우터',
  dress: '원피스',
  shoes: '신발',
  bag: '가방',
  accessory: '액세서리',
}
</script>

<template>
  <button v-if="selectable" class="clothes-card clothes-card-link selectable" type="button" @click="$emit('select', item)">
    <div class="clothes-thumb" :style="{ '--tone': colorHex(item.color) }">
      <img v-if="item.processed_image || item.image" :src="item.processed_image || item.image" :alt="item.name" />
      <span v-else-if="item.category === 'shoes'" class="shoe-shape"></span>
      <span v-else-if="item.category === 'bottom'" class="pants-shape"></span>
      <span v-else class="top-shape"></span>
    </div>
    <div class="clothes-meta">
      <strong>{{ item.name }}</strong>
      <span>{{ labels[item.category] }} · {{ item.color || '색상 미정' }}</span>
      <small>이 옷으로 교체</small>
    </div>
  </button>

  <RouterLink v-else class="clothes-card clothes-card-link" :to="`/clothes/${item.id}`">
    <div class="clothes-thumb" :style="{ '--tone': colorHex(item.color) }">
      <img v-if="item.processed_image || item.image" :src="item.processed_image || item.image" :alt="item.name" />
      <span v-else-if="item.category === 'shoes'" class="shoe-shape"></span>
      <span v-else-if="item.category === 'bottom'" class="pants-shape"></span>
      <span v-else class="top-shape"></span>
    </div>
    <div class="clothes-meta">
      <strong>{{ item.name }}</strong>
      <span>{{ labels[item.category] }} · {{ item.color || '색상 미정' }}</span>
      <small>상세 보기 →</small>
    </div>
  </RouterLink>
</template>
