<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import client from '@/api/client'
import PhoneFrame from '@/components/PhoneFrame.vue'
import { useUserStore } from '@/stores/user'
import { colorHex } from '@/utils/colors'

const PROFILE_EDIT_LOCAL_KEY = 'pickle_profile_edit_overrides'
const PROFILE_SNAPSHOT_LOCAL_KEY = 'pickle_snap_profile_snapshot'
const DEFAULT_PROFILE_IMAGE = '/images/pickle-buddy-profile.png'

const router = useRouter()
const route = useRoute()
const userStore = useUserStore()
const initialProfileRows = readProfileSnapshotRows()
const activeTab = ref('snap')
const rankingScope = ref('snap')
const selectedGender = ref('전체')
const selectedSnap = ref(null)
const selectedSnapId = ref(null)
const selectedProfileUser = ref(
  route.query.profile === 'me'
    ? (userStore.profile?.username || Object.keys(initialProfileRows)[0] || '나')
    : null,
)
const selectedViewerSnap = ref(null)
const detailSourceSnaps = ref(null)
const detailReturnProfileUser = ref(null)
const showComments = ref(false)
const showFilterSheet = ref(false)
const showMoreSheet = ref(false)
const showProfileMoreSheet = ref(false)
const showPhotoViewer = ref(false)
const showPhotoViewerCount = ref(true)
const showSnapForm = ref(false)
const showClosetPicker = ref(false)
const showPictureEditor = ref(false)
const showSnapGuide = ref(false)
const showSnapBackWarning = ref(false)
const showSnapDeleteWarning = ref(false)
const showSnapPhotoWarning = ref(false)
const showFollowList = ref(false)
const followListTab = ref('followers')
const followListUser = ref('')
const followListRows = ref([])
const followListLoading = ref(false)
const snapFormWarning = ref({ title: '사진을 첨부해 주세요.', body: '스냅 등록을 위해서는 최소 1장의 사진이 필요합니다.' })
const profileBio = ref(localStorage.getItem('pickle_snap_bio') || '')
const profileEditOverrides = ref(readProfileEditOverrides())
const editingProfileBio = ref(false)
const profileBioDraft = ref('')
const showSnapBodySheet = ref(false)
const showSnapToneSheet = ref(false)
const showSortMenu = ref(false)
const showRankingPeriodMenu = ref(false)
const activeFilter = ref('gender')
const selectedSeasons = ref([])
const selectedStyles = ref([])
const selectedTpos = ref([])
const selectedClosetCategory = ref('all')
const closetClothes = ref([])
const closetLoading = ref(false)
const closetLoaded = ref(false)
const heightRange = ref({ min: '', max: '' })
const weightRange = ref({ min: '', max: '' })
const sortBy = ref('popular')
const rankingPeriod = ref('day')
const photoIndex = ref(0)
const photoIndexes = ref({})
const photoSwipe = ref({ x: 0, moved: false, snap: null })
const photoWheelLocked = ref(false)
const productDrag = ref({ x: 0, left: 0, target: null, moved: false })
const transitionName = ref(route.query.motion === 'back' ? 'slide-back' : 'slide-forward')
const detailPage = ref(null)
const apiLoaded = ref(false)
const apiLoading = ref(true)
const snapLoadError = ref('')
const apiSnaps = ref([])
const apiMembers = ref([])
const profileRows = ref(initialProfileRows)
const snapPhotoUploading = ref(false)
const commentRows = ref({})
const commentDraft = ref('')
const selectedComment = ref(null)
const editingCommentId = ref(null)
const editingCommentBody = ref('')
const showCommentMoreSheet = ref(false)
const commentsLoading = ref(false)
const liked = ref(new Set())
const scrapped = ref(new Set())
const following = ref(new Set())
const MY_USER = computed(() => userStore.profile?.username || Object.keys(initialProfileRows)[0] || '나')
const mySnaps = ref([])
const editingSnapId = ref(null)
const editingPhotoIndex = ref(null)
const snapForm = ref({
  photos: [],
  body: '',
  clothes: [],
  height: '',
  weight: '',
  tone: '',
  gender: '',
  seasons: [],
  styles: [],
  tpos: [],
})
const draftPhotoClass = ref('cafe')
const draftPhotoZoom = ref(1)
const photoDimensions = ref({})
const cropDrag = ref({ x: 0, y: 0, startX: 0, startY: 0, active: false })
const cropPinch = ref({ distance: 0, zoom: 1 })
const cropPointers = new Map()
const photoDimensionPromises = new Map()
const PHOTO_EDIT_SEPARATOR = '||edit:'
const CROP_FRAME = { width: 400, height: 378 }

const tabs = [
  { key: 'snap', label: '스냅' },
  { key: 'ranking', label: '랭킹' },
  { key: 'following', label: '팔로잉' },
  { key: 'scrap', label: '스크랩' },
]
const sortOptions = [
  { key: 'popular', label: '인기순' },
  { key: 'recommend', label: '추천순' },
  { key: 'latest', label: '최신순' },
]
const rankingPeriodOptions = [
  { key: 'day', label: '최근 1일' },
  { key: 'week', label: '최근 1주일' },
  { key: 'month', label: '최근 1개월' },
]
const styles = ['전체', '캐주얼', '스트릿', '미니멀', '걸리시', '로맨틱', '시크']
const tpos = ['데일리', '출근', '데이트', '격식', '여행', '운동']
const seasons = ['봄', '여름', '가을', '겨울']
const skinTones = ['봄 웜톤', '여름 쿨톤', '가을 웜톤', '겨울 쿨톤']
const createTags = ['# 오늘의스냅', '# 꾸안꾸', '# 출근룩', '# 체형커버코디', '# 여행룩']
const createPhotoOptions = ['cafe', 'studio', 'street', 'window', 'campus', 'brand']
const closetCategories = [{ key: 'all', label: '전체' }, { key: 'top', label: '상의' }, { key: 'bottom', label: '하의' }, { key: 'outer', label: '아우터' }, { key: 'dress', label: '원피스' }, { key: 'shoes', label: '신발' }, { key: 'bag', label: '가방' }, { key: 'accessory', label: '액세서리' }]
const closetCategoryLabels = Object.fromEntries(closetCategories.map((item) => [item.key, item.label]))
const closetItems = [
  { id: 'blue-shirt', category: '상의', categoryKey: 'top', name: '블루 샴브레이 셔츠', tone: '#8fb1c1' },
  { id: 'white-tee', category: '상의', categoryKey: 'top', name: '릴랙스 화이트 티셔츠', tone: '#f7f2e8' },
  { id: 'olive-shorts', category: '하의', categoryKey: 'bottom', name: '올리브 카고 쇼츠', tone: '#8f8d78' },
  { id: 'denim-pants', category: '하의', categoryKey: 'bottom', name: '라이트 데님 팬츠', tone: '#c7dce6' },
  { id: 'daily-bag', category: '가방', categoryKey: 'bag', name: '데일리 백팩', tone: '#1f2327' },
  { id: 'white-sneakers', category: '신발', categoryKey: 'shoes', name: '화이트 스니커즈', tone: '#f4f4ed' },
]
const filterTabs = [
  { key: 'gender', label: '성별' },
  { key: 'season', label: '계절' },
  { key: 'style', label: '스타일' },
  { key: 'body', label: '키/몸무게' },
  { key: 'tpo', label: 'TPO' },
]
const emptySnap = {
  id: 0,
  user: '',
  avatar: '#eee8df',
  image: 'cafe',
  photos: ['cafe'],
  gender: '전체',
  title: '',
  body: '',
  tags: [],
  meta: '',
  daysAgo: 0,
  likes: 0,
  comments: 0,
  clothes: [],
  comment: { author: '', body: '', time: '' },
}

const snaps = [
  {
    id: 1,
    user: '요니찡3',
    avatar: '#d8e8ee',
    image: 'cafe',
    photos: ['cafe', 'window', 'campus'],
    gender: '남성',
    title: '깔끔한 여름코디',
    body: '여름에도 단정하게 입고 싶은 날 고른 조합이에요.',
    tags: ['꾸안꾸', '샴브레이셔츠', '남자셔츠', '데일리룩', '위크웨어'],
    meta: '183cm/77kg · 가을 웜톤',
    daysAgo: 2,
    likes: 1784,
    comments: 1,
    clothes: [
      { category: '상의', name: '샴브레이 반팔 셔츠', tone: '#8fb1c1' },
      { category: '하의', name: '카고 쇼츠', tone: '#8f8d78' },
      { category: '신발', name: '화이트 스니커즈', tone: '#f4f4ed' },
    ],
    comment: { author: '상쾌한마젠타양복', body: '굿', time: '16시간 전' },
  },
  {
    id: 2,
    user: '똥이쭈야',
    avatar: '#c8ddd7',
    image: 'campus',
    photos: ['campus', 'brand', 'street'],
    gender: '여성',
    title: '흰 티에 연청 팬츠',
    body: '가볍게 나갈 때 손이 자주 가는 조합.',
    tags: ['무신사', '마노모스', '선글라스', '여름코디'],
    meta: '165cm/50kg · 여름 쿨톤',
    daysAgo: 1,
    likes: 1938,
    comments: 1,
    clothes: [
      { category: '상의', name: '릴랙스 화이트 티셔츠', tone: '#f7f2e8' },
      { category: '하의', name: '라이트 데님 팬츠', tone: '#c7dce6' },
    ],
    comment: { author: '피클러버', body: '색 조합 좋아요', time: '2일 전' },
  },
  {
    id: 3,
    user: '가이던스1',
    avatar: '#dbcbb7',
    image: 'studio',
    photos: ['studio', 'cafe', 'window', 'street'],
    gender: '남성',
    title: '샴브레이 셔츠 포인트',
    body: '깔끔한 여름코디',
    tags: ['아메카지', '남자코디', '여름코디', '워크웨어'],
    meta: '183cm/77kg · 가을 웜톤',
    daysAgo: 2,
    likes: 1312,
    comments: 3,
    clothes: [
      { category: '상의', name: '블루 샴브레이 셔츠', tone: '#8fb1c1' },
      { category: '하의', name: '올리브 쇼츠', tone: '#8c9179' },
      { category: '신발', name: '빈티지 스니커즈', tone: '#f4f4ed' },
    ],
    comment: { author: '상쾌한마젠타양복', body: '굿', time: '2일 전' },
  },
  {
    id: 4,
    user: '장바구니요정',
    avatar: '#e4d5c9',
    image: 'street',
    photos: ['street', 'campus', 'cafe'],
    gender: '남성',
    title: '그래픽 티 스트릿 룩',
    body: '그래픽 티 하나로 분위기를 준 코디.',
    tags: ['스트릿', '그래픽티', '와이드팬츠', '데일리룩'],
    meta: '178cm/68kg · 뉴트럴',
    daysAgo: 3,
    likes: 925,
    comments: 5,
    clothes: [
      { category: '상의', name: '그래픽 티셔츠', tone: '#f4f4ed' },
      { category: '하의', name: '와이드 데님 팬츠', tone: '#5c6672' },
    ],
    comment: { author: '데일리픽커', body: '핏 참고할게요', time: '3일 전' },
  },
  {
    id: 5,
    user: '무신사 코디',
    avatar: '#f1f1f1',
    image: 'brand',
    photos: ['brand', 'window', 'campus'],
    gender: '여성',
    title: '차분한 로맨틱 무드',
    body: '부드러운 색감으로 정리한 산책 코디.',
    tags: ['로맨틱', '슬리브리스', '롱스커트', '브랜드코디'],
    meta: '168cm/48kg · 봄 웜톤',
    daysAgo: 4,
    likes: 2104,
    comments: 8,
    clothes: [
      { category: '상의', name: '소프트 슬리브리스', tone: '#31343a' },
      { category: '하의', name: '셔링 롱스커트', tone: '#e6e0d7' },
      { category: '가방', name: '블랙 숄더백', tone: '#1f2327' },
    ],
    comment: { author: '소프트무드', body: '치마 예뻐요', time: '1일 전' },
  },
  {
    id: 6,
    user: '셔츠마니아',
    avatar: '#d9d3c8',
    image: 'window',
    photos: ['window', 'studio', 'brand'],
    gender: '남성',
    title: '린넨 셔츠와 데님',
    body: '습한 날에도 답답하지 않은 린넨 셔츠 조합.',
    tags: ['미니멀', '린넨셔츠', '데님', '남자코디'],
    meta: '180cm/72kg · 여름 쿨톤',
    daysAgo: 5,
    likes: 845,
    comments: 2,
    clothes: [
      { category: '상의', name: '린넨 셔츠', tone: '#d6c7b8' },
      { category: '하의', name: '스트레이트 데님', tone: '#66778a' },
    ],
    comment: { author: '린넨좋아', body: '시원해보여요', time: '4일 전' },
  },
  {
    id: 7,
    user: '무신사 코디',
    avatar: '#f1f1f1',
    image: 'window',
    photos: ['window', 'brand', 'studio'],
    gender: '여성',
    title: '셔츠와 스커트의 균형',
    body: '가볍게 걸쳐도 차분해 보이는 조합이에요.',
    tags: ['미니멀', '브랜드코디', '데일리룩'],
    meta: '168cm/48kg · 봄 웜톤',
    daysAgo: 1,
    likes: 1672,
    comments: 4,
    clothes: [
      { category: '상의', name: '오버핏 셔츠', tone: '#d6c7b8' },
      { category: '하의', name: '롱 스커트', tone: '#66778a' },
    ],
    comment: { author: '옷장정리중', body: '컬러 좋네요', time: '8시간 전' },
  },
  {
    id: 8,
    user: '무신사 코디',
    avatar: '#f1f1f1',
    image: 'campus',
    photos: ['campus', 'brand', 'cafe'],
    gender: '여성',
    title: '캠퍼스 데일리 추천',
    body: '편하게 오래 입기 좋은 기본 코디.',
    tags: ['캐주얼', '데일리룩', '브랜드코디'],
    meta: '168cm/48kg · 봄 웜톤',
    daysAgo: 3,
    likes: 1420,
    comments: 2,
    clothes: [
      { category: '상의', name: '베이직 티셔츠', tone: '#f7f2e8' },
      { category: '하의', name: '라이트 팬츠', tone: '#c7dce6' },
    ],
    comment: { author: '캠퍼스룩', body: '참고할게요', time: '1일 전' },
  },
  {
    id: 9,
    user: '요니찡3',
    avatar: '#d8e8ee',
    image: 'street',
    photos: ['street', 'cafe', 'window'],
    gender: '남성',
    title: '와이드 팬츠 스트릿',
    body: '편한 실루엣에 그래픽으로 포인트를 줬어요.',
    tags: ['스트릿', '남자코디', '데일리룩'],
    meta: '183cm/77kg · 가을 웜톤',
    daysAgo: 1,
    likes: 1515,
    comments: 3,
    clothes: [
      { category: '상의', name: '그래픽 티셔츠', tone: '#f4f4ed' },
      { category: '하의', name: '와이드 팬츠', tone: '#5c6672' },
    ],
    comment: { author: '핏좋다', body: '바지 핏 좋아요', time: '5시간 전' },
  },
  {
    id: 10,
    user: '요니찡3',
    avatar: '#d8e8ee',
    image: 'studio',
    photos: ['studio', 'window', 'cafe'],
    gender: '남성',
    title: '블루 셔츠 데일리',
    body: '밝은 셔츠로 여름 느낌을 살렸어요.',
    tags: ['미니멀', '남자셔츠', '남자코디'],
    meta: '183cm/77kg · 가을 웜톤',
    daysAgo: 4,
    likes: 1188,
    comments: 2,
    clothes: [
      { category: '상의', name: '블루 셔츠', tone: '#8fb1c1' },
      { category: '하의', name: '카고 쇼츠', tone: '#8c9179' },
    ],
    comment: { author: '셔츠수집가', body: '색감 예뻐요', time: '2일 전' },
  },
  {
    id: 11,
    user: '똥이쭈야',
    avatar: '#c8ddd7',
    image: 'brand',
    photos: ['brand', 'campus', 'window'],
    gender: '여성',
    title: '로맨틱 산책 룩',
    body: '부드러운 톤으로 편하게 맞춘 날.',
    tags: ['로맨틱', '걸리시', '여름코디'],
    meta: '165cm/50kg · 여름 쿨톤',
    daysAgo: 2,
    likes: 1322,
    comments: 4,
    clothes: [
      { category: '상의', name: '슬리브리스 탑', tone: '#31343a' },
      { category: '하의', name: '롱 스커트', tone: '#e6e0d7' },
    ],
    comment: { author: '쿨톤찾기', body: '분위기 좋아요', time: '12시간 전' },
  },
  {
    id: 12,
    user: '똥이쭈야',
    avatar: '#c8ddd7',
    image: 'window',
    photos: ['window', 'street', 'campus'],
    gender: '여성',
    title: '셔츠로 마무리한 데일리',
    body: '실내외 모두 편한 셔츠 코디.',
    tags: ['캐주얼', '셔츠', '데일리룩'],
    meta: '165cm/50kg · 여름 쿨톤',
    daysAgo: 5,
    likes: 1015,
    comments: 2,
    clothes: [
      { category: '상의', name: '라이트 셔츠', tone: '#d6c7b8' },
      { category: '가방', name: '데일리 백팩', tone: '#1f2327' },
    ],
    comment: { author: '데일리좋아', body: '따라 입기 좋네요', time: '3일 전' },
  },
]

