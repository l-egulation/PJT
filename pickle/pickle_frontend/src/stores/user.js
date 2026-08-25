import { defineStore } from 'pinia'
import client from '@/api/client'

export const useUserStore = defineStore('user', {
  state: () => ({
    profile: null,
    styleTags: [],
    settings: null,
    profileLoading: false,
    profileLoadPromise: null,
  }),
  getters: {
    isAuthenticated: () => Boolean(localStorage.getItem('pickle_access_token')),
    availableTags: (state) => state.styleTags,
  },
  actions: {
    async login(email, password) {
      const { data } = await client.post('/auth/login/', { email, password })
      localStorage.setItem('pickle_access_token', data.access)
      localStorage.setItem('pickle_refresh_token', data.refresh)
      await this.loadProfile()
    },
    async signup(payload) {
      const { data } = await client.post('/auth/signup/', payload)
      localStorage.setItem('pickle_access_token', data.tokens.access)
      localStorage.setItem('pickle_refresh_token', data.tokens.refresh)
      this.profile = data.user
    },
    async kakaoLogin(code, redirectUri) {
      const { data } = await client.post('/auth/kakao/', {
        code,
        redirect_uri: redirectUri,
      })
      localStorage.setItem('pickle_access_token', data.tokens.access)
      localStorage.setItem('pickle_refresh_token', data.tokens.refresh)
      this.profile = data.user
    },
    async loadProfile() {
      const { data } = await client.get('/auth/profile/')
      this.profile = data
      return data
    },
    async hydrateProfile() {
      if (!this.isAuthenticated || this.profile) return this.profile
      if (this.profileLoadPromise) return this.profileLoadPromise

      this.profileLoading = true
      this.profileLoadPromise = this.loadProfile()
        .catch((error) => {
          if (error.response?.status === 401) this.logout()
          throw error
        })
        .finally(() => {
          this.profileLoading = false
          this.profileLoadPromise = null
        })
      return this.profileLoadPromise
    },
    async loadStyles() {
      const { data } = await client.get('/styles/')
      this.styleTags = data.results || data
    },
    async updateStyles(styleTagIds) {
      const { data } = await client.patch('/auth/profile/', { style_tag_ids: styleTagIds })
      this.profile = data
    },
    async updateProfile(payload) {
      const { data } = await client.patch('/auth/profile/edit/', payload)
      this.profile = { ...this.profile, ...data }
      return data
    },
    async updateStylePreferences(styleTagIds) {
      const { data } = await client.patch('/auth/style-preferences/', { style_tag_ids: styleTagIds })
      this.profile = { ...this.profile, style_tags: data.style_tags }
      return data
    },
    async loadSettings() {
      const { data } = await client.get('/auth/settings/')
      this.settings = data
      return data
    },
    async updateSettings(payload) {
      const { data } = await client.patch('/auth/settings/', payload)
      this.settings = data
      return data
    },
    async deleteAccount() {
      await client.delete('/auth/account/')
      this.logout()
    },
    logout() {
      localStorage.removeItem('pickle_access_token')
      localStorage.removeItem('pickle_refresh_token')
      this.profile = null
      this.settings = null
    },
  },
})
