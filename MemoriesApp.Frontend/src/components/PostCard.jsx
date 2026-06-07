import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import CommentSection from './CommentSection'
import EditPostModal from './EditPostModal'
import { HeartIcon, CommentIcon, EditIcon, DeleteIcon, MenuDotsIcon, ViewIcon } from './Icons'
import { GlassPanel } from './GlassPanel'
import './PostCard.css'

// Helper function to detect image orientation
const getImageOrientation = (imageUrl, callback) => {
  const img = new Image()
  img.onload = () => {
    callback(img.width > img.height ? 'landscape' : 'portrait')
  }
  img.onerror = () => {
    callback('square') // Default to square if error
  }
  img.src = imageUrl
}

function PostCard({ post, currentUserId, onLike, onComment, onUpdate, onDelete }) {
  const navigate = useNavigate()
  const [showComments, setShowComments] = useState(false)
  const [showEditModal, setShowEditModal] = useState(false)
  const [showMenu, setShowMenu] = useState(false)
  const [isExpanded, setIsExpanded] = useState(false)
  const [isVisible, setIsVisible] = useState(false)
  const menuRef = useRef(null)
  const menuButtonRef = useRef(null)
  const cardRef = useRef(null)
  
  const maxLength = 200 // Độ dài tối đa trước khi truncate
  const shouldTruncate = post.content && post.content.length > maxLength
  const displayContent = isExpanded || !shouldTruncate 
    ? post.content 
    : post.content.substring(0, maxLength) + '...'

  // Scroll-triggered reveal animation
  useEffect(() => {
    const el = cardRef.current
    if (!el) return

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setIsVisible(true)
          observer.unobserve(el)
        }
      },
      { threshold: 0.08, rootMargin: '0px 0px -40px 0px' }
    )

    observer.observe(el)
    return () => observer.unobserve(el)
  }, [])

  // ESC key và click outside handler cho menu
  useEffect(() => {
    if (!showMenu) return

    const handleEsc = (e) => {
      if (e.key === 'Escape') {
        setShowMenu(false)
      }
    }

    const handleClickOutside = (e) => {
      if (
        menuRef.current &&
        !menuRef.current.contains(e.target) &&
        menuButtonRef.current &&
        !menuButtonRef.current.contains(e.target)
      ) {
        setShowMenu(false)
      }
    }

    document.addEventListener('keydown', handleEsc)
    document.addEventListener('mousedown', handleClickOutside)
    return () => {
      document.removeEventListener('keydown', handleEsc)
      document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [showMenu])

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
    <div ref={cardRef} className={`animate-on-scroll ${isVisible ? 'visible' : ''}`}>
    <GlassPanel enable3D={true} className="post-card">
      <div className="post-header">
        <div className="post-author">
          {post.authorAvatar ? (
            <img src={post.authorAvatar} alt={post.authorName} className="author-avatar" />
          ) : (
            <div className="author-avatar-placeholder">
              {post.authorName.charAt(0).toUpperCase()}
            </div>
          )}
          <div>
            <div className="author-name">{post.authorName}</div>
            <div 
              className="post-date"
              onClick={() => navigate(`/post/${post.id}`)}
              style={{ cursor: 'pointer' }}
              title="Xem chi tiết"
            >
              {formatDate(post.createdAt)}
              {post.updatedAt && post.updatedAt !== post.createdAt && (
                <span className="edited-badge"> (đã chỉnh sửa)</span>
              )}
            </div>
          </div>
        </div>
        {(post.isOwner || String(post.authorId) === String(currentUserId)) && (
          <div className="post-actions-menu-container">
            <button
              ref={menuButtonRef}
              className="menu-toggle-button"
              onClick={() => setShowMenu(!showMenu)}
              title="Tùy chọn"
            >
              <MenuDotsIcon size={20} />
            </button>
            {showMenu && (
              <>
                <div className="menu-overlay" onClick={() => setShowMenu(false)}></div>
                <div ref={menuRef} className="post-actions-menu">
                  <button
                    className="menu-item"
                    onClick={() => {
                      navigate(`/post/${post.id}`)
                      setShowMenu(false)
                    }}
                  >
                    <span className="menu-icon"><ViewIcon size={18} /></span>
                    <span>Xem chi tiết</span>
                  </button>
                  <button
                    className="menu-item"
                    onClick={() => {
                      setShowEditModal(true)
                      setShowMenu(false)
                    }}
                  >
                    <span className="menu-icon"><EditIcon size={18} /></span>
                    <span>Chỉnh sửa</span>
                  </button>
                  <button
                    className="menu-item delete-item"
                    onClick={() => {
                      onDelete(post.id)
                      setShowMenu(false)
                    }}
                  >
                    <span className="menu-icon"><DeleteIcon size={18} /></span>
                    <span>Xóa</span>
                  </button>
                </div>
              </>
            )}
          </div>
        )}
      </div>

      <div className="post-content">
        {post.content && (
          <div className="post-text-container">
            <p className="post-text">{displayContent}</p>
            {shouldTruncate && (
              <button 
                className="expand-toggle-button"
                onClick={() => setIsExpanded(!isExpanded)}
              >
                {isExpanded ? 'Ẩn bớt' : 'Xem thêm'}
              </button>
            )}
          </div>
        )}
        
        {post.images && post.images.length > 0 && (
          <PostImages 
            images={post.images} 
            postId={post.id} 
            navigate={navigate}
          />
        )}
      </div>

      <div className="post-actions">
        <button
          className={`action-button like-button ${post.isLiked ? 'liked' : ''}`}
          onClick={() => onLike(post.id)}
        >
          <HeartIcon size={20} filled={post.isLiked} /> {post.likesCount > 0 && <span>{post.likesCount}</span>}
        </button>
        <button
          className="action-button comment-button"
          onClick={() => setShowComments(!showComments)}
        >
          <CommentIcon size={20} /> {post.comments.length > 0 && <span>{post.comments.length}</span>}
        </button>
      </div>

      {showComments && (
        <CommentSection
          postId={post.id}
          comments={post.comments}
          currentUserId={currentUserId}
          onComment={onComment}
        />
      )}

      {showEditModal && (
        <EditPostModal
          post={post}
          onClose={() => setShowEditModal(false)}
          onSave={async (content, keepImages, newImages) => {
            try {
              await onUpdate(post.id, content, keepImages, newImages)
              setShowEditModal(false)
            } catch (error) {
              // Error đã được xử lý trong handleUpdatePost
            }
          }}
        />
      )}
    </GlassPanel>
    </div>
  )
}

