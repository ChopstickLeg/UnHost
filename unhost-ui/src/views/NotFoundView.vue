<script setup lang="ts">
import { RouterLink } from 'vue-router'
import BrandMark from '@/components/BrandMark.vue'
import { Button } from '@/components/ui/button'
</script>

<template>
  <main class="flex min-h-svh items-center justify-center px-6 py-12">
    <div class="w-full max-w-sm">
      <BrandMark class="mb-8" />
      <h1 class="text-2xl font-semibold">404 · Page not found</h1>
      <p class="mt-2 text-sm text-muted-foreground">This page does not exist.</p>
      <Button as-child variant="outline" class="mt-6"
        ><RouterLink to="/">Go home</RouterLink></Button
      >
    </div>
  </main>
</template>
