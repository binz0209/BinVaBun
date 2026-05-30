import { useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { BellIcon } from './Icons'
import './NotificationBell.css'

function NotificationBell({
  notifications,
  unreadCount,
  showNotifications,
  onToggle,
  onNotificationClick,
  onMarkAllRead
}) {
  const navigate = useNavigate()
  const dropdownRef = useRef(null)
  const buttonRef = useRef(null)

  // ESC key và click outside handler
  useEffect(() => {
    if (!showNotifications) return

    const handleEsc = (e) => {
      if (e.key === 'Escape') {
        onToggle()
      }
    }

    const handleClickOutside = (e) => {
      if (
        dropdownRef.current &&
        !dropdownRef.current.contains(e.target) &&
        buttonRef.current &&
        !buttonRef.current.contains(e.target)
      ) {
        onToggle()
      }
    }

    document.addEventListener('keydown', handleEsc)
    document.addEventListener('mousedown', handleClickOutside)
    return () => {
      document.removeEventListener('keydown', handleEsc)
      document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [showNotifications, onToggle])
  const formatDate = (date) => {
    const d = new Date(date)
    const now = new Date()
    const diff = now - d
    const minutes = Math.floor(diff / 60000)
    const hours = Math.floor(diff / 3600000)
    const days = Math.floor(diff / 86400000)

    if (minutes < 1) return 'Vừa xong'
    if (minutes < 60) return `${minutes} phút trước`
    if (hours < 24) return `${hours} giờ trước`
    if (days < 7) return `${days} ngày trước`
    return d.toLocaleDateString('vi-VN')
  }

  return (
    <div className="notification-bell-container">
      <button ref={buttonRef} className="notification-bell-button" onClick={onToggle}>
        <BellIcon size={24} />
        {unreadCount > 0 && <span className="notification-badge">{unreadCount}</span>}
      </button>

      {showNotifications && (
        <div ref={dropdownRef} className="notification-dropdown">
          <div className="notification-header">
            <h3>Thông báo</h3>
            {unreadCount > 0 && (
              <button onClick={onMarkAllRead} className="mark-all-read-button">
                Đánh dấu tất cả đã đọc
              </button>
            )}
          </div>
          
          <div className="notification-list">
            {notifications.length === 0 ? (
              <div className="no-notifications">Chưa có thông báo nào</div>
            ) : (
              notifications.map((notification) => (
                <div
                  key={notification.id}
                  className={`notification-item ${!notification.isRead ? 'unread' : ''}`}
                  onClick={() => {
                    onNotificationClick(notification)
                    if (notification.postId) {
                      navigate(`/post/${notification.postId}`)
                    } else {
                      navigate('/')
                    }
                  }}
                >
                  <div className="notification-content">
                    <p className="notification-message">{notification.message}</p>
                    <span className="notification-date">{formatDate(notification.createdAt)}</span>
                  </div>
                  {!notification.isRead && <div className="unread-dot"></div>}
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  )
}

export default NotificationBell

