<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import PhoneFrame from '@/components/PhoneFrame.vue'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const user = useUserStore()
const LOCAL_PROFILE_KEY = 'pickle_profile_edit_overrides'
const DEFAULT_PROFILE_IMAGE = '/images/pickle-buddy-profile.png'
const localProfile = ref(readLocalProfile(user.profile))

const displayProfile = computed(() => ({ ...(user.profile || {}), ...localProfile.value }))
const profileImage = computed(() => getProfileImage(displayProfile.value) || DEFAULT_PROFILE_IMAGE)
const profileAvatarStyle = computed(() => ({ backgroundImage: `url("${profileImage.value}")` }))
const menuItems = [
  { label: '내 옷장', route: '/closet', icon: 'wardrobe', tone: 'green' },
  { label: '내 코디', route: '/my/outfits', icon: 'clothes-hanger', tone: 'blue' },
  { label: '알림 설정', route: '/my/notifications', icon: 'bell', tone: 'orange' },
  { label: '고객센터', route: '/my/customer-center', icon: 'headset', tone: 'gray' },
]

function profileStorageKey(profileData = user.profile) {
  const ownerKey = profileData?.id || profileData?.email || profileData?.username || profileData?.nickname
  return ownerKey ? `${LOCAL_PROFILE_KEY}:${ownerKey}` : ''
}

function readLocalProfile(profileData = user.profile) {
  const key = profileStorageKey(profileData)
  if (!key) return {}
  try {
    return JSON.parse(localStorage.getItem(key) || '{}')
  } catch {
    return {}
  }
}

function getProfileImage(data = {}) {
  if (data.profile_image_removed) return ''
  return data.profile_image || data.profileImage || data.avatar_image || data.avatarPhoto || data.avatar || ''
}

onMounted(async () => {
  if (!user.profile) await user.loadProfile()
  localProfile.value = readLocalProfile()
})

function logout() {
  user.logout()
  router.push('/')
}
</script>

<template>
  <PhoneFrame show-nav>
    <template #title>
      <img class="header-title-logo header-title-logo--my" src="/images/MY_logo.png" alt="마이" />
    </template>

    <template #action>
      <button class="plain-action icon-action my-settings-button" type="button" aria-label="설정" @click="$router.push('/my/settings')">
        <span class="ui-icon" style="--icon: url('/icons/ui/settings.svg')" aria-hidden="true"></span>
      </button>
    </template>

    <section class="my-profile-card">
      <div class="profile-avatar my-profile-avatar" :style="profileAvatarStyle"></div>
      <div class="my-profile-copy">
        <h2>{{ displayProfile.username || displayProfile.nickname || '지우' }}</h2>
        <p>아이디</p>
      </div>
      <div class="my-profile-actions">
        <button class="my-edit-button" type="button" aria-label="프로필 수정" @click="$router.push('/my/profile')">
          <span class="ui-icon" style="--icon: url('/icons/ui/profile_edit.svg')" aria-hidden="true"></span>
        </button>
        <button class="my-edit-button" type="button" aria-label="회원정보 수정" @click="$router.push('/my/account')">
          <span class="edit-pencil-icon" aria-hidden="true"></span>
        </button>
      </div>
    </section>

    <section class="my-menu-card" aria-label="마이 메뉴">
      <button v-for="item in menuItems" :key="item.label" type="button" @click="$router.push(item.route)">
        <span class="my-menu-icon" :class="`my-menu-icon--${item.tone}`">
          <span v-if="item.icon === 'headset'" class="headset-icon" aria-hidden="true"></span>
          <span v-else class="ui-icon" :style="{ '--icon': `url('/icons/ui/${item.icon}.svg')` }" aria-hidden="true"></span>
        </span>
        <strong>{{ item.label }}</strong>
        <span class="ui-icon my-menu-arrow" style="--icon: url('/icons/ui/angle-small-right.svg')" aria-hidden="true"></span>
      </button>
    </section>

    <button class="text-button wide" type="button" @click="logout">로그아웃</button>
  </PhoneFrame>
</template>
