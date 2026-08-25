<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import client from '@/api/client'
import PhoneFrame from '@/components/PhoneFrame.vue'
import { useUserStore } from '@/stores/user'

const LOCAL_PROFILE_KEY = 'pickle_profile_edit_overrides'
const DEFAULT_PROFILE_IMAGE = '/images/pickle-buddy-profile.png'
const skinToneOptions = ['봄 웜톤', '여름 쿨톤', '가을 웜톤', '겨울 쿨톤']
const genderOptions = [
  { label: '여성', value: 'female' },
  { label: '남성', value: 'male' },
]

const router = useRouter()
const route = useRoute()
const user = useUserStore()
const fileInput = ref(null)
const mode = ref('main')
const saving = ref(false)
const error = ref('')
const showPrivacySheet = ref(false)
const showImageSheet = ref(false)
const localProfile = ref(readLocalProfile(user.profile))
const modeTransitionName = ref('profile-mode-forward')
const drafts = ref({
  nickname: '',
  bio: '',
  height: '',
  weight: '',
  skinTone: '',
  gender: '',
})

const profile = computed(() => ({ ...(user.profile || {}), ...localProfile.value }))
const nickname = computed(() => profile.value.username || profile.value.nickname || '피클러')
const profileImage = computed(() => getProfileImage(profile.value) || DEFAULT_PROFILE_IMAGE)
const profileBio = computed(() => profile.value.bio || profile.value.introduction || '')
const profileHeight = computed(() => profile.value.height || profile.value.profile_height || '')
const profileWeight = computed(() => profile.value.weight || profile.value.profile_weight || '')
const profileSkinTone = computed(() => profile.value.skin_tone || profile.value.skinTone || profile.value.tone || '')
const profileGender = computed(() => normalizeGender(profile.value.gender || profile.value.gender_label || profile.value.genderLabel || ''))
const pageTitle = computed(() => {
  if (mode.value === 'nickname') return '닉네임 변경'
  if (mode.value === 'intro') return '소개글'
  if (mode.value === 'info') return '내 정보'
  if (mode.value === 'gender') return '성별'
  return '프로필 수정'
})
const canSaveNickname = computed(() => {
  const value = drafts.value.nickname.trim()
  return value.length > 0 && value !== nickname.value
})
const canSaveIntro = computed(() => drafts.value.bio.trim() !== profileBio.value)
const canSaveInfo = computed(() => {
  return (
    String(drafts.value.height || '') !== String(profileHeight.value || '') ||
    String(drafts.value.weight || '') !== String(profileWeight.value || '') ||
    drafts.value.skinTone !== profileSkinTone.value
  )
})
const canSaveGender = computed(() => Boolean(drafts.value.gender) && drafts.value.gender !== profileGender.value)
const avatarStyle = computed(() => (profileImage.value ? { backgroundImage: `url("${profileImage.value}")` } : {}))

function normalizeGender(value) {
  if (value === 'male' || value === '남성' || value === '남') return 'male'
  if (value === 'female' || value === '여성' || value === '여') return 'female'
  return ''
}

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

function persistLocalProfile(patch) {
  const key = profileStorageKey()
  if (!key) return
  localProfile.value = { ...localProfile.value, ...patch }
  localStorage.setItem(key, JSON.stringify(localProfile.value))
}

function getProfileImage(data = {}) {
  if (data.profile_image_removed) return ''
  return data.profile_image || data.profileImage || data.avatar_image || data.avatarPhoto || data.avatar || ''
}

function syncDrafts(data = profile.value) {
  drafts.value = {
    nickname: data.username || data.nickname || '',
    bio: data.bio || data.introduction || '',
    height: String(data.height || data.profile_height || ''),
    weight: String(data.weight || data.profile_weight || ''),
    skinTone: data.skin_tone || data.skinTone || data.tone || '',
    gender: normalizeGender(data.gender || data.gender_label || data.genderLabel || ''),
  }
}

function formatRequestError(requestError) {
  const data = requestError.response?.data
  if (!data) return '서버 연결을 확인해 주세요.'
  if (typeof data === 'string') return data
  if (data.detail) return data.detail

  return Object.entries(data)
    .flatMap(([field, messages]) => {
      const values = Array.isArray(messages) ? messages : [messages]
      return values.map((message) => `${field}: ${message}`)
    })
    .join('\n')
}

async function loadProfile() {
  try {
    const { data } = await client.get('/auth/profile/edit/')
    user.profile = { ...(user.profile || {}), ...data }
  } catch {
    if (!user.profile) await user.loadProfile()
  } finally {
    localProfile.value = readLocalProfile()
    syncDrafts()
  }
}

function goBack() {
  if (mode.value !== 'main') {
    modeTransitionName.value = 'profile-mode-back'
    mode.value = 'main'
    error.value = ''
    syncDrafts()
    return
  }
  exitProfileEdit()
}

function exitProfileEdit() {
  if (route.query.from === 'snap-profile' || route.query.returnTo === 'snap-profile') {
    router.replace({ name: 'lookbook', query: { profile: 'me', motion: 'back' } })
    return
  }
  router.back()
}

