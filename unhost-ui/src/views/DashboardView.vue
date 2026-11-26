<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import BrandMark from '@/components/BrandMark.vue'
import LocalClusterPanel from '@/components/LocalClusterPanel.vue'
import { Button } from '@/components/ui/button'
import { logout } from '@/lib/auth'
import { session } from '@/lib/session'

const router = useRouter()
const pending = ref(false)
const error = ref('')

async function signOut() {
  if (pending.value) return
  pending.value = true
  error.value = ''
  try {
    await logout()
    session.value = { kind: 'signed-out' }
    await router.replace({ name: 'login' })
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : 'Unable to sign out. Please try again.'
  } finally {
    pending.value = false
  }
}
</script>

<template>
  <div v-if="session.kind === 'signed-in'" class="min-h-svh">
    <header class="border-b border-border">
      <div class="flex items-center justify-between gap-4 px-6 py-4">
        <RouterLink
          :to="{ name: 'dashboard' }"
          class="inline-flex rounded-md outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
        >
          <BrandMark />
        </RouterLink>
        <div class="flex min-w-0 items-center gap-4">
          <span class="truncate text-sm text-muted-foreground">{{ session.operator.email }}</span>
          <Button variant="ghost" class="shrink-0" :disabled="pending" @click="signOut">
            {{ pending ? 'Signing out…' : 'Sign out' }}
          </Button>
        </div>
      </div>
    </header>
    <main class="space-y-10 px-6 py-10">
      <p v-if="error" role="alert" class="text-sm text-red-300">{{ error }}</p>
      <section aria-labelledby="dashboard-heading" class="space-y-3">
        <h1 id="dashboard-heading" class="text-2xl font-semibold">Dashboard</h1>
        <p class="text-sm text-muted-foreground">No applications yet.</p>
      </section>
      <LocalClusterPanel />
    </main>
  </div>
</template>
