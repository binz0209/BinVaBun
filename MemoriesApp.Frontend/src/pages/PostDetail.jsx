import { useState, useEffect } from 'react'
import { createPortal } from 'react-dom'
import { useParams, useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import { postService } from '../services/postService'
import { BackArrowIcon, ZoomInIcon, CloseIcon, LoadingSpinnerIcon, LogoutIcon } from '../components/Icons'
import { HeartIcon, CommentIcon } from '../components/Icons'
import CommentSection from '../components/CommentSection'
import { GlassPanel } from '../components/GlassPanel'
import './PostDetail.css'

function PostDetail() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const { user, logout } = useAuth()
  const [post, setPost] = useState(null)
  const [loading, setLoading] = useState(true)
  const [currentImageIndex, setCurrentImageIndex] = useState(0)
  const [showZoomModal, setShowZoomModal] = useState(false)
  const [zoomImageIndex, setZoomImageIndex] = useState(0)

  // ESC key handler và prevent body scroll cho zoom modal
  useEffect(() => {
    if (!showZoomModal) return

    // Lock body scroll khi zoom modal mở
    const originalOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    const handleEsc = (e) => {
      if (e.key === 'Escape') {
        setShowZoomModal(false)
      }
    }

    // Prevent scroll propagation khi scroll trong zoom modal
    const handleWheel = (e) => {
      e.preventDefault()
    }

    document.addEventListener('keydown', handleEsc)
    const overlay = document.querySelector('.zoom-modal-overlay')
    if (overlay) {
      overlay.addEventListener('wheel', handleWheel, { passive: false })
    }

    return () => {
      document.removeEventListener('keydown', handleEsc)
      if (overlay) {
        overlay.removeEventListener('wheel', handleWheel)
      }
      document.body.style.overflow = originalOverflow
    }
  }, [showZoomModal])

  useEffect(() => {
    loadPost(true)
  }, [id])

  useEffect(() => {
    // Lấy image index từ URL query string
    const imageIndex = searchParams.get('image')
    if (imageIndex !== null && post?.images) {
      const index = parseInt(imageIndex, 10)
      if (!isNaN(index) && index >= 0 && index < post.images.length) {
        setCurrentImageIndex(index)
      }
    }
  }, [searchParams, post])

  const loadPost = async (showLoading = true) => {
    try {
      if (showLoading) setLoading(true)
      const foundPost = await postService.getPost(id)
      if (foundPost) {
        const currentUserId = user?.id || user?.Id
        setPost({
          ...foundPost,
          isOwner: String(foundPost.authorId) === String(currentUserId)
        })
      } else {
        navigate('/')
      }
    } catch (error) {
      console.error('Error loading post:', error)
      if (error.response?.status === 404) {
        navigate('/')
      }
    } finally {
      if (showLoading) setLoading(false)
    }
  }

  const handleLike = async () => {
    try {
      await postService.toggleLike(id)
      // Realtime or background reload without resetting UI
      loadPost(false)
    } catch (error) {
      console.error('Error toggling like:', error)
    }
  }

  const handleComment = async (postId, content) => {
    try {
      // Đảm bảo sử dụng đúng postId (có thể là id từ useParams hoặc postId từ tham số)
      const targetPostId = postId || id
      console.log('PostDetail handleComment - postId:', targetPostId, 'content:', content)
      await postService.addComment(targetPostId, content)
      loadPost(false)
    } catch (error) {
      console.error('Error adding comment:', error)
    }
  }

  const formatDate = (date) => {
    const d = new Date(date)
    return d.toLocaleString('vi-VN', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    })
  }

  const nextImage = () => {
    if (post?.images && post.images.length > 0) {
      setCurrentImageIndex((prev) => (prev + 1) % post.images.length)
    }
  }

  const prevImage = () => {
    if (post?.images && post.images.length > 0) {
      setCurrentImageIndex((prev) => (prev - 1 + post.images.length) % post.images.length)
    }
  }

  const nextZoomImage = () => {
    if (post?.images && post.images.length > 0) {
      setZoomImageIndex((prev) => (prev + 1) % post.images.length)
    }
  }

  const prevZoomImage = () => {
    if (post?.images && post.images.length > 0) {
      setZoomImageIndex((prev) => (prev - 1 + post.images.length) % post.images.length)
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

  if (!post) {
    return (
      <div className="loading">
        <span>Không tìm thấy bài viết</span>
      </div>
    )
  }

  const currentUserId = user?.id || user?.Id
  const hasMultipleImages = post.images && post.images.length > 1

  return (
    <div className="post-detail-container">
      <header className="post-detail-header">
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

          <main className="post-detail-main">
            <GlassPanel enable3D={true} className={`post-detail-content ${!post.images || post.images.length === 0 ? 'no-images' : ''}`}>
              {/* Left side - Images */}
              {post.images && post.images.length > 0 && (
                <div className="post-images-section">
                  <div className="image-carousel">
                    <div className="carousel-container">
                      <div className="carousel-image-wrapper">
                        <img 
                          key={currentImageIndex}
                          src={post.images[currentImageIndex]} 
                          alt={`Post image ${currentImageIndex + 1}`}
                          className="carousel-image image-transition"
                        />
                      </div>
                      <button
                        className="zoom-button"
                        onClick={() => {
                          setZoomImageIndex(currentImageIndex)
                          setShowZoomModal(true)
                        }}
                        title="Phóng to ảnh"
                      >
                        <ZoomInIcon size={24} />
                      </button>
                      {hasMultipleImages && (
                        <>
                          <button 
                            className="carousel-button carousel-prev"
                            onClick={prevImage}
                            aria-label="Ảnh trước"
                          >
                            ‹
                          </button>
                          <button 
                            className="carousel-button carousel-next"
                            onClick={nextImage}
                            aria-label="Ảnh sau"
                          >
                            ›
                          </button>
                          <div className="carousel-counter">
                            {currentImageIndex + 1} / {post.images.length}
                          </div>
                        </>
                      )}
                    </div>
                    {hasMultipleImages && (
                      <div className="carousel-indicators">
                        {post.images.map((_, index) => (
                          <button
                            key={index}
                            className={`indicator ${index === currentImageIndex ? 'active' : ''}`}
                            onClick={() => setCurrentImageIndex(index)}
                            aria-label={`Ảnh ${index + 1}`}
                          />
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              )}

              {/* Right side - Content and Comments */}
              <div className="post-content-section">
                <div className="post-header-detail">
                  <div className="post-author-detail">
                    {post.authorAvatar ? (
                      <img src={post.authorAvatar} alt={post.authorName} className="author-avatar-large" />
                    ) : (
                      <div className="author-avatar-placeholder-large">
                        {post.authorName.charAt(0).toUpperCase()}
                      </div>
                    )}
                    <div>
                      <div className="author-name-large">{post.authorName}</div>
                      <div className="post-date-detail">
                        {formatDate(post.createdAt)}
                        {post.updatedAt && post.updatedAt !== post.createdAt && (
                          <span className="edited-badge"> (đã chỉnh sửa)</span>
                        )}
                      </div>
                    </div>
                  </div>
                </div>

                <div className="post-content-detail">
                  {post.content && (
                    <p className="post-text-detail">{post.content}</p>
                  )}
                </div>

                <div className="post-actions-detail">
                  <button
                    className={`action-button-detail like-button ${post.isLiked ? 'liked' : ''}`}
                    onClick={handleLike}
                  >
                    <HeartIcon size={20} filled={post.isLiked} />
                    <span>{post.likesCount > 0 ? post.likesCount : 'Thích'}</span>
                  </button>
                  <button className="action-button-detail comment-button" disabled>
                    <CommentIcon size={20} />
                    <span>{post.comments.length > 0 ? post.comments.length : 'Bình luận'}</span>
                  </button>
                </div>

                <div className="comments-section-detail">
                  <h3 className="comments-title">Bình luận ({post.comments.length})</h3>
                  <CommentSection
                    postId={post.id}
                    comments={post.comments}
                    currentUserId={currentUserId}
                    onComment={handleComment}
                  />
                </div>
              </div>
            </GlassPanel>
          </main>

      {/* Zoom Modal - Render ở root level để đè lên mọi thứ */}
      {showZoomModal && post?.images && post.images.length > 0 && createPortal(
        <div className="zoom-modal-overlay" onClick={() => setShowZoomModal(false)}>
          <div className="zoom-modal-content" onClick={(e) => e.stopPropagation()}>
            <button
              className="zoom-modal-close"
              onClick={() => setShowZoomModal(false)}
              aria-label="Đóng"
            >
              <CloseIcon size={28} />
            </button>
            <div className="zoom-image-wrapper">
              <img
                key={zoomImageIndex}
                src={post.images[zoomImageIndex]}
                alt={`Zoomed image ${zoomImageIndex + 1}`}
                className="zoom-modal-image image-transition"
              />
              {hasMultipleImages && (
                <div className="zoom-modal-indicators">
                  {post.images.map((_, index) => (
                    <button
                      key={index}
                      className={`zoom-indicator ${index === zoomImageIndex ? 'active' : ''}`}
                      onClick={() => setZoomImageIndex(index)}
                      aria-label={`Ảnh ${index + 1}`}
                    />
                  ))}
                </div>
              )}
            </div>
            {hasMultipleImages && (
              <>
                <button
                  className="zoom-modal-button zoom-modal-prev"
                  onClick={prevZoomImage}
                  aria-label="Ảnh trước"
                >
                  ‹
                </button>
                <button
                  className="zoom-modal-button zoom-modal-next"
                  onClick={nextZoomImage}
                  aria-label="Ảnh sau"
                >
                  ›
                </button>
                <div className="zoom-modal-counter">
                  {zoomImageIndex + 1} / {post.images.length}
                </div>
              </>
            )}
          </div>
        </div>,
        document.body
      )}
    </div>
  )
}

export default PostDetail