function openMode(nextMode) {
  modeTransitionName.value = 'profile-mode-forward'
  mode.value = nextMode
  error.value = ''
  syncDrafts()
}

function returnToMain() {
  modeTransitionName.value = 'profile-mode-back'
  mode.value = 'main'
}

async function savePatch(patch, fallbackPatch = patch) {
  saving.value = true
  error.value = ''
  persistLocalProfile(fallbackPatch)

  try {
    const data = await user.updateProfile(patch)
    user.profile = { ...(user.profile || {}), ...fallbackPatch, ...data }
  } catch (requestError) {
    user.profile = { ...(user.profile || {}), ...fallbackPatch }
    error.value = formatRequestError(requestError)
  } finally {
    saving.value = false
    syncDrafts()
  }
}

async function saveNickname() {
  if (!canSaveNickname.value) return
  const username = drafts.value.nickname.trim()
  await savePatch({ username }, { username, nickname: username })
  returnToMain()
}

async function saveIntro() {
  const bio = drafts.value.bio.trim()
  await savePatch({ bio, introduction: bio }, { bio, introduction: bio })
  returnToMain()
}

async function saveInfo() {
  const height = drafts.value.height.trim()
  const weight = drafts.value.weight.trim()
  const skinTone = drafts.value.skinTone
  await savePatch(
    { height, weight, skin_tone: skinTone },
    { height, weight, skin_tone: skinTone, skinTone, tone: skinTone },
  )
  returnToMain()
}

async function saveGender() {
  if (!canSaveGender.value) return
  const gender = drafts.value.gender
  const genderLabel = genderOptions.find((item) => item.value === gender)?.label || ''
  await savePatch(
    { gender },
    { gender, gender_label: genderLabel, genderLabel },
  )
  returnToMain()
}

function triggerImageUpload() {
  fileInput.value?.click()
}

function openImageSheet() {
  showImageSheet.value = true
}

function closeImageSheet() {
  showImageSheet.value = false
}

function selectProfileImage() {
  showImageSheet.value = false
  triggerImageUpload()
}

async function resetProfileImage() {
  const fallbackPatch = {
    profile_image_removed: true,
    profile_image: '',
    profileImage: '',
    avatar_image: '',
    avatarPhoto: '',
    avatar: '',
  }
  showImageSheet.value = false
  await savePatch({ profile_image: null }, fallbackPatch)
  persistLocalProfile(fallbackPatch)
  user.profile = { ...(user.profile || {}), ...fallbackPatch }
}

function changeProfileImage(event) {
  const [file] = event.target.files || []
  if (!file) return

  const reader = new FileReader()
  reader.onload = async () => {
    const profileImageData = reader.result
    await savePatch(
      { profile_image: profileImageData },
      {
        profile_image_removed: false,
        profile_image: profileImageData,
        profileImage: profileImageData,
        avatar_image: profileImageData,
        avatarPhoto: profileImageData,
        avatar: profileImageData,
      },
    )
  }
  reader.readAsDataURL(file)
  event.target.value = ''
}

onMounted(loadProfile)
</script>