function normalizeApiSnap(item) {
  const owner = item.owner || {}
  const photos = Array.isArray(item.photos) && item.photos.length ? item.photos : ['cafe']
  const height = item.height ? `${item.height}cm` : ''
  const weight = item.weight ? `${item.weight}kg` : ''
  const meta = [height && weight ? `${height}/${weight}` : height || weight, item.skin_tone].filter(Boolean).join(' · ')
  const tags = [...(item.seasons || []), ...(item.styles || []), ...(item.tpos || [])]
  const bodyTags = String(item.body || '').split(/\s+/).filter((value) => value.startsWith('#')).map((value) => value.replace('#', ''))
  const latestComment = item.latest_comment ? normalizeComment(item.latest_comment) : { author: '', body: '', time: '' }
  return {
    id: item.id,
    user: owner.username || '피클러',
    avatar: profileAvatarSource(owner) || DEFAULT_PROFILE_IMAGE,
    image: photos[0],
    photos,
    gender: item.gender_label || (item.gender === 'male' ? '남성' : '여성'),
    title: item.body || '오늘의 스냅',
    body: item.body || '',
    tags: [...new Set([...tags, ...bodyTags])],
    meta: meta || '체형 정보 없음',
    daysAgo: daysFromNow(item.created_at),
    likes: item.like_count || 0,
    comments: item.comment_count || 0,
    clothes: (item.clothing_items || []).map((clothes) => ({
      id: clothes.id,
      category: closetCategoryLabels[clothes.category] || clothes.category || '옷',
      name: clothes.name,
      tone: colorHex(clothes.color),
      image: clothingImageSource(clothes),
    })),
    comment: latestComment,
    canEdit: item.can_edit,
  }
}

function daysFromNow(value) {
  if (!value) return 0
  const diff = Date.now() - new Date(value).getTime()
  return Math.max(0, Math.floor(diff / 86400000))
}

