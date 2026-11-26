<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { Check, Copy } from '@lucide/vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { getCluster, getJoinCommands, importCluster, initializeCluster } from '@/lib/cluster'
import type { JoinCommands, LocalCluster } from '@/lib/cluster'

type JoinRole = keyof JoinCommands

const cluster = ref<LocalCluster | null>(null)
const joins = ref<JoinCommands | null>(null)
const advertiseAddress = ref('')
const pending = ref(false)
const error = ref('')
const copiedRole = ref<JoinRole | null>(null)
let copiedResetTimer: ReturnType<typeof setTimeout> | undefined

const connectionLabels = {
  disconnected: 'Disconnected',
  incompatible: 'Incompatible Docker Engine API',
  unavailable: 'Swarm unavailable',
  standalone: 'Standalone Engine',
  manager: 'Swarm manager',
  worker: 'Swarm worker',
}

function clearCopiedState() {
  clearTimeout(copiedResetTimer)
  copiedResetTimer = undefined
  copiedRole.value = null
}

async function copyText(text: string): Promise<void> {
  if (navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(text)
      return
    } catch {
      // Fall back for non-secure origins or denied clipboard permissions.
    }
  }

  const textarea = document.createElement('textarea')
  textarea.value = text
  textarea.setAttribute('readonly', '')
  textarea.style.position = 'fixed'
  textarea.style.opacity = '0'
  document.body.appendChild(textarea)
  textarea.select()
  let copied = false
  try {
    copied = document.execCommand('copy')
  } finally {
    document.body.removeChild(textarea)
  }
  if (!copied) {
    throw new Error('Unable to copy the join command. Select and copy it manually.')
  }
}

async function copyJoinCommand(role: JoinRole, command: string) {
  error.value = ''
  try {
    await copyText(command)
    clearCopiedState()
    copiedRole.value = role
    copiedResetTimer = setTimeout(() => {
      copiedRole.value = null
      copiedResetTimer = undefined
    }, 2000)
  } catch (cause) {
    error.value =
      cause instanceof Error ? cause.message : 'Unable to copy the join command. Select and copy it manually.'
  }
}

async function run(action: () => Promise<void>) {
  if (pending.value) return
  pending.value = true
  error.value = ''
  try {
    await action()
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : 'Unable to complete the request.'
  } finally {
    pending.value = false
  }
}

async function loadCluster() {
  clearCopiedState()
  joins.value = null
  cluster.value = null
  cluster.value = await getCluster()
}

function refresh() {
  return run(loadCluster)
}

function initialize() {
  return run(async () => {
    await initializeCluster(advertiseAddress.value.trim())
    await loadCluster()
  })
}

function importSwarm() {
  return run(async () => {
    await importCluster()
    await loadCluster()
  })
}

function toggleJoinCommands() {
  if (joins.value) {
    clearCopiedState()
    joins.value = null
    return
  }
  return run(async () => { joins.value = await getJoinCommands() })
}

onMounted(refresh)
onBeforeUnmount(clearCopiedState)
</script>

