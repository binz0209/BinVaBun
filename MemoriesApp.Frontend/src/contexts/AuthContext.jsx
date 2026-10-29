import { createContext, useContext, useState, useEffect, useRef, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { authService } from '../services/authService'

const AuthContext = createContext()

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [loading, setLoading] = useState(true)
  // Tracks whether the "server waking up" toast has already been shown this session.
  const wakeupToastShown = useRef(false)
  // Guards against concurrent background-validation calls.
  const validatingRef = useRef(false)

  // ── Logout helper (also called by the global 401 event) ───────────────────
  const logout = useCallback(() => {
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    wakeupToastShown.current = false
    setUser(null)
  }, [])

  // ── Listen for the global 401 event dispatched by the Axios interceptor ───
  // This lets any API call — not just the auth ones — trigger a logout without
  // knowing about React's routing.
  useEffect(() => {
    const handleForcedLogout = () => logout()
    window.addEventListener('auth-logout-required', handleForcedLogout)
    return () => window.removeEventListener('auth-logout-required', handleForcedLogout)
  }, [logout])

  // ── Initial auth hydration ─────────────────────────────────────────────────
  useEffect(() => {
    const token = localStorage.getItem('token')
    const userData = localStorage.getItem('user')

    if (!token || !userData) {
      // No stored credentials → nothing to do.
      setLoading(false)
      return
    }

    // Optimistic: show the user immediately from storage so the UI is instant.
    try {
      setUser(JSON.parse(userData))
    } catch {
      // Corrupted storage — clear and force re-login.
      logout()
      setLoading(false)
      return
    }
    setLoading(false)

    // ── Background validation ──────────────────────────────────────────────
    // Confirms the token is still valid against the live backend.
    // - Network errors (cold start, timeouts) are tolerated: we keep the
    //   optimistic user state. The Axios interceptor already retried with backoff.
    // - A genuine 401 means the token expired → logout.
    // - Any other server error (5xx after retries) → keep user logged in;
    //   they'll get an error when they try to do something that requires auth.
    const validateInBackground = async () => {
      if (validatingRef.current) return
      validatingRef.current = true

      try {
        // Warm up the backend first. This is a fire-and-forget ping: if it
        // fails we still proceed because validateToken has its own retry.
        await authService.pingBackend()

        // Validate the stored token. The Axios interceptor will retry up to
        // MAX_RETRIES times on network/5xx errors before throwing.
        await authService.validateToken()
        // Token is valid → nothing to change.
      } catch (err) {
        const status = err?.response?.status

        if (status === 401) {
          // Definitive rejection from the server → clear auth.
          logout()
        }
        // Any other error (network, 5xx after retries) → leave user logged in.
        // They'll hit auth errors on their next action instead of being
        // silently ejected because the backend was slow to wake up.
      } finally {
        validatingRef.current = false
      }
    }

    validateInBackground()
  }, [logout])

  // ── Login ──────────────────────────────────────────────────────────────────
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

  return (
    <AuthContext.Provider value={{ user, login, logout, loading, wakeupToastShown }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  return useContext(AuthContext)
}
