<script setup>
import { computed } from 'vue'
import { colorHex } from '@/utils/colors'

const props = defineProps({
  items: { type: Array, default: () => [] },
})

const topItem = computed(() => props.items.find(item => item.category === 'top'))
const dressItem = computed(() => props.items.find(item => item.category === 'dress'))
const outerItem = computed(() => props.items.find(item => item.category === 'outer'))
const bottomItem = computed(() => props.items.find(item => item.category === 'bottom'))
const shoesItem = computed(() => props.items.find(item => item.category === 'shoes'))
const bagItem = computed(() => props.items.find(item => ['bag', 'accessory'].includes(item.category)))
const hasTopBottom = computed(() =>
  (topItem.value || outerItem.value) && bottomItem.value && !dressItem.value
)

const isEmpty = computed(() =>
  !topItem.value &&
  !dressItem.value &&
  !outerItem.value &&
  !bottomItem.value &&
  !shoesItem.value &&
  !bagItem.value
)

function img(item) {
  return item.flatlay_image || item.processed_image || item.image
}
</script>

<template>
  <div class="flatlay-canvas" :class="{ 'is-connected-look': hasTopBottom }">
    <div
      v-if="outerItem"
      class="flatlay-item"
      :class="(topItem || dressItem) ? 'flatlay-outer' : 'flatlay-top'"
    >
      <img v-if="img(outerItem)" :src="img(outerItem)" :alt="outerItem.name" />
      <i v-else class="flatlay-swatch" :style="{ background: colorHex(outerItem.color) }"></i>
    </div>

    <div
      v-if="topItem"
      class="flatlay-item"
      :class="outerItem ? 'flatlay-top-layered' : 'flatlay-top'"
    >
      <img v-if="img(topItem)" :src="img(topItem)" :alt="topItem.name" />
      <i v-else class="flatlay-swatch" :style="{ background: colorHex(topItem.color) }"></i>
    </div>

    <div v-if="dressItem" class="flatlay-item flatlay-dress">
      <img v-if="img(dressItem)" :src="img(dressItem)" :alt="dressItem.name" />
      <i v-else class="flatlay-swatch" :style="{ background: colorHex(dressItem.color) }"></i>
    </div>

    <div v-if="bottomItem && !dressItem" class="flatlay-item flatlay-bottom">
      <img v-if="img(bottomItem)" :src="img(bottomItem)" :alt="bottomItem.name" />
      <i v-else class="flatlay-swatch" :style="{ background: colorHex(bottomItem.color) }"></i>
    </div>

    <div v-if="bagItem" class="flatlay-item flatlay-bag">
      <img v-if="img(bagItem)" :src="img(bagItem)" :alt="bagItem.name" />
      <i v-else class="flatlay-swatch" :style="{ background: colorHex(bagItem.color) }"></i>
    </div>

    <div v-if="shoesItem" class="flatlay-item flatlay-shoes">
      <img v-if="img(shoesItem)" :src="img(shoesItem)" :alt="shoesItem.name" />
      <i v-else class="flatlay-swatch" :style="{ background: colorHex(shoesItem.color) }"></i>
    </div>

    <div v-if="isEmpty" class="flatlay-empty">
      <span>옷장에 아이템을 등록해주세요.</span>
    </div>
  </div>
</template>
