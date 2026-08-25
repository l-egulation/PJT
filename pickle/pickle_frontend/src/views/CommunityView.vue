<script setup>
import { onMounted, ref } from 'vue'
import client from '@/api/client'
import PhoneFrame from '@/components/PhoneFrame.vue'
import { useUserStore } from '@/stores/user'

const user = useUserStore()
const posts = ref([])
const title = ref('')
const body = ref('')
const tpo = ref('daily')
const suggestion = ref({})
const loading = ref(false)
const error = ref('')

async function load() {
  const { data } = await client.get('/style-requests/')
  posts.value = data.results || data
}
async function createPost() {
  error.value = ''
  try {
    await client.post('/style-requests/', { title: title.value, body: body.value, tpo: tpo.value })
    title.value = ''
    body.value = ''
    await load()
  } catch {
    error.value = '제목과 내용을 확인해 주세요.'
  }
}
async function suggest(post) {
  const comment = suggestion.value[post.id]
  if (!comment) return
  loading.value = true
  try {
    await client.post(`/style-requests/${post.id}/suggest/`, { title: '스타일 제안', comment })
    suggestion.value[post.id] = ''
    await load()
  } finally {
    loading.value = false
  }
}
async function accept(post, item) {
  await client.post(`/style-requests/${post.id}/accept-suggestion/`, { suggestion_id: item.id })
  await load()
}
onMounted(async () => {
  if (!user.profile) await user.loadProfile()
  await load()
})
</script>

<template>
  <PhoneFrame title="스타일 커뮤니티" show-back show-nav>
    <form class="modern-form community-form" @submit.prevent="createPost">
      <h2>코디 도움 요청</h2><input v-model="title" placeholder="제목" required />
      <textarea v-model="body" placeholder="고민과 원하는 상황을 적어주세요." required></textarea>
      <select v-model="tpo"><option value="daily">일상</option><option value="work">출근</option><option value="date">데이트</option><option value="formal">격식</option></select>
      <p v-if="error" class="error-message">{{ error }}</p><button class="primary-button">등록</button>
    </form>
    <section class="community-list">
      <article v-for="post in posts" :key="post.id" class="reason-card">
        <small>{{ post.requester_username }} / {{ post.tpo }} / {{ post.status }}</small><h2>{{ post.title }}</h2><p>{{ post.body }}</p>
        <div v-for="item in post.suggestions" :key="item.id" class="suggestion">
          <strong>{{ item.stylist_username }}</strong>: {{ item.comment }}
          <button v-if="post.requester === user.profile?.id && post.status === 'open'" class="text-button" @click="accept(post, item)">채택</button>
        </div>
        <form v-if="post.status === 'open'" class="suggest-row" @submit.prevent="suggest(post)"><input v-model="suggestion[post.id]" placeholder="내 코디 의견" /><button class="secondary-button" :disabled="loading">제안</button></form>
      </article>
    </section>
  </PhoneFrame>
</template>