<template>
  <PhoneFrame>
    <section class="profile-edit-page">
      <header class="profile-edit-top">
        <button type="button" aria-label="뒤로 가기" @click="goBack">
          <span class="ui-icon" style="--icon: url('/icons/ui/angle-small-left.svg')" aria-hidden="true"></span>
        </button>
        <h1>{{ pageTitle }}</h1>
        <span></span>
      </header>

      <Transition :name="modeTransitionName" mode="out-in">
      <section v-if="mode === 'main'" key="main" class="profile-edit-content profile-edit-main">
        <input ref="fileInput" class="visually-hidden" type="file" accept="image/*" @change="changeProfileImage" />
        <section class="profile-edit-identity">
          <span class="profile-edit-avatar" :class="{ 'has-image': profileImage }" :style="avatarStyle"></span>
          <strong>{{ nickname }}</strong>
          <button type="button" @click="openImageSheet">이미지 변경</button>
        </section>

        <nav class="profile-edit-menu" aria-label="프로필 수정 메뉴">
          <button type="button" @click="openMode('nickname')">
            <span>닉네임</span>
            <i aria-hidden="true">›</i>
          </button>
          <button type="button" @click="openMode('intro')">
            <span>소개글</span>
            <i aria-hidden="true">›</i>
          </button>
          <button type="button" @click="openMode('info')">
            <span>내 정보</span>
            <i aria-hidden="true">›</i>
          </button>
          <button type="button" @click="openMode('gender')">
            <span>성별</span>
            <i aria-hidden="true">›</i>
          </button>
        </nav>

        <button class="profile-edit-privacy-link" type="button" @click="showPrivacySheet = true">
          개인정보 수집 및 이용 안내
        </button>
        <p v-if="error" class="profile-edit-error">{{ error }}</p>
      </section>

      <form v-else-if="mode === 'nickname'" key="nickname" class="profile-edit-content profile-edit-form-page" @submit.prevent="saveNickname">
        <label>
          <strong>닉네임</strong>
          <input v-model.trim="drafts.nickname" maxlength="12" placeholder="닉네임 입력" autocomplete="nickname" />
        </label>
        <p>현재 닉네임: {{ nickname }}</p>
        <p v-if="error" class="profile-edit-error">{{ error }}</p>
        <button class="profile-edit-save" type="submit" :disabled="saving || !canSaveNickname">
          {{ saving ? '저장 중...' : '저장하기' }}
        </button>
      </form>

      <form v-else-if="mode === 'intro'" key="intro" class="profile-edit-content profile-edit-form-page" @submit.prevent="saveIntro">
        <label>
          <strong>소개글</strong>
          <textarea v-model="drafts.bio" maxlength="150" placeholder="소개글을 입력해주세요"></textarea>
        </label>
        <div class="profile-edit-count"><span>150자 이내</span><span>{{ drafts.bio.length }}/150</span></div>
        <p v-if="error" class="profile-edit-error">{{ error }}</p>
        <button class="profile-edit-save" type="submit" :disabled="saving || !canSaveIntro">
          {{ saving ? '저장 중...' : '저장하기' }}
        </button>
      </form>

      <form v-else-if="mode === 'info'" key="info" class="profile-edit-content profile-edit-form-page profile-edit-info" @submit.prevent="saveInfo">
        <section>
          <h2>체형 정보</h2>
          <label>
            <strong>키</strong>
            <span><input v-model.trim="drafts.height" inputmode="numeric" placeholder="키" /><b>cm</b></span>
          </label>
          <label>
            <strong>몸무게</strong>
            <span><input v-model.trim="drafts.weight" inputmode="numeric" placeholder="몸무게" /><b>kg</b></span>
          </label>
        </section>
        <section>
          <h2>피부 정보</h2>
          <strong class="profile-edit-subtitle">피부 톤</strong>
          <div class="profile-edit-tone-options">
            <button
              v-for="tone in skinToneOptions"
              :key="tone"
              type="button"
              :class="{ active: drafts.skinTone === tone }"
              @click="drafts.skinTone = drafts.skinTone === tone ? '' : tone"
            >
              {{ tone }}
            </button>
          </div>
        </section>
        <p v-if="error" class="profile-edit-error">{{ error }}</p>
        <button class="profile-edit-save" type="submit" :disabled="saving || !canSaveInfo">
          {{ saving ? '저장 중...' : '저장하기' }}
        </button>
      </form>

      <form v-else key="gender" class="profile-edit-content profile-edit-form-page profile-edit-info" @submit.prevent="saveGender">
        <section>
          <h2>성별</h2>
          <div class="profile-edit-tone-options">
            <button
              v-for="gender in genderOptions"
              :key="gender.value"
              type="button"
              :class="{ active: drafts.gender === gender.value }"
              @click="drafts.gender = gender.value"
            >
              {{ gender.label }}
            </button>
          </div>
        </section>
        <p v-if="error" class="profile-edit-error">{{ error }}</p>
        <button class="profile-edit-save" type="submit" :disabled="saving || !canSaveGender">
          {{ saving ? '저장 중...' : '저장하기' }}
        </button>
      </form>
      </Transition>

      <div v-if="showPrivacySheet" class="profile-edit-overlay" @click.self="showPrivacySheet = false">
        <aside class="profile-edit-sheet" role="dialog" aria-modal="true" aria-labelledby="privacy-title">
          <header>
            <h2 id="privacy-title">개인정보 수집 및 이용 안내</h2>
            <button type="button" aria-label="닫기" @click="showPrivacySheet = false">
              <span class="ui-icon" style="--icon: url('/icons/ui/cross.svg')" aria-hidden="true"></span>
            </button>
          </header>
          <p>
            주식회사 피클은 회원의 개인정보 및 권리 보호를 위해 개인정보 보호법 및 관계 법령이 정한
            바를 준수하여 안전하게 관리하고 있습니다. 자세한 사항은 개인정보처리방침에서 확인할 수
            있습니다.
          </p>
          <button type="button" @click="showPrivacySheet = false">확인</button>
        </aside>
      </div>

      <div v-if="showImageSheet" class="profile-edit-overlay" @click.self="closeImageSheet">
        <aside class="profile-edit-sheet profile-image-sheet" role="dialog" aria-modal="true" aria-labelledby="profile-image-title">
          <header>
            <h2 id="profile-image-title">프로필 이미지</h2>
            <button type="button" aria-label="닫기" @click="closeImageSheet">
              <span class="ui-icon" style="--icon: url('/icons/ui/cross.svg')" aria-hidden="true"></span>
            </button>
          </header>
          <div class="profile-image-actions">
            <button type="button" @click="selectProfileImage">이미지 변경</button>
            <button type="button" @click="resetProfileImage">기본 프로필 설정</button>
          </div>
        </aside>
      </div>
    </section>
  </PhoneFrame>
</template>
