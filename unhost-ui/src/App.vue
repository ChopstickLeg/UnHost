<script setup lang="ts">
import { RouterView } from 'vue-router'
</script>

<template>
  <RouterView v-slot="{ Component }">
    <component :is="Component" v-if="Component" />
    <main v-else class="flex min-h-svh items-center justify-center px-6">
      <p role="status" class="text-sm text-muted-foreground">Loading…</p>
    </main>
  </RouterView>
</template>
