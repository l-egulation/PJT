<template>
  <div class="app-shell">
    <RouterView v-slot="{ Component, route }">
      <Transition :name="route.query.motion === 'back' ? 'slide-back' : 'slide-forward'" mode="out-in">
        <component :is="Component" :key="route.fullPath" />
      </Transition>
    </RouterView>
  </div>
</template>
