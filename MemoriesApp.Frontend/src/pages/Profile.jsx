import { useState, useEffect } from 'react'
import { useAuth } from '../contexts/AuthContext'
import { profileService } from '../services/profileService'
import { useNavigate } from 'react-router-dom'
import { CameraIcon, SaveIcon, CancelIcon, EditIcon, BackArrowIcon, LoadingSpinnerIcon, LogoutIcon } from '../components/Icons'
import { BottomNav } from '../components/BottomNav'
import { GlassPanel } from '../components/GlassPanel'
import './Profile.css'

function Profile() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [profile, setProfile] = useState(null)
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState(false)
  const [displayName, setDisplayName] = useState('')
  const [avatar, setAvatar] = useState(null)
  const [avatarPreview, setAvatarPreview] = useState(null)
  const [saving, setSaving] = useState(false)
  const [showChangePassword, setShowChangePassword] = useState(false)
  const [oldPassword, setOldPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [changingPassword, setChangingPassword] = useState(false)

  useEffect(() => {
    loadProfile()
  }, [])

  const loadProfile = async () => {
    try {
      const data = await profileService.getProfile()
      setProfile(data)
      setDisplayName(data.displayName)
      setAvatarPreview(data.avatar)
    } catch (error) {
      console.error('Error loading profile:', error)
    } finally {
      setLoading(false)
    }
  }

  const handleAvatarChange = (e) => {
    const file = e.target.files[0]
    if (file) {
      setAvatar(file)
      const reader = new FileReader()
      reader.onloadend = () => {
        setAvatarPreview(reader.result)
      }
      reader.readAsDataURL(file)
    }
  }

  const handleSave = async () => {
    if (!displayName.trim()) {
      alert('Vui lòng nhập tên hiển thị')
      return
    }

    setSaving(true)
    try {
      const updatedProfile = await profileService.updateProfile(displayName, avatar)
      setProfile(updatedProfile)
      setEditing(false)
      setAvatar(null)
      
      // Update user in localStorage and context
      const userData = JSON.parse(localStorage.getItem('user') || '{}')
      userData.displayName = updatedProfile.displayName
      userData.avatar = updatedProfile.avatar
      localStorage.setItem('user', JSON.stringify(userData))
      
      // Reload page to update context
      window.location.reload()
    } catch (error) {
      console.error('Error updating profile:', error)
      alert('Có lỗi xảy ra khi cập nhật profile')
    } finally {
      setSaving(false)
    }
  }

  const handleCancel = () => {
    setEditing(false)
    setDisplayName(profile?.displayName || '')
    setAvatar(null)
    setAvatarPreview(profile?.avatar || null)
  }

  const handleChangePassword = async () => {
    if (!oldPassword || !newPassword || !confirmPassword) {
      alert('Vui lòng điền đầy đủ thông tin')
      return
    }

    if (newPassword.length < 6) {
      alert('Mật khẩu mới phải có ít nhất 6 ký tự')
      return
    }

    if (newPassword !== confirmPassword) {
      alert('Mật khẩu mới và xác nhận mật khẩu không khớp')
      return
    }

    setChangingPassword(true)
    try {
      await profileService.changePassword(oldPassword, newPassword)
      alert('Đổi mật khẩu thành công')
      setShowChangePassword(false)
      setOldPassword('')
      setNewPassword('')
      setConfirmPassword('')
    } catch (error) {
      console.error('Error changing password:', error)
      const errorMessage = error.response?.data?.message || error.response?.data?.error || 'Có lỗi xảy ra khi đổi mật khẩu'
      alert(errorMessage)
    } finally {
      setChangingPassword(false)
    }
  }

  if (loading) {
    return (
      <div className="loading">
        <LoadingSpinnerIcon size={32} className="spinning" />
        <span>Đang tải...</span>
      </div>
    )
  }

  return (
    <div className="profile-container">
      <header className="profile-header">
        <div className="header-content">
              <h1 className="app-title" onClick={() => navigate('/')} style={{ cursor: 'pointer' }}>
                Bin và Bún
              </h1>
          <div className="header-actions">
            <button onClick={() => navigate('/')} className="back-button" title="Về trang chủ">
              <BackArrowIcon size={24} />
            </button>
            <button onClick={logout} className="logout-button" title="Đăng xuất">
              <LogoutIcon size={24} />
            </button>
          </div>
        </div>
      </header>

      <main className="profile-main">
        <GlassPanel enable3D={true} className="profile-card">
          <div className="profile-header-section">
            <div className="avatar-section">
              {editing ? (
                <div className="avatar-edit">
                  <img 
                    src={avatarPreview || '/default-avatar.png'} 
                    alt="Avatar" 
                    className="avatar-preview"
                  />
                  <label htmlFor="avatar-input" className="avatar-upload-btn">
                    <CameraIcon size={18} style={{ display: 'inline-block', verticalAlign: 'middle', marginRight: '4px' }} />
                    Chọn ảnh
                  </label>
                  <input
                    id="avatar-input"
                    type="file"
                    accept="image/*"
                    onChange={handleAvatarChange}
                    style={{ display: 'none' }}
                  />
                </div>
              ) : (
                <img 
                  src={profile?.avatar || '/default-avatar.png'} 
                  alt="Avatar" 
                  className="avatar-display"
                />
              )}
            </div>
            <div className="profile-info">
              {editing ? (
                <div className="edit-form">
                  <input
                    type="text"
                    value={displayName}
                    onChange={(e) => setDisplayName(e.target.value)}
                    className="display-name-input"
                    placeholder="Tên hiển thị"
                  />
                </div>
              ) : (
                <>
                  <h2 className="profile-name">{profile?.displayName}</h2>
                  <p className="profile-username">@{profile?.username}</p>
                </>
              )}
            </div>
          </div>

          <div className="profile-actions">
            {editing ? (
              <>
                <button 
                  onClick={handleSave} 
                  className="save-button"
                  disabled={saving}
                >
                  {saving ? 'Đang lưu...' : (
                    <>
                      <SaveIcon size={18} style={{ display: 'inline-block', verticalAlign: 'middle', marginRight: '4px' }} />
                      Lưu
                    </>
                  )}
                </button>
                <button 
                  onClick={handleCancel} 
                  className="cancel-button"
                  disabled={saving}
                >
                  <CancelIcon size={18} style={{ display: 'inline-block', verticalAlign: 'middle', marginRight: '4px' }} />
                  Hủy
                </button>
              </>
            ) : (
              <>
                <button 
                  onClick={() => setEditing(true)} 
                  className="edit-button"
                >
                  <EditIcon size={18} style={{ display: 'inline-block', verticalAlign: 'middle', marginRight: '4px' }} />
                  Chỉnh sửa profile
                </button>
                <button 
                  onClick={() => setShowChangePassword(true)} 
                  className="change-password-button"
                >
                  Đổi mật khẩu
                </button>
              </>
            )}
          </div>

          {showChangePassword && (
            <div className="change-password-modal">
              <div className="change-password-content">
                <h3>Đổi mật khẩu</h3>
                <div className="password-form">
                  <input
                    type="password"
                    value={oldPassword}
                    onChange={(e) => setOldPassword(e.target.value)}
                    placeholder="Mật khẩu cũ"
                    className="password-input"
                    disabled={changingPassword}
                  />
                  <input
                    type="password"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    placeholder="Mật khẩu mới"
                    className="password-input"
                    disabled={changingPassword}
                  />
                  <input
                    type="password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    placeholder="Xác nhận mật khẩu mới"
                    className="password-input"
                    disabled={changingPassword}
                  />
                  <div className="password-actions">
                    <button 
                      onClick={handleChangePassword} 
                      className="save-password-button"
                      disabled={changingPassword}
                    >
                      {changingPassword ? (
                        <>
                          <LoadingSpinnerIcon size={16} className="spinning" />
                          <span>Đang đổi...</span>
                        </>
                      ) : (
                        'Đổi mật khẩu'
                      )}
                    </button>
                    <button 
                      onClick={() => {
                        setShowChangePassword(false)
                        setOldPassword('')
                        setNewPassword('')
                        setConfirmPassword('')
                      }} 
                      className="cancel-password-button"
                      disabled={changingPassword}
                    >
                      Hủy
                    </button>
                  </div>
                </div>
              </div>
            </div>
          )}
        </GlassPanel>
      </main>
      <BottomNav />
    </div>
  )
}

export default Profile
