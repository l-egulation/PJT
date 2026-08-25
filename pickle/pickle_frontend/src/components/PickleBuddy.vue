<script setup>
const props = defineProps({
  size: { type: String, default: 'large' },
  variant: { type: String, default: 'hanger' },
})

const variants = {
  hanger: 'pickle-buddy-hanger.png',
  full: 'pickle-buddy-hanger-full.png',
  profile: 'pickle-buddy-profile.png',
  heart: 'pickle-buddy-heart.png',
  shy: 'pickle-buddy-shy.png',
  clothes: 'pickle-buddy-clothes.png',
  umbrella: 'pickle-buddy-umbrella.png',
  wave: 'pickle-buddy-wave.png',
  wink: 'pickle-buddy-wink.png',
}

const imagePath = `${window.location.origin}/images/${variants[props.variant] || variants.hanger}?v=transparent-2`
</script>

<template>
  <div class="pickle-buddy pickle-buddy-image" :class="`pickle-buddy--${size}`">
    <img :src="imagePath" alt="pickle 새싹 마스코트" />
  </div>
</template>
