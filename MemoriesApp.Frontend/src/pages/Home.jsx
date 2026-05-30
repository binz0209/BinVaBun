import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import { postService } from '../services/postService'
import { notificationService } from '../services/notificationService'
import signalRService from '../services/signalRService'
import PostList from '../components/PostList'
import NotificationBell from '../components/NotificationBell'
import CreatePostModal from '../components/CreatePostModal'
import FilterModal from '../components/FilterModal'
import CalendarModal from '../components/CalendarModal'
import { calendarService } from '../services/calendarService'
import { BottomNav } from '../components/BottomNav'
import { UserIcon, FilterIcon, LogoutIcon, MenuIcon, LoadingSpinnerIcon } from '../components/Icons'
import { PostSkeleton } from '../components/Skeleton'
import { ConfirmModal } from '../components/ConfirmModal'
import { requestNotificationPermission, showNotification } from '../utils/notifications'
import './Home.css'

function Home() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [posts, setPosts] = useState([])
  const [allPosts, setAllPosts] = useState([])
  const [notifications, setNotifications] = useState([])
  const [totalMarkedDates, setTotalMarkedDates] = useState(0)
  const [loading, setLoading] = useState(true)
  const [showNotifications, setShowNotifications] = useState(false)
  const [showCreatePostModal, setShowCreatePostModal] = useState(false)
  const [showFilterModal, setShowFilterModal] = useState(false)
  const [showCalendarModal, setShowCalendarModal] = useState(false)
  const [filter, setFilter] = useState({ includeContent: '', timeFilter: null })
  const [currentPage, setCurrentPage] = useState(1)
  const [hasMore, setHasMore] = useState(true)
  const [loadingMore, setLoadingMore] = useState(false)
  const [showMenuDropdown, setShowMenuDropdown] = useState(false)
  const [postToDelete, setPostToDelete] = useState(null)
  const pageSize = 10
  const menuRef = useRef(null)
  const menuButtonRef = useRef(null)
  const audioRef = useRef(null) // Ref cho audio element để reuse

  // Intersection Observer cho infinite scroll
  useEffect(() => {
    if (!hasMore || loadingMore) return

    const observerOptions = {
      root: null,
      rootMargin: '100px',
      threshold: 0.1
    }

    const observerCallback = (entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting && hasMore && !loadingMore) {
          loadMorePosts()
        }
      })
    }

    const observer = new IntersectionObserver(observerCallback, observerOptions)
    const sentinel = document.getElementById('scroll-sentinel')

    if (sentinel) {
      observer.observe(sentinel)
    }

    return () => {
      if (sentinel) {
        observer.unobserve(sentinel)
      }
    }
  }, [hasMore, loadingMore])

  // ESC key và click outside handler cho menu dropdown
  useEffect(() => {
    if (!showMenuDropdown) return

    const handleEsc = (e) => {
      if (e.key === 'Escape') {
        setShowMenuDropdown(false)
      }
    }

    const handleClickOutside = (e) => {
      if (
        menuRef.current &&
        !menuRef.current.contains(e.target) &&
        menuButtonRef.current &&
        !menuButtonRef.current.contains(e.target)
      ) {
        setShowMenuDropdown(false)
      }
    }

    document.addEventListener('keydown', handleEsc)
    document.addEventListener('mousedown', handleClickOutside)
    return () => {
      document.removeEventListener('keydown', handleEsc)
      document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [showMenuDropdown])

  useEffect(() => {
    loadData(1, false)
    loadMarkedDatesTotal()

    // Yêu cầu quyền thông báo khi component mount
    requestNotificationPermission().then(granted => {
      if (granted) {
        console.log('Đã được cấp quyền thông báo')
      } else {
        console.log('Không được cấp quyền thông báo')
      }
    })

    // Tạo audio element một lần và reuse
    if (!audioRef.current) {
      audioRef.current = new Audio('/NotificationSound.mp3')
      audioRef.current.volume = 1.0 // Set volume tối đa
      audioRef.current.preload = 'auto' // Preload audio

      // Xử lý lỗi khi load audio
      audioRef.current.addEventListener('error', (e) => {
        console.error('Lỗi khi load audio:', e)
        console.error('Audio error details:', audioRef.current?.error)
      })

      // Xử lý khi audio sẵn sàng
      audioRef.current.addEventListener('canplaythrough', () => {
        console.log('Audio đã sẵn sàng để phát')
      })

      // Thử unlock audio bằng cách play/pause ngay (sẽ fail nhưng unlock policy)
      // Chỉ làm khi đã có user interaction (user đã login = đã có interaction)
      try {
        audioRef.current.play().then(() => {
          audioRef.current.pause()
          audioRef.current.currentTime = 0
          console.log('Audio đã được unlock')
        }).catch(() => {
          console.log('Audio chưa được unlock, cần user interaction')
        })
      } catch (e) {
        console.log('Chưa thể unlock audio:', e)
      }
    }

    // Connect to SignalR
    const token = localStorage.getItem('token')
    if (token) {
      signalRService.connect(token).catch(console.error)
    }

    // Setup SignalR event handlers
    const handleNewPost = (post) => {
      // Tính lại IsOwner và IsLiked dựa trên current user
      const currentUserId = user?.id || user?.Id
      const updatedPost = {
        ...post,
        isOwner: String(post.authorId) === String(currentUserId),
        isLiked: false // Sẽ được cập nhật khi load lại hoặc khi like
      }
      // Nếu không có filter, thêm post mới vào đầu danh sách (chỉ nếu chưa tồn tại)
      if (!hasActiveFilter) {
        setAllPosts(prev => {
          // Kiểm tra xem post đã tồn tại chưa (tránh duplicate)
          const exists = prev.some(p => p.id === updatedPost.id)
          if (exists) {
            console.log('Post already exists, skipping duplicate')
            return prev
          }
          return [updatedPost, ...prev]
        })
        setPosts(prev => {
          // Kiểm tra xem post đã tồn tại chưa (tránh duplicate)
          const exists = prev.some(p => p.id === updatedPost.id)
          if (exists) {
            console.log('Post already exists, skipping duplicate')
            return prev
          }
          return [updatedPost, ...prev]
        })
      } else {
        // Nếu có filter, reload lại để đảm bảo post mới được filter đúng
        loadData(1, false, filter)
      }
    }

    const handlePostUpdated = (post) => {
      // Tính lại IsOwner và IsLiked dựa trên current user
      const currentUserId = user?.id || user?.Id
      setAllPosts(prev => prev.map(p => {
        if (p.id === post.id) {
          return {
            ...post,
            isOwner: String(post.authorId) === String(currentUserId),
            isLiked: p.isLiked // Giữ nguyên trạng thái like hiện tại
          }
        }
        return p
      }))
      setPosts(prev => prev.map(p => {
        if (p.id === post.id) {
          return {
            ...post,
            isOwner: String(post.authorId) === String(currentUserId),
            isLiked: p.isLiked
          }
        }
        return p
      }))
    }

    const handlePostDeleted = (postId) => {
      setAllPosts(prev => prev.filter(p => p.id !== postId))
      setPosts(prev => prev.filter(p => p.id !== postId))
    }

    const handleNewComment = ({ postId, comment }) => {
      setPosts(prev => prev.map(p =>
        p.id === postId
          ? { ...p, comments: [...p.comments, comment] }
          : p
      ))
    }

    const handlePostLiked = ({ postId, isLiked, likesCount, userId }) => {
      setPosts(prev => prev.map(p => {
        if (p.id === postId) {
          const currentUserId = user?.id
          return {
            ...p,
            likesCount,
            isLiked: currentUserId === userId ? isLiked : p.isLiked
          }
        }
        return p
      }))
    }

    const handleNewNotification = (notification) => {
      console.log('Received notification:', notification)
      const currentUserId = user?.id || user?.Id
      console.log('Current user ID:', currentUserId, 'Notification fromUserId:', notification.fromUserId)

      // Luôn thêm notification vào danh sách
      setNotifications(prev => {
        // Kiểm tra xem notification đã tồn tại chưa (tránh duplicate)
        const exists = prev.some(n => n.id === notification.id)
        if (exists) {
          console.log('Notification already exists, skipping')
          return prev
        }
        return [notification, ...prev]
      })

      // Phát sound và hiển thị browser notification cho tất cả notifications
      // (kể cả khi comment vào bài của chính mình)
      const shouldPlaySound = true // Luôn phát sound cho mọi notification
      console.log('Should play sound:', shouldPlaySound, 'Notification type:', notification.type)

      if (shouldPlaySound) {
        // Phát sound khi có thông báo mới
        const playNotificationSound = async () => {
          if (audioRef.current) {
            try {
              // Reset audio về đầu để phát lại
              audioRef.current.currentTime = 0
              // Phát sound
              await audioRef.current.play()
              console.log('✅ Đã phát sound thông báo thành công')
            } catch (err) {
              console.error('❌ Không thể phát sound với audioRef:', err)
              // Fallback: thử tạo audio mới
              try {
                const newAudio = new Audio('/NotificationSound.mp3')
                newAudio.volume = 1.0
                await newAudio.play()
                console.log('✅ Đã phát sound với audio mới')
              } catch (fallbackErr) {
                console.error('❌ Lỗi fallback audio:', fallbackErr)
                // Thử một lần nữa với cách khác
                try {
                  const finalAudio = new Audio()
                  finalAudio.src = '/NotificationSound.mp3'
                  finalAudio.volume = 1.0
                  finalAudio.load()
                  await finalAudio.play()
                  console.log('✅ Đã phát sound với final audio')
                } catch (finalErr) {
                  console.error('❌ Tất cả cách phát sound đều thất bại:', finalErr)
                }
              }
            }
          } else {
            // Nếu audioRef chưa được tạo, tạo mới
            try {
              const audio = new Audio('/NotificationSound.mp3')
              audio.volume = 1.0
              await audio.play()
              console.log('✅ Đã phát sound với audio mới tạo')
            } catch (error) {
              console.error('❌ Lỗi khi tạo và phát audio:', error)
            }
          }
        }

        // Gọi hàm phát sound
        playNotificationSound()

        // Hiển thị browser notification (hoạt động ngay cả khi tab không được focus)
        showNotification('Bin Và Bún', {
          body: notification.message,
          icon: '/favicon.ico',
          badge: '/favicon.ico',
          tag: notification.id, // Để tránh duplicate notifications
          requireInteraction: false,
          silent: false, // Không tắt tiếng để phát sound
          postId: notification.postId // Để navigate khi click
        })
      }
    }

    const handleDatesBatchUpdated = (data) => {
      // Dữ liệu batch update từ backend
      setTotalMarkedDates(prev => prev + (data.added?.length || 0) - (data.removedIds?.length || 0))
    }

    signalRService.on('NewPost', handleNewPost)
    signalRService.on('PostUpdated', handlePostUpdated)
    signalRService.on('PostDeleted', handlePostDeleted)
    signalRService.on('NewComment', handleNewComment)
    signalRService.on('PostLiked', handlePostLiked)
    signalRService.on('NewNotification', handleNewNotification)
    signalRService.on('DateMarked', () => setTotalMarkedDates(prev => prev + 1))
    signalRService.on('DateUnmarked', () => setTotalMarkedDates(prev => prev - 1))
    signalRService.on('DatesBatchUpdated', handleDatesBatchUpdated)

    return () => {
      signalRService.off('NewPost', handleNewPost)
      signalRService.off('PostUpdated', handlePostUpdated)
      signalRService.off('PostDeleted', handlePostDeleted)
      signalRService.off('NewComment', handleNewComment)
      signalRService.off('PostLiked', handlePostLiked)
      signalRService.off('NewNotification', handleNewNotification)
      signalRService.off('DateMarked')
      signalRService.off('DateUnmarked')
      signalRService.off('DatesBatchUpdated', handleDatesBatchUpdated)
      signalRService.disconnect()
    }
  }, [user])

  const loadMarkedDatesTotal = async () => {
    try {
      const dates = await calendarService.getMarkedDates()
      setTotalMarkedDates(dates.length)
    } catch (error) {
      console.error('Error loading marked dates:', error)
    }
  }

  // Filter giờ được xử lý ở backend, không cần applyFilter nữa

  const handleApplyFilter = (newFilter) => {
    setFilter(newFilter)
    setCurrentPage(1) // Reset về trang 1 khi filter
    setHasMore(true)
    loadData(1, false, newFilter) // Load lại từ đầu với filter mới
  }

  const handleClearFilter = () => {
    const emptyFilter = { includeContent: '', timeFilter: null }
    setFilter(emptyFilter)
    setCurrentPage(1) // Reset về trang 1 khi xóa filter
    setHasMore(true)
    loadData(1, false, emptyFilter) // Load lại từ đầu không filter
  }

  const hasActiveFilter = filter.includeContent?.trim() !== '' || (filter.timeFilter && filter.timeFilter.type)

  const loadData = async (page = 1, append = false, currentFilter = null) => {
    try {
      if (page === 1) {
        setLoading(true)
      } else {
        setLoadingMore(true)
      }

      const filterToUse = currentFilter || filter

      // Build query params cho API
      const params = {
        page,
        pageSize
      }

      if (filterToUse.includeContent && filterToUse.includeContent.trim()) {
        params.content = filterToUse.includeContent.trim()
      }

      if (filterToUse.timeFilter && filterToUse.timeFilter.type) {
        params.timeType = filterToUse.timeFilter.type
        if (filterToUse.timeFilter.type === 'before' && filterToUse.timeFilter.beforeDate) {
          params.beforeDate = filterToUse.timeFilter.beforeDate
        } else if (filterToUse.timeFilter.type === 'after' && filterToUse.timeFilter.afterDate) {
          params.afterDate = filterToUse.timeFilter.afterDate
        } else if (filterToUse.timeFilter.type === 'exact' && filterToUse.timeFilter.exactDate) {
          params.exactDate = filterToUse.timeFilter.exactDate
        } else if (filterToUse.timeFilter.type === 'range' && filterToUse.timeFilter.fromDate && filterToUse.timeFilter.toDate) {
          params.fromDate = filterToUse.timeFilter.fromDate
          params.toDate = filterToUse.timeFilter.toDate
        }
      }

      const [postsResponse, notificationsData] = await Promise.all([
        postService.getPosts(params),
        page === 1 ? notificationService.getNotifications() : Promise.resolve([])
      ])

      // Tính lại isOwner dựa trên current user
      const currentUserId = user?.id || user?.Id
      const postsWithOwner = postsResponse.posts.map(post => ({
        ...post,
        isOwner: String(post.authorId) === String(currentUserId)
      }))

      if (append) {
        setAllPosts(prev => [...prev, ...postsWithOwner])
        setPosts(prev => [...prev, ...postsWithOwner])
      } else {
        setAllPosts(postsWithOwner)
        setPosts(postsWithOwner)
      }

      setCurrentPage(page)
      setHasMore(postsResponse.hasMore)

      if (page === 1) {
        setNotifications(notificationsData)
      }
    } catch (error) {
      console.error('Error loading data:', error)
    } finally {
      setLoading(false)
      setLoadingMore(false)
    }
  }

  // Load more posts khi scroll đến cuối
  const loadMorePosts = async () => {
    if (!loadingMore && hasMore) {
      await loadData(currentPage + 1, true, filter)
    }
  }

  const handlePostCreated = (newPost) => {
    // Không cần thêm post ở đây vì SignalR sẽ tự động broadcast NewPost
    // Chỉ reload nếu có filter để đảm bảo post mới được filter đúng
    if (hasActiveFilter) {
      loadData(1, false, filter)
    }
    // Nếu không có filter, SignalR handleNewPost sẽ tự động thêm post
  }

  const handleLike = async (postId) => {
    try {
      await postService.toggleLike(postId)
      // Real-time update via SignalR, no need to reload
    } catch (error) {
      console.error('Error toggling like:', error)
    }
  }

  const handleComment = async (postId, content) => {
    try {
      await postService.addComment(postId, content)
      // Real-time update via SignalR, no need to reload
    } catch (error) {
      console.error('Error adding comment:', error)
    }
  }

  const handleUpdatePost = async (postId, content, keepImages, newImages) => {
    try {
      await postService.updatePost(postId, content, keepImages, newImages)
      // Real-time update via SignalR, no need to reload
    } catch (error) {
      console.error('Error updating post:', error)
      throw error
    }
  }

  const handleDeletePost = (postId) => {
    setPostToDelete(postId)
  }

  const confirmDeletePost = async () => {
    if (postToDelete) {
      try {
        await postService.deletePost(postToDelete)
        // Real-time update via SignalR, no need to reload
      } catch (error) {
        console.error('Error deleting post:', error)
        alert('Có lỗi xảy ra khi xóa bài viết')
      } finally {
        setPostToDelete(null)
      }
    }
  }

  const handleNotificationClick = async (notification) => {
    if (!notification.isRead) {
      await notificationService.markAsRead(notification.id)
      loadData(1, false, filter)
    }
    setShowNotifications(false)
  }

  const handleMarkAllRead = async () => {
    await notificationService.markAllAsRead()
    loadData(1, false, filter)
  }

  const unreadCount = notifications.filter(n => !n.isRead).length

  if (loading) {
    return (
      <div className="home-container">
        <header className="home-header">
          <div className="header-content">
            <div className="header-left">
              <h1 className="app-title"><span className="app-title-full">Bin & Bún</span><span className="app-title-short">2B</span></h1>
            </div>
          </div>
        </header>
        <main className="home-main">
          <div className="content-wrapper">
            <PostSkeleton />
            <PostSkeleton />
            <PostSkeleton />
          </div>
        </main>
      </div>
    )
  }

  // Caculate days together
  const startDate = new Date('2025-12-29T00:00:00Z'); // Example date, could be fetched from API
  const today = new Date();
  const diffTime = Math.abs(today - startDate);
  const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24) + 1);

  return (
    <div className="home-container">
      <header className="home-header">
        <div className="header-content">
          <div className="header-left">
            <h1 className="app-title" onClick={() => navigate('/')}>
              <span className="app-title-full">Bin & Bún</span>
              <span className="app-title-short">2B</span>
            </h1>
          </div>

          <div className="header-right">
            <NotificationBell
              notifications={notifications}
              unreadCount={unreadCount}
              showNotifications={showNotifications}
              onToggle={() => setShowNotifications(!showNotifications)}
              onNotificationClick={handleNotificationClick}
              onMarkAllRead={handleMarkAllRead}
            />

            <button className="icon-btn" onClick={() => setShowFilterModal(true)} title="Lọc">
              <FilterIcon size={20} />
            </button>

            <div className="desktop-menu">
              <button onClick={() => navigate('/profile')} className="icon-btn" title="Xem profile">
                <UserIcon size={20} />
              </button>
              <button onClick={logout} className="icon-btn" title="Đăng xuất">
                <LogoutIcon size={20} />
              </button>
            </div>

            <div className="mobile-menu">
              <button
                ref={menuButtonRef}
                onClick={() => setShowMenuDropdown(!showMenuDropdown)}
                className="icon-btn"
                title="Menu"
              >
                <MenuIcon size={20} />
              </button>
              {showMenuDropdown && (
                <div ref={menuRef} className="menu-dropdown">
                  <button onClick={() => { navigate('/profile'); setShowMenuDropdown(false); }} className="menu-item">
                    <UserIcon size={16} />
                    <span>Profile</span>
                  </button>
                  <button onClick={() => { logout(); setShowMenuDropdown(false); }} className="menu-item">
                    <LogoutIcon size={16} />
                    <span>Đăng xuất</span>
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      </header>

      <main className="home-main">
        <div className="content-wrapper">

          {/* Anniversary Banner */}
          {!hasActiveFilter && (
            <div 
              className="anniversary-banner"
              onClick={() => setShowCalendarModal(true)}
            >
              <div className="anniversary-title">📅 Bên nhau {diffDays} ngày</div>
              <div className="anniversary-subtitle">
                💕 Đã đi chơi cùng nhau: {totalMarkedDates} ngày
              </div>
            </div>
          )}

          {/* Create Post Prompt */}
          <div className="create-post-prompt" onClick={() => setShowCreatePostModal(true)}>
            <div className="create-post-avatar">
              {user?.avatar ? <img src={user.avatar} alt="Avatar" /> : (user?.displayName || user?.username || '?')[0].toUpperCase()}
            </div>
            <div className="create-post-text">Chia sẻ khoảnh khắc...</div>
            <div className="create-post-actions">
              <div className="create-action-btn">📷</div>
              <div className="create-action-btn">💌</div>
            </div>
          </div>

          {hasActiveFilter && (
            <div style={{ marginBottom: '16px', display: 'flex', justifyContent: 'center' }}>
              <button onClick={handleClearFilter} style={{
                background: 'var(--color-ink-100)', color: 'var(--color-ink-700)', border: 'none',
                padding: '8px 16px', borderRadius: 'var(--radius-full)', cursor: 'pointer'
              }}>
                ✕ Xóa bộ lọc
              </button>
            </div>
          )}

          <PostList
            posts={posts}
            currentUserId={user?.id}
            onLike={handleLike}
            onComment={handleComment}
            onUpdate={handleUpdatePost}
            onDelete={handleDeletePost}
          />

          {/* Scroll sentinel */}
          <div id="scroll-sentinel" style={{ height: '20px', marginTop: '20px' }}>
            {loadingMore && (
              <div style={{ textAlign: 'center', padding: '20px', color: 'var(--color-ink-500)', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '8px' }}>
                <LoadingSpinnerIcon size={20} className="spinning" />
                <span>Đang tải thêm...</span>
              </div>
            )}
            {!hasMore && posts.length > 0 && (
              <div style={{ textAlign: 'center', padding: '20px', color: 'var(--color-ink-300)' }}>
                Đã hiển thị tất cả kỷ niệm
              </div>
            )}
          </div>
        </div>
      </main>

      <CreatePostModal
        isOpen={showCreatePostModal}
        onClose={() => setShowCreatePostModal(false)}
        onPostCreated={handlePostCreated}
      />

      <FilterModal
        isOpen={showFilterModal}
        onClose={() => setShowFilterModal(false)}
        onApplyFilter={handleApplyFilter}
      />

      <CalendarModal
        isOpen={showCalendarModal}
        onClose={() => setShowCalendarModal(false)}
      />

      <BottomNav onOpenCreatePost={() => setShowCreatePostModal(true)} />

      <ConfirmModal
        isOpen={!!postToDelete}
        onClose={() => setPostToDelete(null)}
        onConfirm={confirmDeletePost}
        title="Xóa kỷ niệm"
        message="Bạn có chắc chắn muốn xóa kỷ niệm này? Hành động này không thể hoàn tác."
        confirmText="Xóa"
        cancelText="Hủy"
      />
    </div>
  )
}

export default Home

