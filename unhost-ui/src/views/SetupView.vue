<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import BrandMark from '@/components/BrandMark.vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { setup } from '@/lib/auth'
import { session } from '@/lib/session'

const router = useRouter()
const email = ref('')
const password = ref('')
const bootstrapSecret = ref('')
const pending = ref(false)
const error = ref('')

async function createAccount() {
  if (pending.value) return
  pending.value = true
  error.value = ''
  try {
    await setup({
      kind: 'setup',
      email: email.value.trim(),
      password: password.value,
      bootstrapSecret: bootstrapSecret.value,
    })
    session.value = { kind: 'signed-out' }
    await router.replace({ name: 'login', query: { created: '1' } })
  } catch (cause) {
    error.value =
      cause instanceof Error ? cause.message : 'Unable to create your account. Please try again.'
  } finally {
    pending.value = false
  }
}
</script>

<template>
  <main class="flex min-h-svh items-center justify-center px-6 py-12">
    <div class="w-full max-w-sm">
      <BrandMark class="mb-8" />
      <h1 class="text-2xl font-semibold">Set up UnHost</h1>
      <p class="mt-2 text-sm text-muted-foreground">Create your operator account.</p>
      <form class="mt-6 space-y-5" :aria-busy="pending" @submit.prevent="createAccount">
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
            autocomplete="new-password"
            aria-describedby="password-hint"
            minlength="6"
            required
            :disabled="pending"
          />
          <p id="password-hint" class="text-sm leading-5 text-muted-foreground">
            Use at least 6 characters with uppercase, lowercase, a number, and a symbol.
          </p>
        </div>
        <div class="space-y-2">
          <Label for="setup-key">Setup key</Label>
          <Input
            id="setup-key"
            v-model="bootstrapSecret"
            type="password"
            autocomplete="off"
            aria-describedby="setup-key-hint"
            required
            :disabled="pending"
          />
          <p id="setup-key-hint" class="text-sm leading-5 text-muted-foreground">
            Enter the bootstrap secret configured for this instance.
          </p>
        </div>
        <p v-if="error" role="alert" class="text-sm text-red-300">{{ error }}</p>
        <Button type="submit" class="w-full" :disabled="pending">{{
          pending ? 'Please wait…' : 'Create account'
        }}</Button>
      </form>
    </div>
  </main>
</template>
