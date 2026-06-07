import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import { useTheme } from '../contexts/ThemeContext'
import { GlassPanel } from '../components/GlassPanel'
import { SunIcon, MoonIcon } from '../components/Icons'
import './Login.css'

function Login() {
  const { theme, toggleTheme } = useTheme()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const { login, user, loading: authLoading } = useAuth()
  const navigate = useNavigate()

  // Redirect về home nếu đã đăng nhập
  useEffect(() => {
    if (!authLoading && user) {
      navigate('/')
    }
  }, [user, authLoading, navigate])

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')
    setLoading(true)

    const result = await login(username, password)

    if (result.success) {
      navigate('/')
    } else {
      setError(result.message)
    }

    setLoading(false)
  }

  // Hiển thị loading nếu đang kiểm tra auth
  if (authLoading) {
    return <div className="loading">Đang mở khóa kỷ niệm...</div>
  }

  // Không hiển thị login form nếu đã đăng nhập (sẽ redirect trong useEffect)
  if (user) {
    return null
  }

  return (
    <div className="login-container">
      <button className="login-theme-toggle theme-toggle-btn" onClick={toggleTheme} title={theme === 'light' ? 'Chế độ tối' : 'Chế độ sáng'}>
        {theme === 'light' ? <MoonIcon size={20} /> : <SunIcon size={20} />}
      </button>
      <GlassPanel enable3D={true} className="login-card">
        <div className="login-header">
          <h1 className="login-title">
            ✦ Bin và Bún ✦
          </h1>
          <p className="login-subtitle">iu iu bbi</p>
        </div>

        <form onSubmit={handleSubmit} className="login-form">
          <div className="form-group">
            <label htmlFor="username">Tên người dùng</label>
            <input
              id="username"
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="Nhập tên đăng nhập"
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="password">Mật khẩu bí mật</label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Nhập mật khẩu"
              required
            />
          </div>

          {error && <div className="error-message">{error}</div>}

          <button type="submit" className="login-button" disabled={loading}>
            {loading ? 'Đang mở cửa...' : 'Đăng nhập 💕'}
          </button>
        </form>
      </GlassPanel>
    </div>
  )
}

export default Login
