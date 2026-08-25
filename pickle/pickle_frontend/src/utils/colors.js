export const COLOR_OPTIONS = [
  { name: 'white', label: '화이트', hex: '#ffffff' },
  { name: 'ivory', label: '아이보리', hex: '#fff7e6' },
  { name: 'beige', label: '베이지', hex: '#d7bd92' },
  { name: 'yellow', label: '옐로우', hex: '#e5d565' },
  { name: 'orange', label: '오렌지', hex: '#dd9855' },
  { name: 'pink', label: '핑크', hex: '#ffd6e0' },
  { name: 'skyblue', label: '스카이블루', hex: '#d6ecff' },
  { name: 'green', label: '그린', hex: '#7ac943' },
  { name: 'mint', label: '민트', hex: '#bde89c' },
  { name: 'navy', label: '네이비', hex: '#27354d' },
  { name: 'brown', label: '브라운', hex: '#7a5d45' },
  { name: 'gray', label: '그레이', hex: '#929292' },
  { name: 'black', label: '블랙', hex: '#333333' },
  { name: 'silver', label: '실버', hex: '#c4c7ca' },
]

const COLOR_HEX = Object.fromEntries(COLOR_OPTIONS.map(({ name, hex }) => [name, hex]))

export function colorHex(name, fallback = '#ded7d0') {
  return COLOR_HEX[name] || name || fallback
}
