<script setup>
defineProps({
  label: {
    type: String,
    required: true,
  },
  selected: {
    type: Boolean,
    default: false,
  },
})

defineEmits(['click'])
</script>

<template>
  <button class="tag-chip" :class="{ selected }" type="button" @click="$emit('click')">
    {{ label }}
  </button>
</template>
