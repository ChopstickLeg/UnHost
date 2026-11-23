import { createRouter, createWebHistory, type RouterHistory } from 'vue-router'
import { loadSession, session } from '@/lib/session'
import LoginView from '@/views/LoginView.vue'
import SetupView from '@/views/SetupView.vue'
import DashboardView from '@/views/DashboardView.vue'
import NotFoundView from '@/views/NotFoundView.vue'
import ConnectionErrorView from '@/views/ConnectionErrorView.vue'

export function createAppRouter(
  history: RouterHistory = createWebHistory(import.meta.env.BASE_URL),
) {
  const router = createRouter({
    history,
    routes: [
      { path: '/', redirect: { name: 'dashboard' } },
      { path: '/setup', name: 'setup', component: SetupView },
      { path: '/login', name: 'login', component: LoginView },
      { path: '/dashboard', name: 'dashboard', component: DashboardView },
      { path: '/unavailable', name: 'unavailable', component: ConnectionErrorView },
      { path: '/:pathMatch(.*)*', name: 'not-found', component: NotFoundView },
    ],
  })

  router.beforeEach(async (to) => {
    if (to.name === 'not-found' || to.name === 'unavailable') return true
    if (session.value.kind === 'unknown') await loadSession()

    let destination: string
    switch (session.value.kind) {
      case 'setup-required':
        destination = 'setup'
        break
      case 'signed-out':
        destination = 'login'
        break
      case 'signed-in':
        destination = 'dashboard'
        break
      default:
        destination = 'unavailable'
    }
    return to.name === destination ? true : { name: destination, replace: true }
  })

  return router
}
