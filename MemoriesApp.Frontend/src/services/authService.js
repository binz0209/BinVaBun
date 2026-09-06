import axios from 'axios'

// Lấy API URL và loại bỏ BOM character nếu có
let API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'
API_URL = API_URL.replace(/^\uFEFF/, '').trim()

const api = axios.create({
  baseURL: API_URL,
  headers: {
    'Content-Type': 'application/json'
  },
  transformRequest: [(data, headers) => {
    if (data && typeof data === 'object' && !(data instanceof FormData)) {
      headers['Content-Type'] = 'application/json'
      return JSON.stringify(data)
    }
    return data
  }]
})

// ── Request interceptor: đính token + phát sự kiện nếu backend chậm ─────────
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  // Gắn timer 3 giây: nếu request chưa xong → phát event "backend-slow"
  // để App.jsx hiện toast "Server đang khởi động..."
  const slowTimer = setTimeout(() => {
    window.dispatchEvent(new CustomEvent('backend-slow'))
  }, 3000)

  // Lưu timer vào config để response interceptor có thể huỷ
  config._slowTimer = slowTimer

  return config
})

// ── Response interceptor: huỷ slow-timer + xử lý 401 ────────────────────────
api.interceptors.response.use(
  (response) => {
    // Request thành công → huỷ timer (backend đủ nhanh)
    if (response.config._slowTimer) {
      clearTimeout(response.config._slowTimer)
    }
    return response
  },
  (error) => {
    // Huỷ timer dù lỗi
    if (error.config?._slowTimer) {
      clearTimeout(error.config._slowTimer)
    }

    if (error.response?.status === 401) {
      const currentPath = window.location.pathname
      if (currentPath !== '/login') {
        localStorage.removeItem('token')
        localStorage.removeItem('user')
        window.location.href = '/login'
      }
    }
    return Promise.reject(error)
  }
)

// ── Auth service ──────────────────────────────────────────────────────────────
export const authService = {
  login: async (username, password) => {
    const response = await api.post('/auth/login', { username, password })
    return response.data
  },

  /**
   * validateToken — gọi endpoint nhẹ (GET /auth/me hoặc /auth/validate)
   * để kiểm tra token còn hạn không.
   * Chạy ngầm sau khi user đã vào app (không block UI).
   */
  validateToken: async () => {
    const response = await api.get('/auth/me')
    return response.data
  },

  /**
   * pingBackend — gọi endpoint nhẹ không cần auth để warm up backend.
   * Dùng khi mở trang Login để đánh thức Render free tier.
   */
  pingBackend: async () => {
    try {
      await api.get('/auth/ping')
      return true
    } catch {
      return false
    }
  }
}

export default api
