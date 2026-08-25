export const DAEJEON_COORDINATES = {
  latitude: 36.3504,
  longitude: 127.3845,
  locationLabel: '대전 기준',
  isFallback: true,
}

export function getCurrentCoordinates() {
  if (!navigator.geolocation) return Promise.resolve(DAEJEON_COORDINATES)

  return new Promise((resolve) => {
    navigator.geolocation.getCurrentPosition(
      ({ coords }) => resolve({
        latitude: coords.latitude,
        longitude: coords.longitude,
        locationLabel: '현재 위치',
        isFallback: false,
      }),
      () => resolve(DAEJEON_COORDINATES),
      { enableHighAccuracy: false, timeout: 5000, maximumAge: 10 * 60 * 1000 },
    )
  })
}

export function describeWeather(code) {
  if (code === 0) return { icon: '☀️', label: '맑음' }
  if ([1, 2].includes(code)) return { icon: '🌤️', label: '대체로 맑음' }
  if (code === 3) return { icon: '☁️', label: '흐림' }
  if ([45, 48].includes(code)) return { icon: '🌫️', label: '안개' }
  if ([51, 53, 55, 56, 57].includes(code)) return { icon: '🌦️', label: '이슬비' }
  if ([61, 63, 65, 66, 67, 80, 81, 82].includes(code)) return { icon: '🌧️', label: '비' }
  if ([71, 73, 75, 77, 85, 86].includes(code)) return { icon: '❄️', label: '눈' }
  if ([95, 96, 99].includes(code)) return { icon: '⛈️', label: '뇌우' }
  return { icon: '🌡️', label: '날씨 정보' }
}