function relativeTime(value) {
  if (!value) return '방금 전'
  const diff = Math.max(0, Date.now() - new Date(value).getTime())
  const minutes = Math.floor(diff / 60000)
  if (minutes < 1) return '방금 전'
  if (minutes < 60) return `${minutes}분 전`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}시간 전`
  return `${Math.floor(hours / 24)}일 전`
}

function normalizeApiMember(member) {
  const rawChange = member.rank_change ?? member.change ?? member.rank_delta ?? 0
  const change = Math.abs(Number(rawChange) || 0)
  const trend = member.trend || member.rank_trend || (Number(rawChange) < 0 ? 'down' : 'up')
  return {
    user: member.username,
    avatar: profileAvatarSource(member) || DEFAULT_PROFILE_IMAGE,
    followers: member.follower_count || 0,
    change,
    trend,
    snaps: (member.snaps || []).map((snap) => normalizeApiSnap({
      ...snap,
      owner: member,
      gender: 'unisex',
      gender_label: '전체',
    })),
  }
}

function normalizeApiProfile(profile) {
  return {
    user: profile.username || profile.user || profile.nickname,
    avatar: profile.profile_image || profile.avatar || profile.image || '',
    posts: profile.post_count ?? profile.posts_count ?? profile.posts ?? profile.snap_count ?? profile.snaps_count ?? 0,
    followers: profile.follower_count ?? profile.followers_count ?? profile.followers ?? 0,
    following: profile.following_count ?? profile.followings_count ?? profile.following ?? 0,
    isFollowing: Boolean(profile.is_following),
  }
}

function syncSnapState(items) {
  liked.value = new Set(items.filter((snap) => snap.is_liked).map((snap) => snap.id))
  scrapped.value = new Set(items.filter((snap) => snap.is_scrapped).map((snap) => snap.id))
  const nextFollowing = new Set(following.value)
  items.forEach((snap) => {
    if (snap.owner?.is_following) nextFollowing.add(snap.owner.username)
  })
  following.value = nextFollowing
}

async function loadSnaps() {
  apiLoading.value = true
  try {
    snapLoadError.value = ''
    const params = {}
    if (selectedGender.value === '남성') params.gender = 'male'
    if (selectedGender.value === '여성') params.gender = 'female'
    if (selectedSeasons.value.length) params.season = selectedSeasons.value.join(',')
    if (selectedStyles.value.length) params.style = selectedStyles.value.join(',')
    if (selectedTpos.value.length) params.tpo = selectedTpos.value.join(',')
    if (sortBy.value) params.sort = sortBy.value
    if (activeTab.value === 'scrap') params.scrapped = 'true'
    if (activeTab.value === 'following') params.following = 'true'
    const { data } = await client.get('/snaps/', { params })
    const rows = data.results || data || []
    apiSnaps.value = rows.map(normalizeApiSnap)
    syncSnapState(rows)
    apiLoaded.value = true
  } catch (error) {
    const status = error.response?.status
    if (status === 401) {
      localStorage.removeItem('pickle_access_token')
      localStorage.removeItem('pickle_refresh_token')
      snapLoadError.value = '로그인 정보가 만료됐어요. 다시 로그인해 주세요.'
    } else {
      snapLoadError.value = '스냅을 불러오지 못했어요. 잠시 후 다시 시도해 주세요.'
    }
    apiSnaps.value = []
    apiLoaded.value = true
  } finally {
    apiLoading.value = false
  }
}

async function loadMemberRanking() {
  try {
    const { data } = await client.get('/snaps/members/ranking/')
    apiMembers.value = (data.results || data || []).map(normalizeApiMember)
  } catch {
    apiMembers.value = []
  }
}

const allSnaps = computed(() => apiLoaded.value ? [...apiSnaps.value, ...mySnaps.value] : [])
const currentSnap = computed(() => selectedSnap.value || allSnaps.value[0] || emptySnap)
const sortLabel = computed(() => sortOptions.find((item) => item.key === sortBy.value)?.label || '인기순')
const activeFilterChips = computed(() => [
  ...(selectedGender.value !== '전체' ? [{ type: 'gender', value: selectedGender.value === '남성' ? '남' : '여' }] : []),
  ...selectedSeasons.value.map((value) => ({ type: 'season', value })),
  ...selectedStyles.value.map((value) => ({ type: 'style', value })),
  ...selectedTpos.value.map((value) => ({ type: 'tpo', value })),
  ...(heightRange.value.min || heightRange.value.max ? [{ type: 'height', value: `${heightRange.value.min || '100'}-${heightRange.value.max || '220'}cm` }] : []),
  ...(weightRange.value.min || weightRange.value.max ? [{ type: 'weight', value: `${weightRange.value.min || '30'}-${weightRange.value.max || '150'}kg` }] : []),
])
const filteredSnaps = computed(() => {
  const base = selectedGender.value === '전체' ? allSnaps.value : allSnaps.value.filter((snap) => snap.gender === selectedGender.value)
  const withSeason = selectedSeasons.value.length
    ? base.filter((snap) => selectedSeasons.value.some((season) => snap.tags.some((tag) => tag.includes(season)) || snap.title.includes(season) || snap.body.includes(season)))
    : base
  const withStyle = selectedStyles.value.length ? withSeason.filter((snap) => selectedStyles.value.some((style) => snap.tags.includes(style))) : withSeason
  const withTpo = selectedTpos.value.length ? withStyle.filter((snap) => selectedTpos.value.some((tpo) => snap.tags.includes(tpo) || snap.title.includes(tpo))) : withStyle
  return withTpo
})
const sortedSnaps = computed(() => {
  const sorted = [...filteredSnaps.value]
  if (sortBy.value === 'latest') return sorted.sort((a, b) => a.daysAgo - b.daysAgo)
  if (sortBy.value === 'recommend') return sorted.sort((a, b) => a.id - b.id)
  return sorted.sort((a, b) => b.likes - a.likes)
})
const feedSnaps = computed(() => activeTab.value === 'scrap' ? sortedSnaps.value.filter((snap) => scrapped.value.has(snap.id)) : sortedSnaps.value)
const rankedSnaps = computed(() => [...filteredSnaps.value].sort((a, b) => b.likes - a.likes))
const rankingPeriodLabel = computed(() => rankingPeriodOptions.find((item) => item.key === rankingPeriod.value)?.label || '최근 1일')
const rankingBaseTime = computed(() => {
  const now = new Date()
  return `${String(now.getMonth() + 1).padStart(2, '0')}.${String(now.getDate()).padStart(2, '0')} ${String(now.getHours()).padStart(2, '0')}:00 기준`
})
const mockRankedMembers = computed(() => [
  { user: '무신사 코디', avatar: '#f1f1f1', followers: 1654, change: 4, trend: 'up', snapIds: [5, 7, 8] },
  { user: '요니찡3', avatar: '#d8e8ee', followers: 420, change: 1, trend: 'down', snapIds: [1, 9, 10] },
  { user: '똥이쭈야', avatar: '#c8ddd7', followers: 155, change: 2, trend: 'up', snapIds: [2, 11, 12] },
].map((member) => ({
  ...member,
  snaps: member.snapIds.map((id) => allSnaps.value.find((snap) => snap.id === id)).filter(Boolean),
})))
const rankedMembers = computed(() => apiLoaded.value ? apiMembers.value : [])
const displayRankedMembers = computed(() => rankedMembers.value.map((member) => {
  const profile = profileRows.value[member.user]
  const snaps = allSnaps.value.filter((snap) => snap.user === member.user)
  return {
    ...member,
    followers: profile?.followers ?? member.followers,
    avatar: profile?.avatar || member.avatar,
    snaps: member.snaps?.length ? member.snaps : snaps,
    isMe: member.user === MY_USER.value,
  }
}))
const followingSnap = computed(() => allSnaps.value.find((snap) => snap.image === 'brand'))
const followingRecommendations = computed(() => activeTab.value === 'following' ? sortedSnaps.value : allSnaps.value.slice(4, 12))
const viewerSnap = computed(() => selectedViewerSnap.value || currentSnap.value)
const currentComments = computed(() => commentRows.value[currentSnap.value.id] || [])
const selectedProfileSnaps = computed(() => allSnaps.value.filter((snap) => snap.user === selectedProfileUser.value))
const selectedProfile = computed(() => {
  const snap = selectedProfileSnaps.value[0] || currentSnap.value
  const member = rankedMembers.value.find((item) => item.user === selectedProfileUser.value)
  const profile = profileRows.value[selectedProfileUser.value]
  const isMe = selectedProfileUser.value === MY_USER.value
  const ownProfile = { ...(userStore.profile || {}), ...profileEditOverrides.value }
  const ownAvatar = profileAvatarSource(ownProfile) || (isAvatarImageSource(profile?.avatar) ? profile.avatar : '')
  const otherAvatar = isAvatarImageSource(profile?.avatar) ? profile.avatar : (isAvatarImageSource(snap.avatar) ? snap.avatar : '')
  const avatarPhoto = isMe ? ownAvatar : otherAvatar
  const ownHeight = ownProfile.height || ownProfile.profile_height || snapForm.value.height
  const ownWeight = ownProfile.weight || ownProfile.profile_weight || snapForm.value.weight
  const ownTone = ownProfile.skin_tone || ownProfile.skinTone || ownProfile.tone || snapForm.value.tone
  const ownBio = ownProfile.bio || ownProfile.introduction || profileBio.value
  return {
    user: selectedProfileUser.value,
    avatar: avatarPhoto || DEFAULT_PROFILE_IMAGE,
    avatarPhoto,
    meta: isMe ? `${ownHeight}cm/${ownWeight}kg${ownTone ? ` · ${ownTone}` : ''}` : snap.meta,
    posts: Math.max(Number(profile?.posts) || 0, selectedProfileSnaps.value.length),
    followers: Number(profile?.followers ?? member?.followers ?? ownProfile.follower_count ?? ownProfile.followers_count ?? ownProfile.followers ?? 0) || 0,
    following: Number(profile?.following ?? ownProfile.following_count ?? ownProfile.followings_count ?? ownProfile.following ?? 0) || 0,
    bio: isMe ? ownBio || '좋아하는 스타일이나 브랜드로 간단한 소개를 적어보세요.' : '...',
    isMe,
  }
})
const myProfileAvatar = computed(() => {
  const rowAvatar = profileRows.value[MY_USER.value]?.avatar
  const ownProfile = { ...(userStore.profile || {}), ...profileEditOverrides.value }
  return profileAvatarSource(ownProfile) || (isAvatarImageSource(rowAvatar) ? rowAvatar : DEFAULT_PROFILE_IMAGE)
})
const myProfileAvatarStyle = computed(() => avatarStyle(myProfileAvatar.value))
const detailSnaps = computed(() => {
  const source = detailSourceSnaps.value || sortedSnaps.value
  const index = source.findIndex((snap) => snap.id === currentSnap.value.id)
  return index < 0 ? [currentSnap.value] : source.slice(index)
})
const snapCount = computed(() => filteredSnaps.value.length)
const displayLikes = computed(() => currentSnap.value.likes)
const currentPhotoClass = computed(() => currentSnap.value.photos[photoIndex.value] || currentSnap.value.image)
const filterSheetCount = computed(() => snapCount.value)
const snapFormBodyLabel = computed(() => {
  const height = snapForm.value.height ? `${snapForm.value.height}cm` : ''
  const weight = snapForm.value.weight ? `${snapForm.value.weight}kg` : ''
  return [height, weight].filter(Boolean).join(' / ') || '체형 정보를 입력해 주세요.'
})
const closetOptions = computed(() => {
  const source = closetLoaded.value || closetLoading.value ? closetClothes.value : closetItems
  return source.map(normalizeClosetSelection)
})
const filteredClosetOptions = computed(() => selectedClosetCategory.value === 'all'
  ? closetOptions.value
  : closetOptions.value.filter((item) => item.categoryKey === selectedClosetCategory.value))

function photoPosition(snap) {
  return photoIndexes.value[snap.id] || 0
}

function photoClass(snap) {
  return snap.photos[photoPosition(snap)] || snap.image
}

function parsePhotoEdit(photo) {
  const raw = String(photo || '')
  const [source, encodedMeta = ''] = raw.split(PHOTO_EDIT_SEPARATOR)
  const params = new URLSearchParams(encodedMeta)
  const rotation = Number(params.get('rotate') || 0)
  const x = Number(params.get('x') || 0)
  const y = Number(params.get('y') || 0)
  const zoom = Number(params.get('zoom') || 1)
  return {
    source,
    rotation: Number.isFinite(rotation) ? rotation : 0,
    x: Number.isFinite(x) ? x : 0,
    y: Number.isFinite(y) ? y : 0,
    zoom: Number.isFinite(zoom) ? Math.max(1, Math.min(2.6, zoom)) : 1,
  }
}

function encodePhotoEdit(photo, patch = {}) {
  const current = parsePhotoEdit(photo)
  const next = { ...current, ...patch }
  const rotation = ((Number(next.rotation || 0) % 360) + 360) % 360
  const zoom = Math.max(1, Math.min(2.6, Number(next.zoom) || 1))
  const params = new URLSearchParams()
  if (rotation) params.set('rotate', String(rotation))
  if (Number(next.x)) params.set('x', String(Math.round(Number(next.x))))
  if (Number(next.y)) params.set('y', String(Math.round(Number(next.y))))
  if (zoom !== 1) params.set('zoom', String(Number(zoom.toFixed(2))))
  const encodedMeta = params.toString()
  return encodedMeta ? `${next.source}${PHOTO_EDIT_SEPARATOR}${encodedMeta}` : next.source
}

function isUploadedPhoto(photo) {
  return /^(https?:|blob:|data:|\/media\/|media\/)/.test(parsePhotoEdit(photo).source)
}

function photoSourceUrl(source) {
  return String(source || '').startsWith('media/') ? `/${source}` : source
}

function clothingImageSource(item = {}) {
  const images = Array.isArray(item.images) ? item.images : []
  const source = item.processed_image
    || item.processedImage
    || item.image
    || item.image_url
    || item.imageUrl
    || item.photo
    || item.photo_url
    || item.thumbnail
    || item.thumbnail_url
    || item.main_image
    || item.clothing_image
    || images[0]?.processed_image
    || images[0]?.image
    || images[0]?.url
    || ''
  return photoSourceUrl(source)
}

function clothingThumbStyle(item = {}) {
  const source = clothingImageSource(item)
  return source
    ? { '--tone': item.tone || '#f7efe3', backgroundImage: `url("${source}")` }
    : { '--tone': item.tone || '#f7efe3' }
}

function ensurePhotoDimensions(photo) {
  if (!isUploadedPhoto(photo)) return Promise.resolve(null)
  const { source } = parsePhotoEdit(photo)
  const imageUrl = photoSourceUrl(source)
  if (photoDimensions.value[imageUrl]) return Promise.resolve(photoDimensions.value[imageUrl])
  if (photoDimensionPromises.has(imageUrl)) return photoDimensionPromises.get(imageUrl)

  const promise = new Promise((resolve) => {
    const image = new Image()
    image.onload = () => {
      const dimensions = { width: image.naturalWidth || image.width, height: image.naturalHeight || image.height }
      photoDimensions.value = { ...photoDimensions.value, [imageUrl]: dimensions }
      photoDimensionPromises.delete(imageUrl)
      resolve(dimensions)
    }
    image.onerror = () => {
      photoDimensionPromises.delete(imageUrl)
      resolve(null)
    }
    image.src = imageUrl
  })
  photoDimensionPromises.set(imageUrl, promise)
  return promise
}

function cropMovementLimits(photo, zoom = parsePhotoEdit(photo).zoom) {
  const { source, rotation } = parsePhotoEdit(photo)
  const dimensions = photoDimensions.value[photoSourceUrl(source)]
  if (!dimensions?.width || !dimensions?.height) {
    const limit = Math.max(0, zoom - 1) * 180
    return { x: limit, y: limit, padX: 0, padY: 0 }
  }

  const imageAspect = rotation % 180 === 0
    ? dimensions.width / dimensions.height
    : dimensions.height / dimensions.width
  const frameAspect = CROP_FRAME.width / CROP_FRAME.height
  const padX = imageAspect > frameAspect
    ? Math.max(0, (CROP_FRAME.height * imageAspect - CROP_FRAME.width) / 2)
    : 0
  const padY = imageAspect < frameAspect
    ? Math.max(0, (CROP_FRAME.width / imageAspect - CROP_FRAME.height) / 2)
    : 0
  const zoomX = ((CROP_FRAME.width + padX * 2) * Math.max(0, zoom - 1)) / 2
  const zoomY = ((CROP_FRAME.height + padY * 2) * Math.max(0, zoom - 1)) / 2

  return {
    x: padX + zoomX,
    y: padY + zoomY,
    padX,
    padY,
  }
}

function snapPhotoClass(photo) {
  const { source } = parsePhotoEdit(photo)
  return isUploadedPhoto(photo) ? 'has-image' : `snap-photo--${source}`
}

function snapPhotoStyle(photo, applyEdit = false) {
  if (!isUploadedPhoto(photo)) return {}
  const { source, rotation, x, y, zoom } = parsePhotoEdit(photo)
  const imageUrl = photoSourceUrl(source)
  if (applyEdit) ensurePhotoDimensions(photo)
  const limits = applyEdit ? cropMovementLimits(photo, zoom) : { x: 0, y: 0, padX: 0, padY: 0 }
  const nextX = applyEdit ? Math.max(-limits.x, Math.min(limits.x, x)) : 0
  const nextY = applyEdit ? Math.max(-limits.y, Math.min(limits.y, y)) : 0
  return {
    '--photo-image': `url("${imageUrl}")`,
    '--photo-rotate': `${rotation}deg`,
    '--photo-scale': (rotation % 180 === 0 ? 1 : 1.42) * (applyEdit ? zoom : 1),
    '--photo-x': `${nextX}px`,
    '--photo-y': `${nextY}px`,
    '--photo-pad-x': `${limits.padX}px`,
    '--photo-pad-y': `${limits.padY}px`,
  }
}

function profileAvatarSource(profile) {
  if (!profile) return ''
  if (profile.profile_image_removed) return ''
  return profile.profile_image || profile.profileImage || profile.avatar_image || profile.avatarPhoto || profile.avatar || profile.image || profile.photo || ''
}

function profileEditStorageKey(profile = userStore.profile) {
  const ownerKey = profile?.id || profile?.email || profile?.username || profile?.nickname
  return ownerKey ? `${PROFILE_EDIT_LOCAL_KEY}:${ownerKey}` : ''
}

function readProfileEditOverrides(profile = userStore.profile) {
  const key = profileEditStorageKey(profile)
  if (!key) return {}
  try {
    return JSON.parse(localStorage.getItem(key) || '{}')
  } catch {
    return {}
  }
}

function readProfileSnapshotRows() {
  try {
    const snapshot = JSON.parse(localStorage.getItem(PROFILE_SNAPSHOT_LOCAL_KEY) || 'null')
    if (!snapshot?.user) return {}
    return { [snapshot.user]: snapshot }
  } catch {
    return {}
  }
}

function refreshProfileEditOverrides() {
  profileEditOverrides.value = readProfileEditOverrides()
}

function cacheProfileSnapshot(profile = selectedProfile.value) {
  if (!profile?.user) return
  const snapshot = {
    user: profile.user,
    avatar: profile.avatarPhoto || profile.avatar || '',
    posts: profile.posts || 0,
    followers: profile.followers || 0,
    following: profile.following || 0,
    isFollowing: false,
  }
  localStorage.setItem(PROFILE_SNAPSHOT_LOCAL_KEY, JSON.stringify(snapshot))
  profileRows.value = {
    ...profileRows.value,
    [snapshot.user]: {
      ...(profileRows.value[snapshot.user] || {}),
      ...snapshot,
    },
  }
}

function isAvatarImageSource(value) {
  const source = String(value || '')
  return /^(https?:|blob:|data:|\/|media\/)/.test(source)
}

function avatarStyle(value) {
  const source = String(value || DEFAULT_PROFILE_IMAGE)
  if (isAvatarImageSource(source)) {
    const imageUrl = source.startsWith('media/') ? `/${source}` : source
    return { backgroundImage: `url("${imageUrl}")` }
  }
  if (source.startsWith('#')) return { backgroundImage: `url("${DEFAULT_PROFILE_IMAGE}")` }
  return { backgroundImage: `url("${DEFAULT_PROFILE_IMAGE}")` }
}

function avatarForUser(username, fallback = '') {
  if (username === MY_USER.value) {
    const ownProfile = { ...(userStore.profile || {}), ...profileEditOverrides.value }
    return profileAvatarSource(ownProfile) || DEFAULT_PROFILE_IMAGE
  }
  return isAvatarImageSource(fallback) ? fallback : DEFAULT_PROFILE_IMAGE
}

function openFilter(key) {
  if (productDrag.value.moved) return
  activeFilter.value = key
  showFilterSheet.value = true
}

function toggleStyle(value) {
  selectedStyles.value = selectedStyles.value.includes(value)
    ? selectedStyles.value.filter((item) => item !== value)
    : [...selectedStyles.value, value]
}

function toggleSeason(value) {
  selectedSeasons.value = selectedSeasons.value.includes(value)
    ? selectedSeasons.value.filter((item) => item !== value)
    : [...selectedSeasons.value, value]
}

function toggleTpo(value) {
  selectedTpos.value = selectedTpos.value.includes(value)
    ? selectedTpos.value.filter((item) => item !== value)
    : [...selectedTpos.value, value]
}

function removeFilterChip(chip) {
  if (chip.type === 'gender') selectedGender.value = '전체'
  if (chip.type === 'season') selectedSeasons.value = selectedSeasons.value.filter((value) => value !== chip.value)
  if (chip.type === 'style') selectedStyles.value = selectedStyles.value.filter((value) => value !== chip.value)
  if (chip.type === 'tpo') selectedTpos.value = selectedTpos.value.filter((value) => value !== chip.value)
  if (chip.type === 'height') heightRange.value = { min: '', max: '' }
  if (chip.type === 'weight') weightRange.value = { min: '', max: '' }
}

function resetSnapForm() {
  const profileDefaults = snapProfileDefaults()
  snapForm.value = {
    photos: [],
    body: '',
    clothes: [],
    height: profileDefaults.height,
    weight: profileDefaults.weight,
    tone: profileDefaults.tone,
    gender: profileDefaults.gender,
    seasons: [],
    styles: [],
    tpos: [],
  }
  editingSnapId.value = null
}

function snapProfileDefaults() {
  const profile = { ...(userStore.profile || {}), ...profileEditOverrides.value }
  return {
    height: String(profile.height || profile.profile_height || ''),
    weight: String(profile.weight || profile.profile_weight || ''),
    tone: profile.skin_tone || profile.skinTone || profile.tone || '',
    gender: genderToLabel(profile.gender || profile.gender_label || profile.genderLabel || ''),
  }
}

function genderToLabel(value) {
  if (value === 'male' || value === '남성' || value === '남') return '남성'
  if (value === 'female' || value === '여성' || value === '여') return '여성'
  return ''
}

function genderToApi(value) {
  if (value === '남성' || value === '남' || value === 'male') return 'male'
  if (value === '여성' || value === '여' || value === 'female') return 'female'
  return ''
}

function openSnapCreate() {
  resetSnapForm()
  showSnapForm.value = true
  showSnapGuide.value = true
  transitionName.value = 'slide-forward'
}

function openSnapEdit(snap) {
  snapForm.value = {
    photos: [...snap.photos],
    body: snap.body,
    clothes: normalizeClosetItems(snap.clothes),
    height: snap.meta.match(/(\d+)cm/)?.[1] || '',
    weight: snap.meta.match(/(\d+)kg/)?.[1] || '',
    tone: snap.meta.split('· ')[1] || '',
    gender: snap.gender,
    seasons: snap.tags.filter((tag) => seasons.includes(tag)),
    styles: snap.tags.filter((tag) => styles.includes(tag)),
    tpos: snap.tags.filter((tag) => tpos.includes(tag)),
  }
  editingSnapId.value = snap.id
  showMoreSheet.value = false
  showSnapForm.value = true
  transitionName.value = 'slide-forward'
}

function requestCloseSnapForm() {
  showSnapBackWarning.value = true
}

function closeSnapForm() {
  showSnapBackWarning.value = false
  showSnapForm.value = false
  showClosetPicker.value = false
  showPictureEditor.value = false
  editingPhotoIndex.value = null
  resetSnapForm()
}

function openPictureEditor(photo = 'cafe', index = null) {
  if (productDrag.value.moved) return
  draftPhotoClass.value = photo
  draftPhotoZoom.value = parsePhotoEdit(photo).zoom
  cropDrag.value = { x: 0, y: 0, startX: 0, startY: 0, active: false }
  cropPinch.value = { distance: 0, zoom: draftPhotoZoom.value }
  cropPointers.clear()
  editingPhotoIndex.value = index
  showPictureEditor.value = true
  transitionName.value = 'slide-forward'
  ensurePhotoDimensions(photo).then(() => {
    const { x, y, zoom } = parsePhotoEdit(draftPhotoClass.value)
    const next = clampCropPosition(x, y, zoom)
    draftPhotoClass.value = encodePhotoEdit(draftPhotoClass.value, { x: next.x, y: next.y })
  })
}

function rotateDraftPhoto(step) {
  const { rotation } = parsePhotoEdit(draftPhotoClass.value)
  draftPhotoClass.value = encodePhotoEdit(draftPhotoClass.value, { rotation: rotation + step, x: 0, y: 0 })
}

function updateDraftZoom(value) {
  const zoom = Math.max(1, Math.min(2.6, Number(value) || 1))
  const { x, y } = parsePhotoEdit(draftPhotoClass.value)
  const next = clampCropPosition(x, y, zoom)
  draftPhotoZoom.value = zoom
  draftPhotoClass.value = encodePhotoEdit(draftPhotoClass.value, { zoom, x: next.x, y: next.y })
}

function cropDistance() {
  const points = [...cropPointers.values()]
  if (points.length < 2) return 0
  return Math.hypot(points[0].x - points[1].x, points[0].y - points[1].y)
}

function clampCropPosition(x, y, zoom = parsePhotoEdit(draftPhotoClass.value).zoom) {
  const limit = cropMovementLimits(draftPhotoClass.value, zoom)
  return {
    x: Math.max(-limit.x, Math.min(limit.x, x)),
    y: Math.max(-limit.y, Math.min(limit.y, y)),
  }
}

function beginCropDrag(event) {
  if (!isUploadedPhoto(draftPhotoClass.value)) return
  cropPointers.set(event.pointerId, { x: event.clientX, y: event.clientY })
  if (cropPointers.size >= 2) {
    cropPinch.value = { distance: cropDistance(), zoom: parsePhotoEdit(draftPhotoClass.value).zoom }
    cropDrag.value = { x: 0, y: 0, startX: 0, startY: 0, active: false }
    return
  }
  const { x, y } = parsePhotoEdit(draftPhotoClass.value)
  event.currentTarget.setPointerCapture?.(event.pointerId)
  cropDrag.value = { x: event.clientX, y: event.clientY, startX: x, startY: y, active: true }
}

function moveCropDrag(event) {
  if (cropPointers.has(event.pointerId)) cropPointers.set(event.pointerId, { x: event.clientX, y: event.clientY })
  if (cropPointers.size >= 2) {
    const baseDistance = cropPinch.value.distance || cropDistance()
    if (!baseDistance) return
    updateDraftZoom(cropPinch.value.zoom * (cropDistance() / baseDistance))
    return
  }
  if (!cropDrag.value.active) return
  const { zoom } = parsePhotoEdit(draftPhotoClass.value)
  const next = clampCropPosition(
    cropDrag.value.startX + event.clientX - cropDrag.value.x,
    cropDrag.value.startY + event.clientY - cropDrag.value.y,
    zoom,
  )
  draftPhotoClass.value = encodePhotoEdit(draftPhotoClass.value, {
    x: next.x,
    y: next.y,
  })
}

function endCropDrag(event) {
  if (event?.pointerId !== undefined) cropPointers.delete(event.pointerId)
  if (cropPointers.size < 2) cropPinch.value = { distance: 0, zoom: parsePhotoEdit(draftPhotoClass.value).zoom }
  cropDrag.value = { x: 0, y: 0, startX: 0, startY: 0, active: false }
}

function handleCropWheel(event) {
  event.preventDefault()
  const delta = event.deltaY < 0 ? 0.08 : -0.08
  updateDraftZoom(draftPhotoZoom.value + delta)
}

function completePictureEdit() {
  if (editingPhotoIndex.value !== null) {
    snapForm.value.photos = snapForm.value.photos.map((photo, index) => (
      index === editingPhotoIndex.value ? draftPhotoClass.value : photo
    ))
  } else if (snapForm.value.photos.length < 10) {
    snapForm.value.photos = [...snapForm.value.photos, draftPhotoClass.value]
  }
  editingPhotoIndex.value = null
  showPictureEditor.value = false
}

function removeSnapPhoto(index) {
  snapForm.value.photos = snapForm.value.photos.filter((_, itemIndex) => itemIndex !== index)
}

async function uploadSnapPhotos(event) {
  const files = Array.from(event.target.files || []).slice(0, 10 - snapForm.value.photos.length)
  event.target.value = ''
  if (!files.length || snapPhotoUploading.value) return

  snapPhotoUploading.value = true
  try {
    const uploaded = []
    for (const file of files) {
      const body = new FormData()
      body.append('image', file)
      const { data } = await client.post('/snaps/upload-photo/', body, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      if (data?.url) uploaded.push(data.url)
    }
    snapForm.value.photos = [...snapForm.value.photos, ...uploaded].slice(0, 10)
  } catch {
    showSnapPhotoWarning.value = true
  } finally {
    snapPhotoUploading.value = false
  }
}

function appendBodyTag(tag) {
  if (productDrag.value.moved) return
  const value = tag.replace(/\s/g, '')
  if (!snapForm.value.body.includes(value)) snapForm.value.body = `${snapForm.value.body}${snapForm.value.body ? ' ' : ''}${value}`
}

function normalizeClosetItems(items = []) {
  return items.map((item, index) => {
    const matched = closetOptions.value.find((closetItem) => closetItem.name === item.name)
    return matched || normalizeClosetSelection({ ...item, id: item.id || `snap-clothes-${index}` })
  })
}

function normalizeClosetSelection(item) {
  const categoryKey = item.categoryKey || item.category || 'top'
  return {
    id: item.id,
    categoryKey,
    category: item.categoryKey ? item.category : closetCategoryLabels[categoryKey] || item.category || '상의',
    name: item.name || '이름 없는 옷',
    tone: item.tone || colorHex(item.color),
    image: clothingImageSource(item),
  }
}

function toggleClosetItem(item) {
  if (productDrag.value.moved) return
  snapForm.value.clothes = snapForm.value.clothes.some((clothes) => clothes.id === item.id)
    ? snapForm.value.clothes.filter((clothes) => clothes.id !== item.id)
    : [...snapForm.value.clothes, item]
}

async function loadClosetClothes() {
  if (closetLoaded.value || closetLoading.value) return
  closetLoading.value = true
  try {
    const { data } = await client.get('/closet/')
    closetClothes.value = data.results || data || []
  } catch {
    closetClothes.value = []
  } finally {
    closetLoaded.value = true
    closetLoading.value = false
  }
}

async function openClosetPicker() {
  selectedClosetCategory.value = 'all'
  showClosetPicker.value = true
  transitionName.value = 'slide-forward'
  await loadClosetClothes()
}

function closeClosetPicker() {
  showClosetPicker.value = false
  transitionName.value = 'slide-back'
}

async function loadSnapProfile(username, { force = false } = {}) {
  if (!username || (!force && profileRows.value[username])) return
  try {
    const { data } = await client.get(`/snaps/profiles/${username}/`)
    const profile = normalizeApiProfile(data)
    profileRows.value = {
      ...profileRows.value,
      [username]: profile,
    }
    const next = new Set(following.value)
    if (profile.isFollowing) next.add(username)
    else next.delete(username)
    following.value = next
    if (username === MY_USER.value) {
      userStore.profile = {
        ...(userStore.profile || {}),
        post_count: profile.posts,
        follower_count: profile.followers,
        following_count: profile.following,
      }
      cacheProfileSnapshot({
        ...profile,
        avatarPhoto: profile.avatar,
      })
    }
  } catch {
    // 프로필 숫자 조회 실패 시 화면은 현재 보유한 스냅 정보로 표시한다.
  }
}

async function openMyProfile() {
  refreshProfileEditOverrides()
  transitionName.value = 'slide-forward'
  selectedProfileUser.value = MY_USER.value
  if (route.query.profile !== 'me') {
    await router.replace({ name: 'lookbook', query: { profile: 'me' } })
  }
  await loadSnapProfile(MY_USER.value, { force: true })
  cacheProfileSnapshot()
}

async function openInitialProfileFromRoute() {
  if (route.query.profile !== 'me') return
  refreshProfileEditOverrides()
  transitionName.value = route.query.motion === 'back' ? 'slide-back' : 'slide-forward'
  activeTab.value = 'snap'
  selectedProfileUser.value = MY_USER.value
  await loadSnapProfile(MY_USER.value, { force: true })
  cacheProfileSnapshot()
}

function openProfileEdit() {
  cacheProfileSnapshot()
  router.push({ name: 'my-profile', query: { from: 'snap-profile', returnTo: 'snap-profile' } })
}

function startProfileBioEdit() {
  if (!selectedProfile.value.isMe) return
  profileBioDraft.value = profileBio.value
  editingProfileBio.value = true
}

function saveProfileBio() {
  profileBio.value = profileBioDraft.value.trim()
  localStorage.setItem('pickle_snap_bio', profileBio.value)
  profileEditOverrides.value = { ...profileEditOverrides.value, bio: profileBio.value, introduction: profileBio.value }
  const key = profileEditStorageKey()
  if (key) localStorage.setItem(key, JSON.stringify(profileEditOverrides.value))
  editingProfileBio.value = false
}

function cancelProfileBioEdit() {
  profileBioDraft.value = ''
  editingProfileBio.value = false
}

async function openFollowList(tab) {
  followListTab.value = tab
  followListUser.value = selectedProfile.value.user
  showFollowList.value = true
  followListRows.value = []
  followListLoading.value = true
  transitionName.value = 'slide-forward'
  try {
    const { data } = await client.get(`/snaps/profiles/${followListUser.value}/${tab}/`)
    const rows = data.results || data || []
    followListRows.value = rows.map(normalizeApiProfile)
    const next = new Set(following.value)
    rows.forEach((row) => {
      if (row.is_following) next.add(row.username)
      else next.delete(row.username)
    })
    following.value = next
  } catch {
    const users = [...new Map(allSnaps.value
      .filter((snap) => snap.user !== followListUser.value)
      .map((snap) => [snap.user, { user: snap.user, avatar: snap.avatar, posts: 0, followers: 0, following: 0, isFollowing: following.value.has(snap.user) }]))
      .values()]
    followListRows.value = tab === 'following'
      ? users.filter((user) => following.value.has(user.user))
      : users.slice(0, selectedProfile.value.followers || 10)
  } finally {
    followListLoading.value = false
  }
}

function closeFollowList() {
  showFollowList.value = false
  followListRows.value = []
  transitionName.value = 'slide-back'
}

async function openFollowUser(username) {
  showFollowList.value = false
  transitionName.value = 'slide-forward'
  selectedProfileUser.value = username
  await loadSnapProfile(username)
}

function toggleSingleForm(key, value) {
  snapForm.value[key] = snapForm.value[key] === value ? '' : value
}

function toggleLimitedForm(key, value, limit = 2) {
  const current = snapForm.value[key]
  snapForm.value[key] = current.includes(value)
    ? current.filter((item) => item !== value)
    : current.length < limit ? [...current, value] : current
}

function snapFormPayload() {
  return {
    body: snapForm.value.body || '#오늘의스냅',
    photos: [...snapForm.value.photos],
    gender: genderToApi(snapForm.value.gender),
    seasons: [...snapForm.value.seasons],
    styles: [...snapForm.value.styles],
    tpos: [...snapForm.value.tpos],
    height: snapForm.value.height ? Number(snapForm.value.height) : null,
    weight: snapForm.value.weight ? Number(snapForm.value.weight) : null,
    skin_tone: snapForm.value.tone,
    clothing_item_ids: snapForm.value.clothes
      .map((item) => Number(item.id))
      .filter((id) => Number.isInteger(id)),
  }
}

async function submitSnapForm() {
  if (!snapForm.value.photos.length) {
    snapFormWarning.value = { title: '사진을 첨부해 주세요.', body: '스냅 등록을 위해서는 최소 1장의 사진이 필요합니다.' }
    showSnapPhotoWarning.value = true
    return
  }
  if (!snapForm.value.gender) {
    snapFormWarning.value = { title: '성별을 선택해 주세요.', body: '스냅을 등록하려면 성별 선택이 필요합니다.' }
    showSnapPhotoWarning.value = true
    return
  }
  if (!snapForm.value.seasons.length) {
    snapFormWarning.value = { title: '계절을 선택해 주세요.', body: '계절 태그를 1개 이상 선택해 주세요.' }
    showSnapPhotoWarning.value = true
    return
  }
  if (!snapForm.value.styles.length) {
    snapFormWarning.value = { title: '스타일 태그를 선택해 주세요.', body: '스타일 태그를 1개 이상 선택해 주세요.' }
    showSnapPhotoWarning.value = true
    return
  }
  if (!snapForm.value.tpos.length) {
    snapFormWarning.value = { title: 'TPO를 선택해 주세요.', body: 'TPO 태그를 1개 이상 선택해 주세요.' }
    showSnapPhotoWarning.value = true
    return
  }
  const payload = snapFormPayload()
  try {
    const { data } = editingSnapId.value
      ? await client.patch(`/snaps/${editingSnapId.value}/`, payload)
      : await client.post('/snaps/', payload)
    const nextSnap = normalizeApiSnap(data)
    apiSnaps.value = editingSnapId.value
      ? apiSnaps.value.map((snap) => snap.id === nextSnap.id ? nextSnap : snap)
      : [nextSnap, ...apiSnaps.value]
    apiLoaded.value = true
    showSnapForm.value = false
    showClosetPicker.value = false
    selectedProfileUser.value = MY_USER.value
    selectedSnap.value = null
    resetSnapForm()
  } catch (error) {
    if (error.response?.data?.photos) {
      snapFormWarning.value = { title: '사진을 첨부해 주세요.', body: '스냅 등록을 위해서는 최소 1장의 사진이 필요합니다.' }
      showSnapPhotoWarning.value = true
    }
  }
}

async function confirmDeleteSnap() {
  if (!selectedSnap.value) return
  if (selectedSnap.value.canEdit) {
    await client.delete(`/snaps/${selectedSnap.value.id}/`)
    apiSnaps.value = apiSnaps.value.filter((snap) => snap.id !== selectedSnap.value.id)
  }
  mySnaps.value = mySnaps.value.filter((snap) => snap.id !== selectedSnap.value.id)
  showSnapDeleteWarning.value = false
  showMoreSheet.value = false
  selectedSnap.value = null
  selectedProfileUser.value = MY_USER.value
}

async function hideSnap(snap) {
  await client.post(`/snaps/${snap.id}/hide/`)
  apiSnaps.value = apiSnaps.value.filter((item) => item.id !== snap.id)
  showMoreSheet.value = false
  selectedSnap.value = null
}

async function reportSnap(snap) {
  await client.post(`/snaps/${snap.id}/report/`)
  showMoreSheet.value = false
}

function toggleGender(gender) {
  if (productDrag.value.moved) return
  selectedGender.value = selectedGender.value === gender ? '전체' : gender
}

function resetFilters() {
  selectedGender.value = '전체'
  selectedSeasons.value = []
  selectedStyles.value = []
  selectedTpos.value = []
  heightRange.value = { min: '', max: '' }
  weightRange.value = { min: '', max: '' }
}

function patchSnap(id, patch) {
  const apply = (snap) => snap.id === id ? { ...snap, ...patch } : snap
  apiSnaps.value = apiSnaps.value.map(apply)
  mySnaps.value = mySnaps.value.map(apply)
  if (selectedSnap.value?.id === id) selectedSnap.value = apply(selectedSnap.value)
  detailSourceSnaps.value = detailSourceSnaps.value?.map(apply) || null
}

function normalizeComment(item) {
  return {
    id: item.id,
    author: item.username || '피클러',
    avatar: profileAvatarSource(item) || profileAvatarSource(item.user || {}) || '',
    body: item.body || '',
    time: relativeTime(item.created_at),
    canEdit: Boolean(item.can_edit),
  }
}

async function loadComments(snapId) {
  if (!snapId) return
  commentsLoading.value = true
  try {
    const { data } = await client.get(`/snaps/${snapId}/comments/`)
    const rows = (data.results || data || []).map(normalizeComment)
    commentRows.value = {
      ...commentRows.value,
      [snapId]: rows,
    }
    patchSnap(snapId, {
      comments: rows.length,
      ...(rows.length ? { comment: rows[rows.length - 1] } : {}),
    })
  } finally {
    commentsLoading.value = false
  }
}

async function openComments(snap) {
  selectedSnap.value = snap
  commentDraft.value = ''
  showComments.value = true
  await loadComments(snap.id)
}

async function submitComment() {
  const body = commentDraft.value.trim()
  const snapId = currentSnap.value.id
  if (!body || !snapId) return
  const { data } = await client.post(`/snaps/${snapId}/comments/`, { body })
  const nextComment = normalizeComment(data)
  const nextRows = [...(commentRows.value[snapId] || []), nextComment]
  commentRows.value = {
    ...commentRows.value,
    [snapId]: nextRows,
  }
  commentDraft.value = ''
  patchSnap(snapId, {
    comments: nextRows.length,
    comment: nextComment,
  })
}

function openCommentMenu(comment) {
  if (!comment.canEdit) return
  selectedComment.value = comment
  showCommentMoreSheet.value = true
}

async function editSelectedComment() {
  if (!selectedComment.value) return
  editingCommentId.value = selectedComment.value.id
  editingCommentBody.value = selectedComment.value.body
  showCommentMoreSheet.value = false
}

function cancelCommentEdit() {
  editingCommentId.value = null
  editingCommentBody.value = ''
}

async function saveEditingComment() {
  if (!editingCommentId.value) return
  const nextBody = editingCommentBody.value.trim()
  if (!nextBody) return
  const snapId = currentSnap.value.id
  const { data } = await client.patch(
    `/snaps/${snapId}/comments/${editingCommentId.value}/`,
    { body: nextBody },
  )
  const updated = normalizeComment(data)
  const rows = (commentRows.value[snapId] || []).map((comment) => (
    comment.id === updated.id ? updated : comment
  ))
  commentRows.value = { ...commentRows.value, [snapId]: rows }
  patchSnap(snapId, {
    comments: rows.length,
    comment: rows[rows.length - 1] || { author: '', body: '', time: '' },
  })
  editingCommentId.value = null
  editingCommentBody.value = ''
  selectedComment.value = null
}

async function deleteSelectedComment() {
  if (!selectedComment.value) return
  const snapId = currentSnap.value.id
  await client.delete(`/snaps/${snapId}/comments/${selectedComment.value.id}/`)
  const rows = (commentRows.value[snapId] || []).filter((comment) => (
    comment.id !== selectedComment.value.id
  ))
  commentRows.value = { ...commentRows.value, [snapId]: rows }
  patchSnap(snapId, {
    comments: rows.length,
    comment: rows[rows.length - 1] || { author: '', body: '', time: '' },
  })
  showCommentMoreSheet.value = false
  selectedComment.value = null
}

async function toggleLike(id) {
  const next = new Set(liked.value)
  const wasLiked = next.has(id)
  if (wasLiked) next.delete(id)
  else next.add(id)
  liked.value = next
  const current = allSnaps.value.find((snap) => snap.id === id)
  patchSnap(id, { likes: Math.max(0, (current?.likes || 0) + (wasLiked ? -1 : 1)) })

  try {
    const { data } = await client.post(`/snaps/${id}/like/`)
    const synced = new Set(liked.value)
    if (data.is_liked) synced.add(id)
    else synced.delete(id)
    liked.value = synced
    patchSnap(id, { likes: data.like_count })
  } catch {
    const rollback = new Set(liked.value)
    if (wasLiked) rollback.add(id)
    else rollback.delete(id)
    liked.value = rollback
    patchSnap(id, { likes: current?.likes || 0 })
  }
}

async function toggleScrap(id) {
  const optimistic = new Set(scrapped.value)
  const wasScrapped = optimistic.has(id)
  if (wasScrapped) optimistic.delete(id)
  else optimistic.add(id)
  scrapped.value = optimistic

  try {
    const { data } = await client.post(`/snaps/${id}/scrap/`)
    const next = new Set(scrapped.value)
    if (data.is_scrapped) next.add(id)
    else next.delete(id)
    scrapped.value = next
  } catch {
    const rollback = new Set(scrapped.value)
    if (wasScrapped) rollback.add(id)
    else rollback.delete(id)
    scrapped.value = rollback
  }
}

async function toggleFollow(username) {
  if (!username || username === MY_USER.value) return
  const next = new Set(following.value)
  const wasFollowing = next.has(username)
  if (wasFollowing) next.delete(username)
  else next.add(username)
  following.value = next
  try {
    const { data } = await client.post(`/snaps/profiles/${username}/follow/`)
    if (data.is_following) next.add(username)
    else next.delete(username)
    following.value = new Set(next)
    const changedBy = data.is_following ? 1 : -1
    const currentMyProfile = profileRows.value[MY_USER.value] || { user: MY_USER.value, posts: selectedProfileSnaps.value.length, followers: 0, following: 0 }
    const nextFollowingCount = Math.max(0, (Number(currentMyProfile.following) || 0) + changedBy)
    profileRows.value = {
      ...profileRows.value,
      [MY_USER.value]: {
        ...currentMyProfile,
        following: nextFollowingCount,
      },
    }
    if (userStore.profile) {
      userStore.profile = {
        ...userStore.profile,
        following_count: nextFollowingCount,
      }
    }
    followListRows.value = followListRows.value
      .map((user) => user.user === username ? { ...user, isFollowing: data.is_following } : user)
      .filter((user) => followListUser.value !== MY_USER.value || followListTab.value !== 'following' || data.is_following || user.user !== username)
    if (profileRows.value[username]) {
      profileRows.value = {
        ...profileRows.value,
        [username]: {
          ...profileRows.value[username],
          followers: data.follower_count,
          isFollowing: data.is_following,
        },
      }
    }
    apiMembers.value = apiMembers.value.map((member) => (
      member.user === username
        ? { ...member, followers: data.follower_count }
        : member
    ))
  } catch {
    if (wasFollowing) next.add(username)
    else next.delete(username)
    following.value = new Set(next)
  }
}

async function openDetail(snap, sourceSnaps = null, returnProfileUser = null) {
  transitionName.value = 'slide-forward'
  showFollowList.value = false
  detailSourceSnaps.value = sourceSnaps ? [...sourceSnaps] : null
  detailReturnProfileUser.value = returnProfileUser
  selectedSnap.value = snap
  selectedSnapId.value = snap.id
  photoIndexes.value = { ...photoIndexes.value, [snap.id]: photoPosition(snap) }
  await nextTick()
  document.querySelector('.screen')?.scrollTo({ top: 0, behavior: 'instant' })
  detailPage.value?.scrollTo({ top: 0, behavior: 'instant' })
}

function backToFeed() {
  transitionName.value = 'slide-back'
  if (detailReturnProfileUser.value) {
    selectedProfileUser.value = detailReturnProfileUser.value
    selectedSnap.value = null
    selectedSnapId.value = null
    detailReturnProfileUser.value = null
    detailSourceSnaps.value = null
    showComments.value = false
    showMoreSheet.value = false
    showProfileMoreSheet.value = false
    selectedViewerSnap.value = null
    showPhotoViewer.value = false
    showPhotoViewerCount.value = true
    return
  }
  selectedSnap.value = null
  selectedProfileUser.value = null
  detailSourceSnaps.value = null
  detailReturnProfileUser.value = null
  showComments.value = false
  showMoreSheet.value = false
  showProfileMoreSheet.value = false
  selectedViewerSnap.value = null
  showPhotoViewer.value = false
  showPhotoViewerCount.value = true
}

async function openUserProfile(user) {
  transitionName.value = 'slide-forward'
  showFollowList.value = false
  selectedProfileUser.value = user
  showMoreSheet.value = false
  showComments.value = false
  await loadSnapProfile(user)
}

function closeUserProfile() {
  transitionName.value = 'slide-back'
  showFollowList.value = false
  selectedProfileUser.value = null
  showProfileMoreSheet.value = false
  if (route.query.profile) {
    router.replace({ name: 'lookbook' })
  }
}

function openProfileSnap(snap) {
  openDetail(snap, selectedProfileSnaps.value, selectedProfileUser.value)
  selectedProfileUser.value = null
}

function selectSort(key) {
  sortBy.value = key
  showSortMenu.value = false
}

function selectRankingPeriod(key) {
  rankingPeriod.value = key
  showRankingPeriodMenu.value = false
}

function nextPhoto(step) {
  const total = currentSnap.value.photos.length
  photoIndex.value = (photoIndex.value + step + total) % total
}

function nextPhotoFor(snap, step) {
  const total = snap.photos.length
  const next = (photoPosition(snap) + step + total) % total
  photoIndexes.value = { ...photoIndexes.value, [snap.id]: next }
  if (snap.id === currentSnap.value.id) photoIndex.value = next
}

function nextViewerPhoto(step) {
  const total = viewerSnap.value.photos.length
  photoIndex.value = (photoIndex.value + step + total) % total
}

function beginPhotoSwipe(event, snap) {
  event.currentTarget.setPointerCapture?.(event.pointerId)
  photoSwipe.value = { x: event.clientX, moved: false, snap }
}

function endPhotoSwipe(event) {
  const { x, snap } = photoSwipe.value
  if (!snap) return
  const diff = event.clientX - x
  if (Math.abs(diff) > 38) {
    photoSwipe.value.moved = true
    if (showPhotoViewer.value) nextViewerPhoto(diff < 0 ? 1 : -1)
    else nextPhotoFor(snap, diff < 0 ? 1 : -1)
  }
}

function handlePhotoWheel(event, snap) {
  if (Math.abs(event.deltaX) < 24 || Math.abs(event.deltaX) < Math.abs(event.deltaY)) return
  event.preventDefault()
  if (photoWheelLocked.value) return
  photoWheelLocked.value = true
  if (showPhotoViewer.value) nextViewerPhoto(event.deltaX > 0 ? 1 : -1)
  else nextPhotoFor(snap, event.deltaX > 0 ? 1 : -1)
  window.setTimeout(() => {
    photoWheelLocked.value = false
  }, 420)
}

function beginProductDrag(event) {
  if (event.pointerType === 'mouse' && event.button !== 0) return
  productDrag.value = { x: event.clientX, left: event.currentTarget.scrollLeft, target: event.currentTarget, moved: false }
}

function moveProductDrag(event) {
  if (!productDrag.value.target) return
  if (event.pointerType === 'mouse' && event.buttons !== 1) {
    endProductDrag()
    return
  }
  const distance = event.clientX - productDrag.value.x
  if (Math.abs(distance) > 4) productDrag.value.moved = true
  productDrag.value.target.scrollLeft = productDrag.value.left - distance
}

function endProductDrag() {
  const moved = productDrag.value.moved
  productDrag.value = { x: 0, left: 0, target: null, moved }
  window.setTimeout(() => {
    productDrag.value = { x: 0, left: 0, target: null, moved: false }
  }, 0)
}

function openMemberSnap(snap, sourceSnaps) {
  if (productDrag.value.moved) return
  productDrag.value = { x: 0, left: 0, target: null, moved: false }
  openDetail(snap, sourceSnaps)
}

function openPhotoViewer(snap) {
  selectedViewerSnap.value = snap
  photoIndex.value = photoPosition(snap)
  showPhotoViewerCount.value = snap.photos.length > 1
  showPhotoViewer.value = true
}

function openClothingPhotoViewer(item) {
  if (productDrag.value.moved) return
  const source = clothingImageSource(item)
  if (!source) return
  selectedViewerSnap.value = {
    id: `clothing-${item.id || item.name || Date.now()}`,
    type: 'clothing',
    photos: [source],
  }
  photoIndex.value = 0
  showPhotoViewerCount.value = false
  showPhotoViewer.value = true
}

function openProfilePhotoViewer() {
  const source = selectedProfile.value.avatarPhoto
  if (!isAvatarImageSource(source)) return
  selectedViewerSnap.value = {
    id: `profile-${selectedProfile.value.user}`,
    photos: [source],
  }
  photoIndex.value = 0
  showPhotoViewerCount.value = false
  showPhotoViewer.value = true
}

function openPhotoViewerFromClick(snap) {
  if (photoSwipe.value.moved) {
    photoSwipe.value = { x: 0, moved: false, snap: null }
    return
  }
  photoSwipe.value = { x: 0, moved: false, snap: null }
  openPhotoViewer(snap)
}

function closePhotoViewer() {
  if (typeof viewerSnap.value.id !== 'string') {
    photoIndexes.value = { ...photoIndexes.value, [viewerSnap.value.id]: photoIndex.value }
  }
  selectedViewerSnap.value = null
  showPhotoViewer.value = false
  showPhotoViewerCount.value = true
}

function scrollToTop() {
  detailPage.value?.scrollTo({ top: 0, behavior: 'smooth' })
  detailPage.value?.closest('.screen')?.scrollTo({ top: 0, behavior: 'smooth' })
}

async function goSnapMain() {
  activeTab.value = 'snap'
  transitionName.value = 'slide-back'
  selectedSnap.value = null
  selectedProfileUser.value = null
  detailReturnProfileUser.value = null
  showComments.value = false
  showMoreSheet.value = false
  showProfileMoreSheet.value = false
  selectedViewerSnap.value = null
  showPhotoViewer.value = false
  if (route.query.profile) {
    await router.replace({ name: 'lookbook' })
  }
  await nextTick()
  document.querySelector('.screen')?.scrollTo({ top: 0, behavior: 'smooth' })
}

watch(
  [activeTab, selectedGender, selectedSeasons, selectedStyles, selectedTpos, sortBy],
  () => {
    if (apiLoaded.value) loadSnaps()
    if (activeTab.value === 'ranking') loadMemberRanking()
  },
  { deep: true },
)

onMounted(async () => {
  window.addEventListener('pickle:snap-main', goSnapMain)
  if (!userStore.profile) {
    try {
      await userStore.loadProfile()
    } catch {
      // 로그인 상태가 아니면 라우터 가드가 처리한다.
    }
  }
  if (route.query.profile === 'me') {
    refreshProfileEditOverrides()
    activeTab.value = 'snap'
    selectedProfileUser.value = MY_USER.value
  }
  await loadSnaps()
  await loadMemberRanking()
  await openInitialProfileFromRoute()
  if (selectedProfileUser.value === MY_USER.value) await loadSnapProfile(MY_USER.value, { force: true })
})
onBeforeUnmount(() => window.removeEventListener('pickle:snap-main', goSnapMain))
</script>

<template>
  <PhoneFrame :show-nav="!showSnapForm && !showPictureEditor && !showClosetPicker">
    <Transition :name="transitionName" mode="out-in" :appear="route.query.profile === 'me'">
      <section v-if="showPictureEditor" class="snap-picture-editor">
        <header class="snap-create-top"><button type="button" @click="showPictureEditor = false; editingPhotoIndex = null"><span class="ui-icon" style="--icon: url('/icons/ui/angle-small-left.svg')" aria-hidden="true"></span></button><h1>사진 편집</h1></header>
        <div class="picture-thumb-row"><span class="snap-photo" :class="snapPhotoClass(draftPhotoClass)" :style="snapPhotoStyle(draftPhotoClass)"></span></div>
        <section
          class="crop-preview"
          @pointerdown="beginCropDrag"
          @pointermove="moveCropDrag"
          @pointerup="endCropDrag"
          @pointercancel="endCropDrag"
          @wheel="handleCropWheel"
        >
          <div class="snap-photo" :class="snapPhotoClass(draftPhotoClass)" :style="snapPhotoStyle(draftPhotoClass, true)"></div>
          <div class="crop-grid"></div>
        </section>
        <div class="crop-zoom-control"><span>축소</span><input :value="draftPhotoZoom" type="range" min="1" max="2.6" step="0.05" @input="updateDraftZoom($event.target.value)"><span>확대</span></div>
        <div class="rotate-actions"><button type="button" @click="rotateDraftPhoto(-90)">왼쪽 90도</button><button type="button" @click="rotateDraftPhoto(90)">오른쪽 90도</button></div>
        <button class="snap-submit-button" type="button" @click="completePictureEdit">{{ editingPhotoIndex !== null ? '편집완료' : `${snapForm.photos.length + 1}개 이미지 등록하기` }}</button>
      </section>

      <section v-else-if="showClosetPicker" class="snap-closet-picker-page">
        <header class="snap-create-top"><button type="button" @click="closeClosetPicker"><span class="ui-icon" style="--icon: url('/icons/ui/angle-small-left.svg')" aria-hidden="true"></span></button><h1>옷장 아이템 선택</h1></header>
        <div class="category-scroll padded">
          <button v-for="category in closetCategories" :key="category.key" class="category-tab" :class="{ active: selectedClosetCategory === category.key }" @click="selectedClosetCategory = category.key">{{ category.label }}</button>
        </div>
        <p v-if="closetLoading" class="empty-state">옷장을 불러오는 중...</p>
        <p v-else-if="!filteredClosetOptions.length" class="empty-state">선택할 옷이 없습니다.</p>
        <section v-else class="closet-select-grid">
          <button
            v-for="item in filteredClosetOptions"
            :key="item.id"
            type="button"
            :class="{ active: snapForm.clothes.some((clothes) => clothes.id === item.id) }"
            @click="toggleClosetItem(item)"
          >
            <span class="clothes-thumb" :style="{ '--tone': item.tone }">
              <img v-if="item.image" :src="item.image" :alt="item.name" />
              <span v-else-if="item.categoryKey === 'shoes'" class="shoe-shape"></span>
              <span v-else-if="item.categoryKey === 'bottom'" class="pants-shape"></span>
              <span v-else class="top-shape"></span>
              <i>✓</i>
            </span>
            <strong>{{ item.name }}</strong>
            <small>{{ item.category }}</small>
          </button>
        </section>
        <button class="snap-submit-button closet-picker-submit" type="button" @click="closeClosetPicker">선택한 옷 {{ snapForm.clothes.length }}개 등록하기</button>
      </section>

      <section v-else-if="showSnapForm" class="snap-create-page">
        <header class="snap-create-top"><h1>{{ editingSnapId ? '스냅 수정하기' : '스냅 작성하기' }}</h1><button type="button" @click="requestCloseSnapForm"><span class="ui-icon" style="--icon: url('/icons/ui/cross.svg')" aria-hidden="true"></span></button></header>
        <button class="snap-guide-entry" type="button" @click="showSnapGuide = true">작성 시 유의사항<span>›</span></button>
        <section class="snap-create-block">
          <h2>사진 첨부 (필수)</h2>
          <div
            class="snap-photo-uploader"
            @pointerdown="beginProductDrag"
            @pointermove="moveProductDrag"
            @pointerup="endProductDrag"
            @pointercancel="endProductDrag"
            @pointerleave="endProductDrag"
          >
            <article v-for="(photo, index) in snapForm.photos" :key="`form-photo-${index}`" class="uploaded-photo snap-photo" :class="snapPhotoClass(photo)" :style="snapPhotoStyle(photo)" @click="openPictureEditor(photo, index)"><button type="button" @click.stop="removeSnapPhoto(index)">×</button><span v-if="index === 0">대표</span></article>
            <label v-if="snapForm.photos.length < 10" class="add-photo-box" :class="{ loading: snapPhotoUploading }"><input type="file" accept="image/*" multiple @change="uploadSnapPhotos" /><strong>{{ snapPhotoUploading ? '...' : '+' }}</strong><span>{{ snapForm.photos.length }}/10</span></label>
          </div>
          <p class="ai-notice">실제 촬영한 사진만 등록해주세요. AI로 생성한 가상의 이미지는 등록이 제한될 수 있습니다.</p>
        </section>
        <section class="snap-create-block">
          <h2>옷장 아이템</h2>
          <p class="snap-create-help">내 옷장에서 이번 스냅에 함께 보여줄 옷을 골라보세요.</p>
          <button class="closet-open-button" type="button" @click="openClosetPicker">옷장 보기<span>›</span></button>
          <p v-if="!snapForm.clothes.length" class="snap-create-help closet-empty-help">선택한 옷이 없습니다.</p>
          <div v-else class="selected-closet-strip">
            <article v-for="item in snapForm.clothes" :key="item.id">
              <span class="product-thumb" :style="{ background: item.tone }"></span>
              <div><strong>{{ item.category }}</strong><p>{{ item.name }}</p></div>
              <button type="button" aria-label="선택 해제" @click="toggleClosetItem(item)">×</button>
            </article>
          </div>
        </section>
        <section class="snap-create-block">
          <h2>본문 입력</h2>
          <div class="snap-body-box"><textarea v-model="snapForm.body" maxlength="2000" placeholder="착용한 #아이템 및 스타일을 소개해 주세요."></textarea><div @pointerdown="beginProductDrag" @pointermove="moveProductDrag" @pointerup="endProductDrag" @pointercancel="endProductDrag" @pointerleave="endProductDrag"><button v-for="tag in createTags" :key="tag" type="button" @click="appendBodyTag(tag)">{{ tag }}</button></div></div>
          <small>{{ snapForm.body.length }}/2,000</small>
        </section>
        <section class="snap-create-list">
          <button type="button" @click="showSnapBodySheet = true"><strong>체형 정보</strong><span>{{ snapFormBodyLabel }}</span><i>›</i></button>
          <button type="button" @click="showSnapToneSheet = true"><strong>피부 톤</strong><span>{{ snapForm.tone || '피부 톤을 입력해 주세요.' }}</span><i>›</i></button>
        </section>
        <section class="snap-choice-section">
          <h2>성별</h2><div><button v-for="gender in ['여성', '남성']" :key="gender" type="button" :class="{ active: snapForm.gender === gender }" @click="toggleSingleForm('gender', gender)">{{ gender }}</button></div>
          <h2>계절 (최대 2개)</h2><div><button v-for="season in seasons" :key="season" type="button" :class="{ active: snapForm.seasons.includes(season) }" @click="toggleLimitedForm('seasons', season)">{{ season }}</button></div>
          <h2>스타일 태그 (최대 2개)</h2><div><button v-for="style in styles.filter((item) => item !== '전체')" :key="style" type="button" :class="{ active: snapForm.styles.includes(style) }" @click="toggleLimitedForm('styles', style)">{{ style }}</button></div>
          <h2>TPO (최대 2개)</h2><div><button v-for="tpo in ['데일리', '데이트', '캠퍼스', '출근', '결혼식', '피트니스', '러닝', '여행', '골프']" :key="tpo" type="button" :class="{ active: snapForm.tpos.includes(tpo) }" @click="toggleLimitedForm('tpos', tpo)">{{ tpo }}</button></div>
        </section>
        <button class="snap-submit-button" type="button" @click="submitSnapForm">{{ editingSnapId ? '수정하기' : '등록하기' }}</button>
      </section>

      <section v-else-if="showFollowList" class="follow-list-page">
        <header class="follow-list-top"><button type="button" @click="closeFollowList"><span class="ui-icon" style="--icon: url('/icons/ui/angle-small-left.svg')" aria-hidden="true"></span></button><h1>피클러</h1><span></span></header>
        <nav class="follow-list-tabs"><button type="button" :class="{ active: followListTab === 'followers' }" @click="openFollowList('followers')">팔로워</button><button type="button" :class="{ active: followListTab === 'following' }" @click="openFollowList('following')">팔로잉</button></nav>
        <p class="follow-list-count">{{ followListLoading ? '불러오는 중' : `${followListRows.length}명` }}</p>
        <section class="follow-list-items">
          <article v-for="user in followListRows" :key="`follow-${followListTab}-${user.user}`">
            <button class="follow-list-user" type="button" @click="openFollowUser(user.user)"><span class="rank-avatar" :style="avatarStyle(avatarForUser(user.user, user.avatar))"></span><strong>{{ user.user }}</strong></button>
            <button v-if="user.user !== MY_USER" class="follow-button" type="button" :class="{ active: following.has(user.user) }" @click="toggleFollow(user.user)">{{ following.has(user.user) ? '팔로잉' : '+ 팔로우' }}</button>
          </article>
          <p v-if="!followListLoading && !followListRows.length" class="follow-list-empty">표시할 피클러가 없습니다.</p>
        </section>
      </section>

      <section v-else-if="!selectedSnap && !selectedProfileUser" class="snap-page">
        <header class="snap-topbar">
          <div class="snap-logo"><img src="/images/snap_logo.png" alt="snap" /></div>
        </header>

        <nav class="snap-tabs">
          <button v-for="tab in tabs" :key="tab.key" type="button" :class="{ active: activeTab === tab.key }" @click="activeTab = tab.key">{{ tab.label }}</button>
          <button class="snap-my-profile-button" type="button" aria-label="내 프로필" @click="openMyProfile"><span class="snap-profile" :style="myProfileAvatarStyle"></span></button>
        </nav>

        <template v-if="activeTab === 'snap' || activeTab === 'scrap'">
          <section class="snap-filter-strip" @pointerdown="beginProductDrag" @pointermove="moveProductDrag" @pointerup="endProductDrag" @pointercancel="endProductDrag" @pointerleave="endProductDrag">
            <button class="icon-filter-button" type="button" aria-label="필터" @click="openFilter('gender')"><span class="ui-icon" style="--icon: url('/icons/ui/filter.svg')" aria-hidden="true"></span></button>
            <button type="button" :class="{ active: selectedGender === '남성' }" @click="toggleGender('남성')">남</button>
            <button type="button" :class="{ active: selectedGender === '여성' }" @click="toggleGender('여성')">여</button>
            <button type="button" :class="{ active: selectedSeasons.length }" @click="openFilter('season')">계절<span class="ui-icon" style="--icon: url('/icons/ui/angle-small-down.svg')" aria-hidden="true"></span></button>
            <button type="button" :class="{ active: selectedStyles.length }" @click="openFilter('style')">스타일<span class="ui-icon" style="--icon: url('/icons/ui/angle-small-down.svg')" aria-hidden="true"></span></button>
            <button type="button" :class="{ active: heightRange.min || heightRange.max || weightRange.min || weightRange.max }" @click="openFilter('body')">키/몸무게<span class="ui-icon" style="--icon: url('/icons/ui/angle-small-down.svg')" aria-hidden="true"></span></button>
            <button type="button" :class="{ active: selectedTpos.length }" @click="openFilter('tpo')">TPO<span class="ui-icon" style="--icon: url('/icons/ui/angle-small-down.svg')" aria-hidden="true"></span></button>
          </section>
          <div class="snap-count-row selected-filter-row"><div><button v-for="chip in activeFilterChips" :key="`${chip.type}-${chip.value}`" type="button" @click="removeFilterChip(chip)">{{ chip.value }} ×</button></div><button type="button" @click="resetFilters">초기화</button></div>
          <div class="snap-count-row strong"><strong>{{ snapCount.toLocaleString() }}개</strong><div class="sort-menu-wrap"><button type="button" @click="showSortMenu = !showSortMenu">{{ sortLabel }}<span class="ui-icon" style="--icon: url('/icons/ui/angle-small-down.svg')" aria-hidden="true"></span></button><div v-if="showSortMenu" class="sort-menu"><button v-for="option in sortOptions" :key="option.key" type="button" :class="{ active: sortBy === option.key }" @click="selectSort(option.key)">{{ option.label }}</button></div></div></div>

          <section v-if="snapLoadError" class="scrap-empty"><h2>스냅을 불러오지 못했어요</h2><p>{{ snapLoadError }}</p><button class="snap-empty-action" type="button" @click="loadSnaps">다시 시도</button></section>
          <section v-else-if="!apiLoaded" class="scrap-empty"><h2>스냅을 불러오는 중...</h2><p>잠시만 기다려 주세요.</p></section>
          <section v-else-if="activeTab === 'scrap' && !feedSnaps.length" class="scrap-empty"><span class="ui-icon" style="--icon: url('/icons/ui/scrap.svg')" aria-hidden="true"></span><h2>저장한 스냅이 없어요</h2><p>마음에 드는 스냅을 스크랩하면 여기에서 모아볼 수 있어요.</p></section>
          <section v-else-if="!feedSnaps.length" class="scrap-empty"><h2>등록된 스냅이 없어요</h2><p>조건에 맞는 스냅이 아직 없습니다.</p></section>
          <section v-else class="snap-grid">
            <article v-for="snap in feedSnaps" :key="snap.id" class="snap-tile" :data-snap-id="snap.id" @click="openDetail(snap)">
              <div class="snap-photo" :class="snapPhotoClass(snap.image)" :style="snapPhotoStyle(snap.image)"><button class="snap-save-button" type="button" aria-label="스크랩" :class="{ active: scrapped.has(snap.id) }" @click.stop="toggleScrap(snap.id)"><span class="ui-icon" :style="{ '--icon': `url(${scrapped.has(snap.id) ? '/icons/ui/filled_scrap.svg' : '/icons/ui/scrap.svg'})` }" aria-hidden="true"></span></button></div>
            </article>
          </section>
        </template>

        <template v-else-if="activeTab === 'ranking'">
          <section v-if="rankingScope === 'snap'" class="snap-filter-strip" @pointerdown="beginProductDrag" @pointermove="moveProductDrag" @pointerup="endProductDrag" @pointercancel="endProductDrag" @pointerleave="endProductDrag">
            <button class="icon-filter-button" type="button" aria-label="필터" @click="openFilter('gender')"><span class="ui-icon" style="--icon: url('/icons/ui/filter.svg')" aria-hidden="true"></span></button>
            <button type="button" :class="{ active: selectedGender === '남성' }" @click="toggleGender('남성')">남</button>
            <button type="button" :class="{ active: selectedGender === '여성' }" @click="toggleGender('여성')">여</button>
            <button type="button" :class="{ active: selectedSeasons.length }" @click="openFilter('season')">계절<span class="ui-icon" style="--icon: url('/icons/ui/angle-small-down.svg')" aria-hidden="true"></span></button>
            <button type="button" :class="{ active: selectedStyles.length }" @click="openFilter('style')">스타일<span class="ui-icon" style="--icon: url('/icons/ui/angle-small-down.svg')" aria-hidden="true"></span></button>
            <button type="button" @click="openFilter('body')">키/몸무게<span class="ui-icon" style="--icon: url('/icons/ui/angle-small-down.svg')" aria-hidden="true"></span></button>
            <button type="button" :class="{ active: selectedTpos.length }" @click="openFilter('tpo')">TPO<span class="ui-icon" style="--icon: url('/icons/ui/angle-small-down.svg')" aria-hidden="true"></span></button>
          </section>
          <div v-if="rankingScope === 'snap'" class="snap-count-row selected-filter-row ranking-filter-row"><div><button v-for="chip in activeFilterChips" :key="`ranking-${chip.type}-${chip.value}`" type="button" @click="removeFilterChip(chip)">{{ chip.value }} ×</button></div><button type="button" @click="resetFilters">초기화</button></div>
          <div class="snap-subtabs ranking-scope-tabs"><button type="button" :class="{ active: rankingScope === 'snap' }" @click="rankingScope = 'snap'">스냅</button><button type="button" :class="{ active: rankingScope === 'member' }" @click="rankingScope = 'member'">피클러</button></div>
          <div class="snap-rank-meta"><span>{{ rankingBaseTime }}</span><div class="sort-menu-wrap"><button type="button" @click="showRankingPeriodMenu = !showRankingPeriodMenu">{{ rankingPeriodLabel }}<span class="ui-icon" style="--icon: url('/icons/ui/angle-small-down.svg')" aria-hidden="true"></span></button><div v-if="showRankingPeriodMenu" class="sort-menu ranking-period-menu"><button v-for="option in rankingPeriodOptions" :key="option.key" type="button" :class="{ active: rankingPeriod === option.key }" @click="selectRankingPeriod(option.key)">{{ option.label }}</button></div></div></div>
          <section v-if="rankingScope === 'snap'" class="ranking-grid">
            <article v-for="(snap, index) in rankedSnaps" :key="snap.id" class="ranking-card" :data-snap-id="snap.id" @click="openDetail(snap)">
              <div class="rank-photo snap-photo" :class="snapPhotoClass(snap.image)" :style="snapPhotoStyle(snap.image)"><b>{{ index + 1 }}</b><button class="snap-save-button" type="button" aria-label="스크랩" :class="{ active: scrapped.has(snap.id) }" @click.stop="toggleScrap(snap.id)"><span class="ui-icon" :style="{ '--icon': `url(${scrapped.has(snap.id) ? '/icons/ui/filled_scrap.svg' : '/icons/ui/scrap.svg'})` }" aria-hidden="true"></span></button></div>
              <div class="rank-copy"><strong><span class="rank-avatar" :style="avatarStyle(avatarForUser(snap.user, snap.avatar))"></span>{{ snap.user }}</strong><p>#{{ snap.tags.join(' #') }}</p><small>♡{{ snap.likes.toLocaleString() }}  댓글 {{ snap.comments }}</small></div>
            </article>
          </section>
          <section v-else class="member-ranking-list">
            <article v-for="(member, index) in displayRankedMembers" :key="member.user" class="member-rank-card">
              <header>
                <div class="member-rank-index"><strong>{{ index + 1 }}</strong><span :class="member.trend"><i class="ui-icon" :style="{ '--icon': `url(/icons/ui/caret-${member.trend === 'up' ? 'up' : 'down'}.svg)` }" aria-hidden="true"></i>{{ member.change }}</span></div>
                <button class="member-profile-button" type="button" @click="openUserProfile(member.user)"><span class="rank-avatar" :style="avatarStyle(avatarForUser(member.user, member.avatar))"></span></button>
                <button class="member-name-button" type="button" @click="openUserProfile(member.user)"><strong>{{ member.user }}</strong><small>팔로워 {{ member.followers.toLocaleString() }}</small></button>
                <button v-if="!member.isMe" class="follow-button" type="button" :class="{ active: following.has(member.user) }" @click="toggleFollow(member.user)">{{ following.has(member.user) ? '팔로잉' : '+ 팔로우' }}</button>
              </header>
              <div class="member-snap-strip" @pointerdown="beginProductDrag" @pointermove="moveProductDrag" @pointerup="endProductDrag" @pointercancel="endProductDrag" @pointerleave="endProductDrag">
                <button v-for="snap in member.snaps" :key="`${member.user}-${snap.id}`" type="button" class="member-snap-photo snap-photo" :class="snapPhotoClass(snap.image)" :style="snapPhotoStyle(snap.image)" @click.stop="openMemberSnap(snap, member.snaps)"></button>
              </div>
            </article>
          </section>
        </template>

        <template v-else>
          <section v-if="!followingRecommendations.length" class="following-empty"><h2>관심있는 유저를 팔로우해보세요.</h2><p>팔로우한 유저의 스냅을 모아볼 수 있어요.</p></section>
          <section v-for="snap in followingRecommendations" :key="`following-${snap.id}`" class="following-card" @click="openDetail(snap)">
            <header><span class="rank-avatar following-avatar" :style="avatarStyle(avatarForUser(snap.user, snap.avatar))"></span><strong>{{ snap.user }}</strong><button type="button" @click.stop="toggleFollow(snap.user)">{{ following.has(snap.user) ? '팔로잉' : '+ 팔로우' }}</button></header>
            <div class="following-hero snap-photo" :class="snapPhotoClass(photoClass(snap))" :style="snapPhotoStyle(photoClass(snap))">
              <em v-if="snap.photos.length > 1">{{ photoPosition(snap) + 1 }} / {{ snap.photos.length }}</em>
              <button v-if="snap.photos.length > 1" class="enlarge-button" type="button" aria-label="사진 확대" @click.stop="openPhotoViewer(snap)"><span class="ui-icon" style="--icon: url('/icons/ui/enlarge_photo.svg')" aria-hidden="true"></span></button>
            </div>
          </section>
        </template>

        <div class="snap-gender-float"><button type="button" :class="{ active: selectedGender === '전체' }" @click="selectedGender = '전체'">전체</button><button type="button" :class="{ active: selectedGender === '남성' }" @click="toggleGender('남성')">남성</button><button type="button" :class="{ active: selectedGender === '여성' }" @click="toggleGender('여성')">여성</button></div>
        <button class="snap-floating-add" type="button" @click="openSnapCreate">+</button>
      </section>

      <section v-else-if="selectedProfileUser" class="user-profile-page">
        <header class="user-profile-top">
          <button type="button" @click="closeUserProfile"><span class="ui-icon" style="--icon: url('/icons/ui/angle-small-left.svg')" aria-hidden="true"></span></button>
          <h1>{{ selectedProfile.user }}</h1>
          <button class="menu-dot-button" type="button" aria-label="더보기" @click="showProfileMoreSheet = true"><span class="ui-icon" style="--icon: url('/icons/ui/menu-dots.svg')" aria-hidden="true"></span></button>
        </header>
        <section class="user-profile-summary">
          <button class="user-profile-avatar-button" type="button" aria-label="프로필 사진 확대" :disabled="!selectedProfile.avatarPhoto" @click="openProfilePhotoViewer"><span class="user-profile-avatar" :style="avatarStyle(selectedProfile.avatar)"></span></button>
          <dl>
            <div><dt>{{ selectedProfile.posts }}</dt><dd>게시물</dd></div>
            <button type="button" @click="openFollowList('followers')"><dt>{{ selectedProfile.followers.toLocaleString() }}</dt><dd>팔로워</dd></button>
            <button type="button" @click="openFollowList('following')"><dt>{{ selectedProfile.following }}</dt><dd>팔로잉</dd></button>
          </dl>
          <div class="user-profile-copy">
            <strong>{{ selectedProfile.user }}</strong>
            <p>{{ selectedProfile.meta }}</p>
            <div v-if="selectedProfile.isMe && editingProfileBio" class="profile-bio-edit"><textarea v-model="profileBioDraft" maxlength="80" placeholder="좋아하는 스타일이나 브랜드로 간단한 소개를 적어보세요." /><footer><button type="button" @click="cancelProfileBioEdit">취소</button><button type="button" @click="saveProfileBio">저장</button></footer></div>
            <button v-else-if="selectedProfile.isMe" class="profile-bio-button" type="button" @click="startProfileBioEdit">{{ selectedProfile.bio }}</button>
            <p v-else>{{ selectedProfile.bio }}</p>
          </div>
          <div class="user-profile-actions">
            <button v-if="selectedProfile.isMe" type="button" @click="openProfileEdit">프로필 수정</button>
            <button v-else type="button" :class="{ active: following.has(selectedProfile.user) }" @click="toggleFollow(selectedProfile.user)">{{ following.has(selectedProfile.user) ? '팔로잉' : '팔로우' }}</button>
            <button type="button">프로필 공유</button>
          </div>
        </section>
        <section v-if="selectedProfileSnaps.length" class="user-profile-grid">
          <article v-for="snap in selectedProfileSnaps" :key="`profile-${snap.id}`" class="snap-tile" @click="openProfileSnap(snap)">
            <div class="snap-photo" :class="snapPhotoClass(snap.image)" :style="snapPhotoStyle(snap.image)"><button class="snap-save-button" type="button" aria-label="스크랩" :class="{ active: scrapped.has(snap.id) }" @click.stop="toggleScrap(snap.id)"><span class="ui-icon" :style="{ '--icon': `url(${scrapped.has(snap.id) ? '/icons/ui/filled_scrap.svg' : '/icons/ui/scrap.svg'})` }" aria-hidden="true"></span></button></div>
          </article>
        </section>
        <section v-else class="profile-empty"><strong>업로드한 스냅이 없습니다.</strong><p>{{ selectedProfile.isMe ? '+버튼을 눌러 게시물을 등록해보세요' : '' }}</p></section>
        <button v-if="selectedProfile.isMe" class="snap-floating-add" type="button" @click="openSnapCreate">+</button>
      </section>

      <section v-else ref="detailPage" class="snap-detail-page">
        <header class="snap-detail-top"><button type="button" @click="backToFeed"><span class="ui-icon" style="--icon: url('/icons/ui/angle-small-left.svg')" aria-hidden="true"></span></button></header>
        <article v-for="snap in detailSnaps" :key="snap.id" class="snap-detail-post">
          <div class="snap-author" :class="{ mine: snap.user === MY_USER }"><button class="author-avatar-button" type="button" @click="openUserProfile(snap.user)"><span class="rank-avatar" :style="avatarStyle(avatarForUser(snap.user, snap.avatar))"></span></button><button class="author-name-button" type="button" @click="openUserProfile(snap.user)">{{ snap.user }}</button><button v-if="snap.user !== MY_USER" class="follow-button" type="button" :class="{ active: following.has(snap.user) }" @click="toggleFollow(snap.user)"><span v-if="following.has(snap.user)" class="ui-icon" style="--icon: url('/icons/ui/check.svg')" aria-hidden="true"></span>{{ following.has(snap.user) ? '팔로잉' : '+ 팔로우' }}</button><button class="menu-dot-button" type="button" aria-label="더보기" @click="selectedSnap = snap; showMoreSheet = true"><span class="ui-icon" style="--icon: url('/icons/ui/menu-dots.svg')" aria-hidden="true"></span></button></div>
          <section class="snap-detail-photo-frame" @pointerdown="beginPhotoSwipe($event, snap)" @pointerup="endPhotoSwipe" @pointercancel="photoSwipe = { x: 0, moved: false, snap: null }" @click="openPhotoViewerFromClick(snap)" @wheel="handlePhotoWheel($event, snap)">
            <div class="snap-photo-track" :style="{ transform: `translateX(-${photoPosition(snap) * 100}%)` }">
              <div v-for="photo in snap.photos" :key="`${snap.id}-${photo}`" class="snap-detail-photo snap-photo" :class="snapPhotoClass(photo)" :style="snapPhotoStyle(photo, true)"></div>
            </div>
            <span class="photo-meta">{{ snap.meta }}</span><em>{{ photoPosition(snap) + 1 }} / {{ snap.photos.length }}</em><button class="enlarge-button" type="button" @pointerdown.stop @pointerup.stop @click.stop="openPhotoViewer(snap)"><span class="ui-icon" style="--icon: url('/icons/ui/enlarge_photo.svg')" aria-hidden="true"></span></button>
          </section>
          <section v-if="snap.clothes.length" class="product-strip" @pointerdown="beginProductDrag" @pointermove="moveProductDrag" @pointerup="endProductDrag" @pointercancel="endProductDrag">
            <button v-for="item in snap.clothes" :key="`${item.id}-${item.name}`" type="button" class="product-card" @click="openClothingPhotoViewer(item)">
              <span class="product-thumb" :style="clothingThumbStyle(item)"></span>
              <span class="product-copy"><strong>{{ item.category }}</strong><p>{{ item.name }}</p></span>
            </button>
          </section>
          <section class="snap-detail-copy">
            <div class="snap-action-row"><button type="button" :class="{ active: liked.has(snap.id) }" @click="toggleLike(snap.id)"><span class="ui-icon" :style="{ '--icon': `url(${liked.has(snap.id) ? '/icons/ui/filled_heart.svg' : '/icons/ui/heart.svg'})` }" aria-hidden="true"></span></button><button type="button" @click="openComments(snap)"><span class="ui-icon" style="--icon: url('/icons/ui/comment.svg')" aria-hidden="true"></span></button><button type="button" :class="{ active: scrapped.has(snap.id) }" @click="toggleScrap(snap.id)"><span class="ui-icon" :style="{ '--icon': `url(${scrapped.has(snap.id) ? '/icons/ui/filled_scrap.svg' : '/icons/ui/scrap.svg'})` }" aria-hidden="true"></span></button><button type="button"><span class="ui-icon" style="--icon: url('/icons/ui/share.svg')" aria-hidden="true"></span></button></div>
            <strong>좋아요 {{ snap.likes.toLocaleString() }}개</strong>
            <p>{{ snap.body }}</p>
            <p class="snap-tags">#{{ snap.tags.join(' #') }}</p>
            <button v-if="snap.comments > 0" class="comment-more" type="button" @click="openComments(snap)">댓글 더보기</button>
            <p v-if="snap.comments > 0 && snap.comment.body" class="snap-comment-preview"><strong>{{ snap.comment.author }}</strong> {{ snap.comment.body }}</p>
            <button v-if="snap.comments > 0" class="comment-input-preview" type="button" @click="openComments(snap)"><span class="snap-profile" :style="avatarStyle(avatarForUser(MY_USER))"></span><span>댓글을 남겨주세요.</span></button>
            <small>{{ snap.daysAgo }}일 전</small>
          </section>
        </article>
        <button class="scroll-top-button" type="button" @click="scrollToTop"><span class="ui-icon" style="--icon: url('/icons/ui/angle-small-up.svg')" aria-hidden="true"></span></button>
      </section>
    </Transition>

    <Transition name="sheet">
      <div v-if="showComments" class="comment-backdrop" @click="showComments = false"></div>
    </Transition>
    <Transition name="sheet">
      <aside v-if="showComments" class="comment-sheet">
        <button class="sheet-handle" type="button" aria-label="댓글 닫기" @click="showComments = false"></button>
        <h2>{{ currentComments.length }}개의 댓글</h2>
        <p v-if="commentsLoading" class="comment-empty">댓글을 불러오는 중입니다.</p>
        <p v-else-if="!currentComments.length" class="comment-empty">아직 댓글이 없습니다. 첫 댓글을 남겨보세요.</p>
        <template v-else>
          <article v-for="comment in currentComments" :key="comment.id"><span class="snap-profile" :style="avatarStyle(avatarForUser(comment.author, comment.avatar))"></span><div><strong>{{ comment.author }} <small>{{ comment.time }}</small></strong><div v-if="editingCommentId === comment.id" class="comment-edit-box"><input v-model="editingCommentBody" aria-label="댓글 수정" @keyup.enter="saveEditingComment" /><footer><button type="button" @click="cancelCommentEdit">취소</button><button type="button" @click="saveEditingComment">저장</button></footer></div><p v-else>{{ comment.body }}</p></div><button v-if="comment.canEdit && editingCommentId !== comment.id" class="comment-menu-button" type="button" aria-label="댓글 더보기" @click="openCommentMenu(comment)">...</button></article>
        </template>
        <label><input v-model="commentDraft" placeholder="댓글을 남겨주세요." @keyup.enter="submitComment" /><button type="button" @click="submitComment">↑</button></label>
      </aside>
    </Transition>

    <Transition name="sheet">
      <div v-if="showCommentMoreSheet" class="comment-backdrop" @click="showCommentMoreSheet = false"></div>
    </Transition>
    <Transition name="sheet">
      <aside v-if="showCommentMoreSheet" class="snap-more-sheet comment-more-sheet">
        <header><h2>댓글 더보기</h2><button type="button" @click="showCommentMoreSheet = false"><span class="ui-icon" style="--icon: url('/icons/ui/cross.svg')" aria-hidden="true"></span></button></header>
        <button type="button" @click="editSelectedComment">댓글 수정하기</button>
        <button type="button" @click="deleteSelectedComment">댓글 삭제하기</button>
      </aside>
    </Transition>

    <Transition name="sheet">
      <div v-if="showMoreSheet" class="comment-backdrop" @click="showMoreSheet = false"></div>
    </Transition>
    <Transition name="sheet">
      <aside v-if="showMoreSheet" class="snap-more-sheet">
        <header><h2>더보기</h2><button type="button" @click="showMoreSheet = false"><span class="ui-icon" style="--icon: url('/icons/ui/cross.svg')" aria-hidden="true"></span></button></header>
        <template v-if="currentSnap.user === MY_USER">
          <button type="button" @click="openSnapEdit(currentSnap)">수정하기</button>
          <button type="button" @click="showSnapDeleteWarning = true">삭제하기</button>
        </template>
        <template v-else>
          <button type="button" @click="reportSnap(currentSnap)">해당 글 신고하기</button>
          <button type="button" @click="hideSnap(currentSnap)">해당 글 숨기기</button>
        </template>
      </aside>
    </Transition>

    <Transition name="sheet">
      <div v-if="showProfileMoreSheet" class="comment-backdrop" @click="showProfileMoreSheet = false"></div>
    </Transition>
    <Transition name="sheet">
      <aside v-if="showProfileMoreSheet" class="snap-more-sheet">
        <header><h2>더보기</h2><button type="button" @click="showProfileMoreSheet = false"><span class="ui-icon" style="--icon: url('/icons/ui/cross.svg')" aria-hidden="true"></span></button></header>
        <button type="button">이 프로필 신고하기</button>
        <button type="button">이 프로필 숨기기</button>
      </aside>
    </Transition>

    <Transition name="sheet"><div v-if="showSnapGuide || showSnapBodySheet || showSnapToneSheet || showSnapBackWarning || showSnapDeleteWarning || showSnapPhotoWarning" class="comment-backdrop"></div></Transition>
    <Transition name="sheet">
      <aside v-if="showSnapGuide" class="snap-guide-sheet">
        <header><h2>스타일 스냅 작성 가이드</h2><button type="button" @click="showSnapGuide = false"><span class="ui-icon" style="--icon: url('/icons/ui/cross.svg')" aria-hidden="true"></span></button></header>
        <article><span class="guide-thumb snap-photo snap-photo--studio"></span><div><strong>자유롭게 스타일 공유하기</strong><p>전체적인 스타일 또는 제품 디테일을 보여주는 사진을 올려주세요.</p></div></article>
        <article><span class="guide-thumb snap-photo snap-photo--brand"></span><div><strong>상세 정보로 차별화하기</strong><p>체형이나 스타일, 퍼스널 컬러와 같은 추가 정보를 작성해보세요.</p></div></article>
        <article><span class="guide-ai">AI</span><div><strong>실제 촬영 사진만 등록하기</strong><p>AI로 생성한 가상의 이미지는 등록이 제한될 수 있어요.</p></div></article>
        <button class="snap-submit-button" type="button" @click="showSnapGuide = false">확인</button>
      </aside>
    </Transition>
    <Transition name="sheet">
      <aside v-if="showSnapBodySheet" class="snap-input-sheet">
        <header><h2>체형 정보</h2><button type="button" @click="showSnapBodySheet = false"><span class="ui-icon" style="--icon: url('/icons/ui/cross.svg')" aria-hidden="true"></span></button></header>
        <label class="body-input-field">키<div><input v-model="snapForm.height" inputmode="numeric" /><span>cm</span></div></label>
        <label class="body-input-field">몸무게<div><input v-model="snapForm.weight" inputmode="numeric" /><span>kg</span></div></label>
        <footer><button type="button" @click="snapForm.height = ''; snapForm.weight = ''">초기화</button><button type="button" @click="showSnapBodySheet = false">적용하기</button></footer>
      </aside>
    </Transition>
    <Transition name="sheet">
      <aside v-if="showSnapToneSheet" class="snap-input-sheet">
        <header><h2>피부 톤</h2><button type="button" @click="showSnapToneSheet = false"><span class="ui-icon" style="--icon: url('/icons/ui/cross.svg')" aria-hidden="true"></span></button></header>
        <section class="tone-list"><label v-for="tone in skinTones" :key="tone"><input v-model="snapForm.tone" type="radio" :value="tone" />{{ tone }}</label></section>
        <footer><button type="button" @click="snapForm.tone = ''">초기화</button><button type="button" @click="showSnapToneSheet = false">적용하기</button></footer>
      </aside>
    </Transition>
    <Transition name="sheet">
      <section v-if="showSnapBackWarning" class="snap-dialog"><div><h2>저장하지 않고 나가시겠어요?</h2><p>페이지를 떠날 경우 입력하신 정보가 모두 사라집니다.</p><footer><button type="button" @click="showSnapBackWarning = false">취소</button><button type="button" @click="closeSnapForm">확인</button></footer></div></section>
    </Transition>
    <Transition name="sheet">
      <section v-if="showSnapDeleteWarning" class="snap-dialog"><div><h2>스냅을 삭제하시겠습니까?</h2><footer><button type="button" @click="showSnapDeleteWarning = false">취소</button><button type="button" @click="confirmDeleteSnap">삭제</button></footer></div></section>
    </Transition>
    <Transition name="sheet">
      <section v-if="showSnapPhotoWarning" class="snap-dialog"><div><h2>{{ snapFormWarning.title }}</h2><p>{{ snapFormWarning.body }}</p><footer class="single"><button type="button" @click="showSnapPhotoWarning = false">확인</button></footer></div></section>
    </Transition>

    <Transition name="photo-viewer">
      <section v-if="showPhotoViewer" class="photo-viewer" :class="{ 'photo-viewer--product': viewerSnap.type === 'clothing' }" @pointerdown="beginPhotoSwipe($event, viewerSnap)" @pointerup="endPhotoSwipe" @pointercancel="photoSwipe = { x: 0, moved: false, snap: null }" @wheel="handlePhotoWheel($event, viewerSnap)">
        <button class="photo-close" type="button" @pointerdown.stop @pointerup.stop @click="closePhotoViewer"><span class="ui-icon" style="--icon: url('/icons/ui/cross.svg')" aria-hidden="true"></span></button>
        <div class="photo-viewer-image">
          <div class="snap-photo-track" :style="{ transform: `translateX(-${photoIndex * 100}%)` }">
            <div v-for="photo in viewerSnap.photos" :key="`viewer-${viewerSnap.id}-${photo}`" class="snap-photo" :class="snapPhotoClass(photo)" :style="snapPhotoStyle(photo)"></div>
          </div>
        </div>
        <span v-if="showPhotoViewerCount">{{ photoIndex + 1 }}/{{ viewerSnap.photos.length }}</span>
      </section>
    </Transition>

    <Transition name="sheet">
      <div v-if="showFilterSheet" class="comment-backdrop" @click="showFilterSheet = false"></div>
    </Transition>
    <Transition name="sheet">
      <aside v-if="showFilterSheet" class="snap-filter-sheet">
        <header><h2>필터</h2><button type="button" @click="showFilterSheet = false">×</button></header>
        <nav>
          <button v-for="tab in filterTabs" :key="tab.key" type="button" :class="{ active: activeFilter === tab.key }" @click="activeFilter = tab.key">{{ tab.label }}<small v-if="tab.key === 'gender' && selectedGender !== '전체'">1</small><small v-if="tab.key === 'season' && selectedSeasons.length">{{ selectedSeasons.length }}</small><small v-if="tab.key === 'style' && selectedStyles.length">{{ selectedStyles.length }}</small><small v-if="tab.key === 'tpo' && selectedTpos.length">{{ selectedTpos.length }}</small></button>
        </nav>
        <div class="filter-chip-row"><div><button v-for="chip in activeFilterChips" :key="`sheet-${chip.type}-${chip.value}`" type="button" @click="removeFilterChip(chip)">{{ chip.value }} ×</button></div><button type="button" @click="resetFilters">초기화</button></div>

        <section v-if="activeFilter === 'gender'" class="filter-option-list single">
          <label v-for="gender in ['전체', '남성', '여성']" :key="gender"><input v-model="selectedGender" type="radio" :value="gender" />{{ gender }}</label>
        </section>
        <section v-else-if="activeFilter === 'season'" class="filter-option-list">
          <label v-for="season in seasons" :key="season"><input type="checkbox" :checked="selectedSeasons.includes(season)" @change="toggleSeason(season)" />{{ season }}</label>
        </section>
        <section v-else-if="activeFilter === 'style'" class="filter-option-list">
          <label v-for="style in styles.filter((item) => item !== '전체')" :key="style"><input type="checkbox" :checked="selectedStyles.includes(style)" @change="toggleStyle(style)" />{{ style }}</label>
        </section>
        <section v-else-if="activeFilter === 'tpo'" class="filter-option-list">
          <label v-for="tpo in tpos" :key="tpo"><input type="checkbox" :checked="selectedTpos.includes(tpo)" @change="toggleTpo(tpo)" />{{ tpo }}</label>
        </section>
        <section v-else class="body-filter-panel">
          <div class="body-info-row"><strong>내 체형정보</strong><button type="button" aria-label="내 체형정보 사용"></button></div>
          <button class="body-info-card" type="button">175(cm) · 75(kg)<span>›</span></button>
          <div class="body-input-group">
            <h3>직접입력</h3>
            <p>키(cm)</p>
            <div><input v-model="heightRange.min" inputmode="numeric" placeholder="100" /><span>-</span><input v-model="heightRange.max" inputmode="numeric" placeholder="220" /><button type="button">적용</button></div>
            <p>몸무게(kg)</p>
            <div><input v-model="weightRange.min" inputmode="numeric" placeholder="30" /><span>-</span><input v-model="weightRange.max" inputmode="numeric" placeholder="150" /><button type="button">적용</button></div>
          </div>
        </section>
        <footer><button type="button" @click="showFilterSheet = false">{{ filterSheetCount.toLocaleString() }}개의 스냅 보기</button><button type="button" @click="resetFilters">↻ 선택 초기화하고 {{ allSnaps.length.toLocaleString() }}개의 스냅 보기</button></footer>
      </aside>
    </Transition>
  </PhoneFrame>
</template>
