import { useState, useEffect, useRef, useCallback } from 'react'
import { postService } from '../services/postService'
import { LoadingSpinnerIcon } from './Icons'
import './CommentSection.css'

function CommentSection({ postId, comments: initialComments, currentUserId, onComment }) {
  const [content, setContent] = useState('')
  const [loading, setLoading] = useState(false)
  const [comments, setComments] = useState(initialComments || [])
  const [loadingComments, setLoadingComments] = useState(false)
  const [currentPage, setCurrentPage] = useState(1)
  const [hasMore, setHasMore] = useState(true)
  const [loadingMoreComments, setLoadingMoreComments] = useState(false)
  const pageSize = 10
  const commentsListRef = useRef(null)
  const commentInputRef = useRef(null) // Ref cho input để focus lại sau khi submit

  // Load more comments khi scroll đến cuối
  const loadMoreComments = useCallback(async () => {
    if (loadingMoreComments || !hasMore) return

    setLoadingMoreComments(true)
    try {
      const nextPage = currentPage + 1
      const response = await postService.getComments(postId, nextPage, pageSize)
      const newComments = response.comments
      
      setComments(prev => [...prev, ...newComments])
      setCurrentPage(nextPage)
      setHasMore(response.hasMore)
    } catch (error) {
      console.error('Error loading more comments:', error)
    } finally {
      setLoadingMoreComments(false)
    }
  }, [postId, currentPage, loadingMoreComments, hasMore, pageSize])

  // Intersection Observer cho infinite scroll comments
  useEffect(() => {
    if (!hasMore || loadingMoreComments) return

    const observerOptions = {
      root: commentsListRef.current,
      rootMargin: '50px',
      threshold: 0.1
    }

    const observerCallback = (entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting && hasMore && !loadingMoreComments) {
          loadMoreComments()
        }
      })
    }

    const observer = new IntersectionObserver(observerCallback, observerOptions)
    const sentinel = document.getElementById(`comment-sentinel-${postId}`)
    
    if (sentinel) {
      observer.observe(sentinel)
    }

    return () => {
      if (sentinel) {
        observer.unobserve(sentinel)
      }
    }
  }, [hasMore, loadingMoreComments, postId, loadMoreComments])

  // Load initial comments nếu chưa có
  useEffect(() => {
    if (initialComments && initialComments.length > 0) {
      setComments(initialComments)
      // Kiểm tra xem còn comments nào không
      if (initialComments.length >= pageSize) {
        setHasMore(true)
      } else {
        setHasMore(false)
      }
      setCurrentPage(1)
    } else if (initialComments && initialComments.length === 0) {
      // Nếu initialComments là mảng rỗng, không load thêm
      setHasMore(false)
    }
    // Không load từ API nếu đã có initialComments từ props
  }, [postId, initialComments])

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!content.trim()) return

    setLoading(true)
    try {
      await onComment(postId, content)
      setContent('')
      // Reload comments sau khi thêm comment mới
      const response = await postService.getComments(postId, 1, pageSize)
      setComments(response.comments)
      setCurrentPage(1)
      setHasMore(response.hasMore)
      
      // Focus lại vào input sau khi submit thành công
      setTimeout(() => {
        if (commentInputRef.current) {
          commentInputRef.current.focus()
        }
      }, 100)
    } catch (error) {
      console.error('Error adding comment:', error)
    } finally {
      setLoading(false)
    }
  }

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

  // Tính toán xem có cần scroll không (nếu có hơn 5 comments)
  const shouldScroll = comments.length > 5

  return (
    <div className="comment-section">
      <div 
        className={`comments-list ${shouldScroll ? 'comments-list-scrollable' : ''}`}
        ref={commentsListRef}
      >
        {loadingComments ? (
          <div className="comments-loading">
            <LoadingSpinnerIcon size={24} className="spinning" />
            <span>Đang tải bình luận...</span>
          </div>
        ) : comments.length === 0 ? (
          <p className="no-comments">Chưa có bình luận nào</p>
        ) : (
          <>
            {comments.map((comment) => (
              <div key={comment.id} className="comment-item">
                {comment.authorAvatar ? (
                  <img src={comment.authorAvatar} alt={comment.authorName} className="comment-avatar" />
                ) : (
                  <div className="comment-avatar-placeholder">
                    {comment.authorName.charAt(0).toUpperCase()}
                  </div>
                )}
                <div className="comment-content">
                  <div className="comment-header">
                    <span className="comment-author">{comment.authorName}</span>
                    <span className="comment-date">{formatDate(comment.createdAt)}</span>
                  </div>
                  <p className="comment-text">{comment.content}</p>
                </div>
              </div>
            ))}
            {hasMore && (
              <div id={`comment-sentinel-${postId}`} className="comment-sentinel">
                {loadingMoreComments && (
                  <div className="comments-loading-more">
                    <LoadingSpinnerIcon size={20} className="spinning" />
                    <span>Đang tải thêm...</span>
                  </div>
                )}
              </div>
            )}
          </>
        )}
      </div>

      <form onSubmit={handleSubmit} className="comment-form">
        <input
          ref={commentInputRef}
          type="text"
          value={content}
          onChange={(e) => setContent(e.target.value)}
          placeholder="Viết bình luận..."
          className="comment-input"
          disabled={loading}
          autoFocus
        />
        <button type="submit" className="comment-submit" disabled={loading || !content.trim()}>
          {loading ? (
            <LoadingSpinnerIcon size={16} className="spinning" />
          ) : (
            'Gửi'
          )}
        </button>
      </form>
    </div>
  )
}

export default CommentSection

