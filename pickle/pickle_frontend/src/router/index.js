import { createRouter, createWebHistory } from 'vue-router'

import ClosetStartView from '@/views/ClosetStartView.vue'
import ClosetView from '@/views/ClosetView.vue'
import ClothesNewView from '@/views/ClothesNewView.vue'
import ClothesDetailView from '@/views/ClothesDetailView.vue'
import ClothesEditView from '@/views/ClothesEditView.vue'
import CustomerCenterView from '@/views/CustomerCenterView.vue'
import EvaluationView from '@/views/EvaluationView.vue'
import HomeView from '@/views/HomeView.vue'
import KakaoCallbackView from '@/views/KakaoCallbackView.vue'
import LookbookView from '@/views/LookbookView.vue'
import LoginView from '@/views/LoginView.vue'
import MyView from '@/views/MyView.vue'
import AccountEditView from '@/views/AccountEditView.vue'
import NotificationSettingsView from '@/views/NotificationSettingsView.vue'
import OnboardingView from '@/views/OnboardingView.vue'
import PlannerView from '@/views/PlannerView.vue'
import ProfileEditView from '@/views/ProfileEditView.vue'
import RecommendResultView from '@/views/RecommendResultView.vue'
import RecommendView from '@/views/RecommendView.vue'
import SavedOutfitsView from '@/views/SavedOutfitsView.vue'
import SettingsView from '@/views/SettingsView.vue'
import StartView from '@/views/StartView.vue'
import SignupEmailView from '@/views/SignupEmailView.vue'
import SignupStartView from '@/views/SignupStartView.vue'
import CommunityView from '@/views/CommunityView.vue'
import StylePreferencesView from '@/views/StylePreferencesView.vue'
import { useUserStore } from '@/stores/user'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'start', component: StartView },
    { path: '/start', redirect: '/' },
    { path: '/login', name: 'login', component: LoginView },
    { path: '/signup', name: 'signup-start', component: SignupStartView },
    { path: '/signup/email', name: 'signup-email', component: SignupEmailView },
    { path: '/oauth/kakao/callback', name: 'kakao-callback', component: KakaoCallbackView },
    { path: '/signup/kakao/callback', name: 'signup-kakao-callback', redirect: (to) => ({ path: '/oauth/kakao/callback', query: to.query }) },
    { path: '/onboarding', name: 'onboarding', component: OnboardingView },
    { path: '/closet-start', name: 'closet-start', component: ClosetStartView },
    { path: '/closet', name: 'closet', component: ClosetView },
    { path: '/clothes/new', name: 'clothes-new', component: ClothesNewView },
    { path: '/clothes/:id', name: 'clothes-detail', component: ClothesDetailView },
    { path: '/clothes/:id/edit', name: 'clothes-edit', component: ClothesEditView },
    { path: '/home', name: 'home', component: HomeView },
    { path: '/recommend', name: 'recommend', component: RecommendView },
    { path: '/recommend/result', name: 'recommend-result', component: RecommendResultView },
    { path: '/my/outfits', name: 'my-outfits', component: SavedOutfitsView },
    { path: '/evaluation', name: 'evaluation', component: EvaluationView },
    { path: '/planner', name: 'planner', component: PlannerView },
    { path: '/lookbook', name: 'lookbook', component: LookbookView },
    { path: '/my', name: 'my', component: MyView },
    { path: '/my/account', name: 'my-account', component: AccountEditView },
    { path: '/my/profile', name: 'my-profile', component: ProfileEditView },
    { path: '/my/styles', name: 'my-styles', component: StylePreferencesView },
    { path: '/my/settings', name: 'my-settings', component: SettingsView },
    { path: '/my/notifications', name: 'my-notifications', component: NotificationSettingsView },
    { path: '/my/customer-center', name: 'my-customer-center', component: CustomerCenterView },
    { path: '/community', name: 'community', component: CommunityView },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})

router.beforeEach((to) => {
  const user = useUserStore()
  const authenticated = Boolean(localStorage.getItem('pickle_access_token'))
  const authCallbackRoutes = new Set(['kakao-callback', 'signup-kakao-callback'])
  const publicRoutes = new Set(['start', 'login', 'signup-start', 'signup-email', 'kakao-callback', 'signup-kakao-callback'])
  if (authCallbackRoutes.has(to.name)) return true
  if (!authenticated && !publicRoutes.has(to.name)) return { name: 'start' }
  if (authenticated && !user.profile) {
    user.hydrateProfile().catch(() => {})
  }
  if (authenticated && publicRoutes.has(to.name)) return { name: 'home' }
  return true
})

export default router
