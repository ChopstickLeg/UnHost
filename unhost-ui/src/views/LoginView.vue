<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import BrandMark from '@/components/BrandMark.vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { login } from '@/lib/auth'
import { session } from '@/lib/session'

const route = useRoute()
const router = useRouter()
const email = ref('')
const password = ref('')
const pending = ref(false)
const error = ref('')

async function signIn() {
  if (pending.value) return
  pending.value = true
  error.value = ''
  try {
    const operator = await login({
      kind: 'login',
      email: email.value.trim(),
      password: password.value,
    })
    session.value = { kind: 'signed-in', operator }
    await router.replace({ name: 'dashboard' })
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : 'Unable to sign in. Please try again.'
  } finally {
    pending.value = false
  }
}
</script>

<template>
  <main class="flex min-h-svh items-center justify-center px-6 py-12">
    <div class="w-full max-w-sm">
      <BrandMark class="mb-8" />
      <h1 class="text-2xl font-semibold">Sign in</h1>
      <form class="mt-6 space-y-5" :aria-busy="pending" @submit.prevent="signIn">
        <div class="space-y-2">
          <Label for="email">Email</Label>
          <Input
            id="email"
            v-model="email"
            type="email"
            autocomplete="username"
            required
            :disabled="pending"
          />
        </div>
        <div class="space-y-2">
          <Label for="password">Password</Label>
          <Input
            id="password"
            v-model="password"
            type="password"
            autocomplete="current-password"
            required
            :disabled="pending"
          />
        </div>
        <p v-if="error" role="alert" class="text-sm text-red-300">{{ error }}</p>
        <p v-if="route.query.created === '1'" role="status" class="text-sm text-muted-foreground">
          Account created. Sign in to continue.
        </p>
        <Button type="submit" class="w-full" :disabled="pending">{{
          pending ? 'Please wait…' : 'Sign in'
        }}</Button>
      </form>
    </div>
  </main>
</template>
