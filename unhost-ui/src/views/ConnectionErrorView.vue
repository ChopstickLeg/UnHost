<script setup lang="ts">
import { useRouter } from 'vue-router'
import { Button } from '@/components/ui/button'
import { session } from '@/lib/session'

const router = useRouter()
function retry() {
  session.value = { kind: 'unknown' }
  void router.replace('/')
}
</script>

<template>
  <main class="flex min-h-svh items-center justify-center px-6">
    <div class="max-w-sm space-y-4">
      <p role="alert" class="text-sm text-muted-foreground">
        {{
          session.kind === 'unavailable' ? session.message : 'Unable to connect. Please try again.'
        }}
      </p>
      <Button variant="outline" @click="retry">Try again</Button>
    </div>
  </main>
</template>
