<script setup>
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import client from '@/api/client'
import PhoneFrame from '@/components/PhoneFrame.vue'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const user = useUserStore()
const email = ref('')
const password = ref('')
const username = ref('')
const serviceAgreed = ref(false)
const privacyAgreed = ref(false)
const marketingAgreed = ref(false)
const nicknameChecked = ref(false)
const checkingNickname = ref(false)
const loading = ref(false)
const submitAttempted = ref(false)
const error = ref('')
const notice = ref('')

const emailValid = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value))
const passwordValid = computed(() => password.value.length >= 8)
const usernameValid = computed(() => username.value.trim().length >= 2)
const requiredTermsAgreed = computed(() => serviceAgreed.value && privacyAgreed.value)
const agreedAll = computed(() => requiredTermsAgreed.value && marketingAgreed.value)
const canSubmit = computed(() => (
  emailValid.value
  && passwordValid.value
  && usernameValid.value
  && nicknameChecked.value
  && requiredTermsAgreed.value
  && !loading.value
))
const blockedSubmitMessage = computed(() => {
  if (canSubmit.value) return ''
  if (!emailValid.value) return '올바른 이메일 주소를 입력해주세요.'
  if (!passwordValid.value) return '비밀번호는 8자 이상 입력해주세요.'
  if (!usernameValid.value) return '닉네임을 2자 이상 입력해주세요.'
  if (!nicknameChecked.value) return '닉네임 중복확인을 완료해주세요.'
  if (!requiredTermsAgreed.value) return '필수 약관에 동의해주세요.'
  return ''
})

const errorLabels = {
  username: '닉네임',
  email: '이메일',
  password: '비밀번호',
  non_field_errors: '가입',
}

const errorTranslations = {
  'A user with that username already exists.': '이미 사용 중인 닉네임입니다.',
  'This email is already in use.': '이미 사용 중인 이메일입니다.',
}

watch(username, () => {
  nicknameChecked.value = false
  notice.value = ''
})

function formatRequestError(requestError) {
  const data = requestError.response?.data
  if (!data) return '서버 연결을 확인해 주세요.'
  if (typeof data === 'string') return data
  if (data.detail) return errorTranslations[data.detail] || data.detail

  return Object.entries(data)
    .flatMap(([field, messages]) => {
      const values = Array.isArray(messages) ? messages : [messages]
      return values.map((message) => `${errorLabels[field] || field}: ${errorTranslations[message] || message}`)
    })
    .join('\n')
}

function toggleAll() {
  const next = !agreedAll.value
  serviceAgreed.value = next
  privacyAgreed.value = next
  marketingAgreed.value = next
}

async function checkNickname() {
  const value = username.value.trim()
  error.value = ''
  notice.value = ''
  nicknameChecked.value = false

  if (value.length < 2) {
    error.value = '닉네임을 2자 이상 입력해 주세요.'
    return
  }

  checkingNickname.value = true
  try {
    const { data } = await client.get('/auth/check-username/', {
      params: { username: value },
    })
    if (!data.available) {
      error.value = '이미 사용 중인 닉네임입니다.'
      return
    }
    nicknameChecked.value = true
    notice.value = '사용 가능한 닉네임입니다.'
  } catch (requestError) {
    error.value = requestError.response?.data?.detail || '닉네임 중복 확인에 실패했습니다.'
  } finally {
    checkingNickname.value = false
  }
}

async function submit() {
  submitAttempted.value = true
  if (!canSubmit.value) {
    error.value = blockedSubmitMessage.value
    return
  }
  loading.value = true
  error.value = ''

  try {
    await user.signup({
      username: username.value.trim(),
      email: email.value.trim(),
      password: password.value,
    })
    router.push('/onboarding')
  } catch (requestError) {
    error.value = formatRequestError(requestError)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <PhoneFrame show-back>
    <form class="signup-form-page" @submit.prevent="submit">
      <span class="signup-step-count">1 / 4</span>
      <header class="signup-form-heading">
        <h1>계정 정보를<br />입력해 주세요</h1>
        <p>피클 시작에 필요한 기본 정보예요.</p>
      </header>

      <label class="signup-field">
        이메일
        <span class="signup-input-wrap">
          <input v-model.trim="email" type="email" autocomplete="email" placeholder="jiwoo.kim@gmail.com" required />
          <i v-if="emailValid" class="signup-check" aria-hidden="true">✓</i>
        </span>
      </label>

      <label class="signup-field">
        비밀번호
        <span class="signup-input-wrap">
          <input v-model="password" type="password" minlength="8" autocomplete="new-password" placeholder="••••••••" required />
        </span>
        <small>영문, 숫자 포함 8자 이상</small>
      </label>

      <label class="signup-field">
        닉네임
        <span class="signup-nickname-row">
          <input v-model.trim="username" autocomplete="username" placeholder="지우" required />
          <button type="button" :disabled="checkingNickname" @click="checkNickname">
            {{ checkingNickname ? '확인중' : nicknameChecked ? '확인완료' : '중복확인' }}
          </button>
        </span>
      </label>

      <section class="signup-terms-card">
        <button class="signup-all-agree" type="button" @click="toggleAll">
          <span class="signup-checkbox" :class="{ active: agreedAll }">✓</span>
          약관 전체 동의
        </button>
        <div class="signup-term-row" role="button" tabindex="0" @click="serviceAgreed = !serviceAgreed" @keydown.enter.prevent="serviceAgreed = !serviceAgreed">
          <span class="signup-term-check" :class="{ active: serviceAgreed }">✓</span>
          <strong>(필수) 서비스 이용약관</strong>
          <button type="button" @click.stop>보기</button>
        </div>
        <div class="signup-term-row" role="button" tabindex="0" @click="privacyAgreed = !privacyAgreed" @keydown.enter.prevent="privacyAgreed = !privacyAgreed">
          <span class="signup-term-check" :class="{ active: privacyAgreed }">✓</span>
          <strong>(필수) 개인정보 수집 및 이용</strong>
          <button type="button" @click.stop>보기</button>
        </div>
        <div class="signup-term-row" role="button" tabindex="0" @click="marketingAgreed = !marketingAgreed" @keydown.enter.prevent="marketingAgreed = !marketingAgreed">
          <span class="signup-term-check" :class="{ active: marketingAgreed }">✓</span>
          <strong>(선택) 마케팅 정보 수신 동의</strong>
          <button type="button" @click.stop>보기</button>
        </div>
      </section>

      <p v-if="notice" class="success-message">{{ notice }}</p>
      <p v-if="!error && submitAttempted && blockedSubmitMessage" class="error-message">{{ blockedSubmitMessage }}</p>
      <p v-if="error" class="error-message">{{ error }}</p>
      <button class="primary-button signup-next-button" type="submit" :disabled="loading">
        {{ loading ? '가입 중...' : '다음' }}
      </button>
    </form>
  </PhoneFrame>
</template>
