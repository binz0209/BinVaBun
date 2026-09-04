import { BrowserRouter as Router, Routes, Route, Navigate, useLocation } from 'react-router-dom'
import { useEffect } from 'react'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import { ThemeProvider } from './contexts/ThemeContext'
import { ToastProvider, useToast } from './contexts/ToastContext'
import { SkeletonLoader } from './components/SkeletonLoader'
import Login from './pages/Login'
import Home from './pages/Home'
import Profile from './pages/Profile'
import PostDetail from './pages/PostDetail'
import './App.css'

// ── Route Guards ──────────────────────────────────────────────────────────────

function PrivateRoute({ children }) {
  const { user, loading, wakeupToastShown } = useAuth()
  const { addToast } = useToast()

  // Nếu vẫn đang loading (hiếm gặp với optimistic approach) → hiện skeleton
  if (loading) {
    return <SkeletonLoader />
  }

  // Chưa đăng nhập → redirect login
  if (!user) {
    return <Navigate to="/login" />
  }

  return <WakeupToastWrapper wakeupToastShown={wakeupToastShown} addToast={addToast}>{children}</WakeupToastWrapper>
}

/**
 * WakeupToastWrapper — bao quanh các private route.
 * Sau 4 giây mà backend chưa respond (first API call từ component con),
 * hiện toast "Server đang khởi động...".
 *
 * Cách hoạt động: lắng nghe custom event "backend-slow" mà authService phát đi.
 */
function WakeupToastWrapper({ children, wakeupToastShown, addToast }) {
  useEffect(() => {
    const handleSlow = () => {
      if (!wakeupToastShown.current) {
        wakeupToastShown.current = true
        addToast('☕ Server đang khởi động lại, vui lòng chờ chút...', 'info', 8000)
      }
    }

    window.addEventListener('backend-slow', handleSlow)
    return () => window.removeEventListener('backend-slow', handleSlow)
  }, [wakeupToastShown, addToast])

  return children
}

function PublicRoute({ children }) {
  const { user, loading } = useAuth()

  if (loading) {
    return <SkeletonLoader />
  }

  return user ? <Navigate to="/" /> : children
}

// ── App Routes ────────────────────────────────────────────────────────────────

function AppRoutes() {
  const location = useLocation()
  return (
    <div key={location.pathname} className="page-transition">
      <Routes location={location}>
        <Route path="/login" element={<PublicRoute><Login /></PublicRoute>} />
        <Route path="/" element={<PrivateRoute><Home /></PrivateRoute>} />
        <Route path="/profile" element={<PrivateRoute><Profile /></PrivateRoute>} />
        <Route path="/post/:id" element={<PrivateRoute><PostDetail /></PrivateRoute>} />
      </Routes>
    </div>
  )
}

// ── Root App ──────────────────────────────────────────────────────────────────

function App() {
  return (
    <ThemeProvider>
      <ToastProvider>
        <AuthProvider>
          <Router>
            <div className="spatial-background"></div>
            <div className="starfield"></div>
            <AppRoutes />
          </Router>
        </AuthProvider>
      </ToastProvider>
    </ThemeProvider>
  )
}

export default App
