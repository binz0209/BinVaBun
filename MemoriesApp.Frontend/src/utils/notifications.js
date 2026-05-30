// Utility functions for browser notifications

export const requestNotificationPermission = async () => {
  if (!('Notification' in window)) {
    console.log('Trình duyệt này không hỗ trợ thông báo')
    return false
  }

  if (Notification.permission === 'granted') {
    return true
  }

  if (Notification.permission !== 'denied') {
    const permission = await Notification.requestPermission()
    return permission === 'granted'
  }

  return false
}

export const showNotification = (title, options = {}) => {
  if (!('Notification' in window)) {
    console.log('Trình duyệt này không hỗ trợ thông báo')
    return null
  }

  if (Notification.permission === 'granted') {
    const notification = new Notification(title, {
      icon: '/favicon.ico', // Có thể thay bằng icon của app
      badge: '/favicon.ico',
      ...options
    })

    // Xử lý khi click vào notification
    notification.onclick = (event) => {
      event.preventDefault()
      window.focus() // Focus vào tab
      
      // Navigate đến post detail nếu có postId
      if (options.postId) {
        const targetUrl = `/post/${options.postId}`
        // Sử dụng window.location để navigate
        if (window.location.pathname !== targetUrl) {
          window.location.href = targetUrl
        }
      } else {
        // Nếu không có postId, về trang chủ
        if (window.location.pathname !== '/') {
          window.location.href = '/'
        }
      }
      
      notification.close()
    }

    // Đóng thông báo sau 5 giây
    setTimeout(() => {
      notification.close()
    }, 5000)

    return notification
  }

  return null
}

