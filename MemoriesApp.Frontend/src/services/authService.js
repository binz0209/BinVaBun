import axios from 'axios'

// Strip BOM character if present (common with some env file editors)
let API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'
API_URL = API_URL.replace(/^\uFEFF/, '').trim()

// ── Cold-start constants ──────────────────────────────────────────────────────
// Render free tier can take up to ~30s to cold-start. We give it 60s total.
const REQUEST_TIMEOUT_MS = 60_000
const SLOW_BACKEND_TOAST_MS = 3_000
// HTTP status codes that indicate a transient server-side problem (cold start)
const RETRYABLE_STATUS = new Set([502, 503, 504])
const MAX_RETRIES = 3
const RETRY_BASE_DELAY_MS = 2_000  // doubles each attempt: 2s → 4s → 8s

// ── Axios instance ─────────────────────────────────────────────────────────────
const api = axios.create({
  baseURL: API_URL,
  timeout: REQUEST_TIMEOUT_MS,
  headers: { 'Content-Type': 'application/json' },
  transformRequest: [(data, headers) => {
    if (data && typeof data === 'object' && !(data instanceof FormData)) {
      headers['Content-Type'] = 'application/json'
      return JSON.stringify(data)
    }
    return data
  }]
})

// ── Helpers ───────────────────────────────────────────────────────────────────

/** Returns true if the error is a transient network / cold-start problem. */
function isTransientError(error) {
  if (!error.response) {
    // Network-level error (no response): timeout, DNS, TCP refused, etc.
    return true
  }
  return RETRYABLE_STATUS.has(error.response.status)
}

/** Wait `ms` milliseconds (used for retry backoff). */
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

// ── Request interceptor ───────────────────────────────────────────────────────
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  // Track retry state on the config object
  config._retryCount = config._retryCount ?? 0

  // Fire "backend-slow" toast after SLOW_BACKEND_TOAST_MS on the first attempt
  if (config._retryCount === 0) {
    config._slowTimer = setTimeout(() => {
      window.dispatchEvent(new CustomEvent('backend-slow'))
    }, SLOW_BACKEND_TOAST_MS)
  }

  return config
})

// ── Response interceptor ──────────────────────────────────────────────────────
api.interceptors.response.use(
  (response) => {
    // Success → cancel the "slow" toast timer
    if (response.config._slowTimer) {
      clearTimeout(response.config._slowTimer)
    }
    return response
  },
  async (error) => {
    const config = error.config

    // Cancel the slow-toast timer on any final error
    if (config?._slowTimer) {
      clearTimeout(config._slowTimer)
    }

    // ── Retry logic: transient errors (cold start / network) ─────────────────
    if (config && isTransientError(error)) {
      config._retryCount = (config._retryCount ?? 0) + 1

      if (config._retryCount <= MAX_RETRIES) {
        const delay = RETRY_BASE_DELAY_MS * Math.pow(2, config._retryCount - 1)
        console.warn(
          `[api] Transient error on ${config.url} — retry ${config._retryCount}/${MAX_RETRIES} in ${delay}ms`,
          error.response?.status ?? error.code
        )
        await sleep(delay)

        // Re-attach the token (it may have been set between retries)
        const token = localStorage.getItem('token')
        if (token) {
          config.headers.Authorization = `Bearer ${token}`
        }

        return api(config)
      }
    }

    // ── 401 handling: only clear auth when genuinely unauthorised ─────────────
    // We intentionally do NOT redirect here if the error was transient (network /
    // 502/503/504) — those have already exhausted retries above. A genuine 401
    // from the server means the JWT is invalid or expired.
    if (error.response?.status === 401) {
      const currentPath = window.location.pathname
      if (currentPath !== '/login') {
        localStorage.removeItem('token')
        localStorage.removeItem('user')
        // Use React-router navigation where possible; fall back to hard redirect.
        window.dispatchEvent(new CustomEvent('auth-logout-required'))
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
   * validateToken — calls a lightweight protected endpoint to confirm the
   * stored JWT is still valid. Runs in the background after the user lands on
   * a private route — does NOT block the UI.
   */
  validateToken: async () => {
    const response = await api.get('/auth/me')
    return response.data
  },

  /**
   * pingBackend — calls the anonymous /auth/ping endpoint to warm up Render's
   * free-tier backend before making authenticated requests.
   * Returns true on success, false on any error (including timeout).
   */
  pingBackend: async () => {
    try {
      // Use a shorter timeout for the ping itself — we just want to kick
      // the cold-start, not necessarily wait for a full round-trip.
      await api.get('/auth/ping', { timeout: REQUEST_TIMEOUT_MS })
      return true
    } catch {
      return false
    }
  }
}

export default api