<template>
  <section aria-labelledby="cluster-heading" :aria-busy="pending" class="space-y-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 id="cluster-heading" class="text-xl font-semibold">Local cluster</h2>
      <Button variant="outline" :disabled="pending" @click="refresh">Refresh</Button>
    </div>

    <p v-if="error" role="alert" class="text-sm text-red-300">{{ error }}</p>
    <p v-if="pending && !cluster" role="status" class="text-sm text-muted-foreground">
      Checking the Docker connection…
    </p>

    <template v-if="cluster">
      <div class="space-y-4 rounded-lg border border-border p-5">
        <div>
          <p class="font-medium">{{ connectionLabels[cluster.state] }}</p>
          <p v-if="cluster.engineVersion" class="text-sm text-muted-foreground">
            Docker Engine {{ cluster.engineVersion }}
          </p>
        </div>
        <p v-if="cluster.state === 'disconnected'" class="text-sm text-muted-foreground">
          UnHost cannot reach the local Docker Engine. Check that Docker is running and that the
          UnHost container can access its socket.
        </p>
        <p v-else-if="cluster.state === 'incompatible'" class="text-sm text-muted-foreground">
          The local Docker Engine API version is outside the range UnHost supports.
        </p>
        <p v-else-if="cluster.state === 'unavailable'" class="text-sm text-muted-foreground">
          Docker is reachable. Check the node’s Swarm status and refresh when it is ready.
        </p>
        <p v-else-if="cluster.state === 'worker'" class="text-sm text-muted-foreground">
          This host already belongs to a Swarm. Connect UnHost to a manager to administer it.
        </p>
        <form v-else-if="cluster.state === 'standalone'" class="max-w-md space-y-3" @submit.prevent="initialize">
          <Label for="advertise-address">Advertised address</Label>
          <Input id="advertise-address" v-model="advertiseAddress" required maxlength="255"
            placeholder="192.168.1.10" aria-describedby="advertise-help" />
          <p id="advertise-help" class="text-sm text-muted-foreground">
            Enter the host IP address or network interface that other nodes can reach.
          </p>
          <Button type="submit" :disabled="pending || !advertiseAddress.trim()">Initialize Swarm</Button>
        </form>
        <template v-else>
          <p class="break-all text-sm text-muted-foreground">Swarm ID {{ cluster.swarmId }}</p>
          <Button v-if="!cluster.imported" :disabled="pending" @click="importSwarm">Import Swarm</Button>
          <p v-else class="text-sm">Imported into UnHost</p>
        </template>
      </div>

      <section v-if="cluster.state === 'manager'" aria-labelledby="nodes-heading" class="space-y-3">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <h3 id="nodes-heading" class="text-lg font-semibold">Nodes</h3>
          <Button v-if="cluster.imported" variant="outline" :disabled="pending"
            :aria-expanded="joins !== null" @click="toggleJoinCommands">
            {{ joins ? 'Hide join commands' : 'Show join commands' }}
          </Button>
        </div>
        <div v-if="joins" class="space-y-4 rounded-lg border border-border p-4">
          <p class="text-sm text-muted-foreground">Run the command for the desired role on the node you want to join.</p>
          <div>
            <p class="text-sm font-medium">Worker</p>
            <div class="mt-1 flex items-center gap-2 rounded bg-muted py-1.5 pr-1.5 pl-3">
              <pre class="min-w-0 flex-1 overflow-x-auto py-1.5 text-xs"><code>{{ joins.worker }}</code></pre>
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                class="shrink-0 text-muted-foreground hover:text-foreground"
                :title="copiedRole === 'worker' ? 'Copied' : 'Copy command'"
                :aria-label="copiedRole === 'worker' ? 'Copied worker join command' : 'Copy worker join command'"
                @click="copyJoinCommand('worker', joins.worker)"
              >
                <Check v-if="copiedRole === 'worker'" class="size-3.5" aria-hidden="true" />
                <Copy v-else class="size-3.5" aria-hidden="true" />
              </Button>
            </div>
          </div>
          <div>
            <p class="text-sm font-medium">Manager</p>
            <div class="mt-1 flex items-center gap-2 rounded bg-muted py-1.5 pr-1.5 pl-3">
              <pre class="min-w-0 flex-1 overflow-x-auto py-1.5 text-xs"><code>{{ joins.manager }}</code></pre>
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                class="shrink-0 text-muted-foreground hover:text-foreground"
                :title="copiedRole === 'manager' ? 'Copied' : 'Copy command'"
                :aria-label="copiedRole === 'manager' ? 'Copied manager join command' : 'Copy manager join command'"
                @click="copyJoinCommand('manager', joins.manager)"
              >
                <Check v-if="copiedRole === 'manager'" class="size-3.5" aria-hidden="true" />
                <Copy v-else class="size-3.5" aria-hidden="true" />
              </Button>
            </div>
          </div>
        </div>
        <div v-if="cluster.nodes.length" class="overflow-x-auto rounded-lg border border-border">
          <table class="w-full text-left text-sm">
            <thead class="border-b border-border text-muted-foreground">
              <tr>
                <th scope="col" class="px-4 py-3 font-medium">Hostname</th>
                <th scope="col" class="px-4 py-3 font-medium">Role</th>
                <th scope="col" class="px-4 py-3 font-medium">Readiness</th>
                <th scope="col" class="px-4 py-3 font-medium">Availability</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-border">
              <tr v-for="node in cluster.nodes" :key="node.id">
                <th scope="row" class="px-4 py-3 font-medium">{{ node.hostname }}</th>
                <td class="px-4 py-3 capitalize">{{ node.role }}</td>
                <td class="px-4 py-3 capitalize">{{ node.readiness }}</td>
                <td class="px-4 py-3 capitalize">{{ node.availability }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <p v-else class="text-sm text-muted-foreground">Docker has not reported any nodes.</p>
      </section>
    </template>
  </section>
</template>