// Component for displaying post images with smart layout
function PostImages({ images, postId, navigate }) {
  const [imageOrientations, setImageOrientations] = useState([])
  const [imagesToShow, setImagesToShow] = useState([])

  useEffect(() => {
    if (images.length === 0) return

    // Always show max 4 images
    const displayImages = images.slice(0, 4)
    setImagesToShow(displayImages)

    // Detect orientations for 2-3 images
    if (images.length >= 2 && images.length <= 3) {
      const orientations = []
      let loadedCount = 0
      
      displayImages.forEach((image, index) => {
        getImageOrientation(image, (orientation) => {
          orientations[index] = orientation
          loadedCount++
          
          if (loadedCount === displayImages.length) {
            setImageOrientations(orientations)
          }
        })
      })
    } else {
      setImageOrientations([])
    }
  }, [images])

  const imageCount = images.length
  const displayCount = imagesToShow.length
  const remainingCount = imageCount > 4 ? imageCount - 4 : 0

  // Determine layout class
  let layoutClass = 'post-images-grid'
  
  if (imageCount === 1) {
    layoutClass = 'post-images-single'
  } else if (imageCount === 2) {
    const [first, second] = imageOrientations
    if (first === 'portrait' && second === 'portrait') {
      layoutClass = 'post-images-two-portrait'
    } else if (first === 'landscape' && second === 'landscape') {
      layoutClass = 'post-images-two-landscape'
    } else if ((first === 'portrait' && second === 'landscape') || 
               (first === 'landscape' && second === 'portrait')) {
      layoutClass = 'post-images-two-mixed'
    }
  } else if (imageCount === 3) {
    const [first, second, third] = imageOrientations
    // Trường hợp: ảnh đầu dọc, 2 ảnh còn lại ngang
    if (first === 'portrait' && second === 'landscape' && third === 'landscape') {
      layoutClass = 'post-images-portrait-two-landscape'
    } else if (first === 'portrait') {
      // Ảnh đầu dọc, các ảnh khác không phải cả 2 đều ngang
      layoutClass = 'post-images-portrait-layout'
    } else if (first === 'landscape') {
      layoutClass = 'post-images-landscape-layout'
    }
  }

  return (
    <div className={layoutClass}>
      {imagesToShow.map((image, index) => {
        const isLast = index === 3 && remainingCount > 0
        const isFirst = index === 0
        
        return (
          <div 
            key={index} 
            className={`post-image-wrapper ${isFirst && (imageCount === 2 || imageCount === 3) ? 'first-image' : ''}`}
          >
            <img 
              src={image} 
              alt={`Post image ${index + 1}`}
              onClick={() => navigate(`/post/${postId}?image=${index}`)}
              className={isFirst && (imageCount === 2 || imageCount === 3) ? 'post-image-first' : 'post-image-square'}
              title="Xem chi tiết"
            />
            {isLast && (
              <div className="post-image-overlay">
                <span className="post-image-count">+{remainingCount}</span>
              </div>
            )}
          </div>
        )
      })}
    </div>
  )
}

export default PostCard

