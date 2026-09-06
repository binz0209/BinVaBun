import { createContext, useContext, useState, useEffect, useRef } from 'react'
import { authService } from '../services/authService'

const AuthContext = createContext()

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [loading, setLoading] = useState(true)
  // ref để track nếu toast "đang khởi động" đã hiện chưa (tránh hiện lại)
  const wakeupToastShown = useRef(false)

  useEffect(() => {
    const token = localStorage.getItem('token')
    const userData = localStorage.getItem('user')

    if (!token || !userData) {
      // Không có token → không cần gọi backend, redirect ngay
      setLoading(false)
      return
    }

    // ── Optimistic: hiển thị user từ localStorage ngay lập tức ──
    // User thấy UI ngay, token validation chạy ngầm bên dưới
    setUser(JSON.parse(userData))
    setLoading(false)

    // ── Background validation: xác nhận token vẫn còn hạn ──
    // Không block UI — chỉ logout âm thầm nếu token thực sự expired
    const validateInBackground = async () => {
      try {
        // Ping trước để đảm bảo backend đã thức (Render free tier)
        await authService.pingBackend()
        await authService.validateToken()
        // Token hợp lệ → không làm gì cả, user đã vào app rồi
      } catch (err) {
        if (err?.response?.status === 401) {
          // Token thực sự hết hạn → logout
          localStorage.removeItem('token')
          localStorage.removeItem('user')
          setUser(null)
        }
        // Nếu lỗi network (backend đang wake up) → giữ nguyên optimistic state
        // User vẫn xài được app, token sẽ được validate lần sau
      }
    }

    validateInBackground()
  }, [])

  const login = async (username, password) => {
    try {
      const response = await authService.login(username, password)
      localStorage.setItem('token', response.token)
      localStorage.setItem('user', JSON.stringify(response.user))
      setUser(response.user)
      return { success: true }
    } catch (error) {
      return {
        success: false,
        message: error.response?.data?.message || 'Đăng nhập thất bại'
      }
    }
  }

  const logout = () => {
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    wakeupToastShown.current = false
    setUser(null)
  }

  return (
    <AuthContext.Provider value={{ user, login, logout, loading, wakeupToastShown }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  return useContext(AuthContext)
}
