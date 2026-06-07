import axios from 'axios'

// Lấy API URL và loại bỏ BOM character nếu có
let API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'
// Loại bỏ BOM character và trim whitespace
API_URL = API_URL.replace(/^\uFEFF/, '').trim()

const api = axios.create({
  baseURL: API_URL,
  headers: {
    'Content-Type': 'application/json'
  },
  transformRequest: [(data, headers) => {
    // Đảm bảo data được serialize đúng
    if (data && typeof data === 'object' && !(data instanceof FormData)) {
      headers['Content-Type'] = 'application/json'
      return JSON.stringify(data)
    }
    return data
  }]
})

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// Auto-logout khi token hết hạn (401 Unauthorized)
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response && error.response.status === 401) {
      // Token hết hạn hoặc không hợp lệ
      const currentPath = window.location.pathname
      // Chỉ xử lý nếu không phải đang ở trang login (tránh loop)
      if (currentPath !== '/login') {
        localStorage.removeItem('token')
        localStorage.removeItem('user')
        window.location.href = '/login'
      }
    }
    return Promise.reject(error)
  }
)

export const authService = {
  login: async (username, password) => {
    const response = await api.post('/auth/login', { username, password })
    return response.data
  }
}

export default api

