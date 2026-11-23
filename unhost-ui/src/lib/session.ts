import { shallowRef } from 'vue'
import { getOperator, getSetupComplete, type Operator } from './auth'

type Session =
  | { kind: 'unknown' }
  | { kind: 'setup-required' }
  | { kind: 'signed-out' }
  | { kind: 'signed-in'; operator: Operator }
  | { kind: 'unavailable'; message: string }

export const session = shallowRef<Session>({ kind: 'unknown' })

export async function loadSession() {
  try {
    if (!(await getSetupComplete())) {
      session.value = { kind: 'setup-required' }
      return
    }
    const operator = await getOperator()
    session.value = operator ? { kind: 'signed-in', operator } : { kind: 'signed-out' }
  } catch (cause) {
    session.value = {
      kind: 'unavailable',
      message: cause instanceof Error ? cause.message : 'Unable to connect. Please try again.',
    }
  }
}
