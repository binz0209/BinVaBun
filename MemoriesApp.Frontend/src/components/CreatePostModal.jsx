import { useState, useEffect } from 'react'
import { createPortal } from 'react-dom'
import { postService } from '../services/postService'
import { CameraIcon, CloseIcon } from './Icons'
import './CreatePostModal.css'

function CreatePostModal({ isOpen, onClose, onPostCreated }) {
  const [content, setContent] = useState('')
  const [images, setImages] = useState([])
  const [previewImages, setPreviewImages] = useState([])
  const [loading, setLoading] = useState(false)

  // ESC key handler và prevent body scroll
  useEffect(() => {
    if (!isOpen) return

    // Lock body scroll khi modal mở
    const originalOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    const handleEsc = (e) => {
      if (e.key === 'Escape' && !loading) {
        handleClose(false) // Không reset data khi đóng bằng ESC
      }
    }

    // Prevent scroll propagation khi scroll đến cuối modal
    const handleWheel = (e) => {
      const modalContent = e.currentTarget.querySelector('.create-post-modal-content')
      if (!modalContent) return

      const { scrollTop, scrollHeight, clientHeight } = modalContent
      const isAtTop = scrollTop === 0
      const isAtBottom = scrollTop + clientHeight >= scrollHeight - 1

      if ((isAtTop && e.deltaY < 0) || (isAtBottom && e.deltaY > 0)) {
        e.preventDefault()
      }
    }

    document.addEventListener('keydown', handleEsc)
    const overlay = document.querySelector('.create-post-modal-overlay')
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
  }, [isOpen, loading])

  const handleImageChange = (e) => {
    const files = Array.from(e.target.files)
    // Merge với ảnh đã chọn trước đó
    const newImages = [...images, ...files]
    setImages(newImages)
    
    const newPreviews = files.map(file => URL.createObjectURL(file))
    setPreviewImages([...previewImages, ...newPreviews])
    
    // Reset input để có thể chọn lại cùng file
    e.target.value = ''
  }

  const removeImage = (index) => {
    // Revoke URL để tránh memory leak
    URL.revokeObjectURL(previewImages[index])
    
    const newImages = images.filter((_, i) => i !== index)
    const newPreviews = previewImages.filter((_, i) => i !== index)
    setImages(newImages)
    setPreviewImages(newPreviews)
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!content.trim() && images.length === 0) return

    setLoading(true)
    try {
      const newPost = await postService.createPost(content, images)
      onPostCreated(newPost)
      // Reset form
      setContent('')
      setImages([])
      setPreviewImages([])
      onClose()
    } catch (error) {
      console.error('Error creating post:', error)
      alert('Có lỗi xảy ra khi đăng bài')
    } finally {
      setLoading(false)
    }
  }

  const handleClose = (resetData = true) => {
    if (!loading) {
      if (resetData) {
        setContent('')
        setImages([])
        setPreviewImages([])
      }
      onClose()
    }
  }

  if (!isOpen) return null

  return createPortal(
    <div className="create-post-modal-overlay">
      {/* Không đóng khi click outside - chỉ đóng bằng nút X hoặc Hủy */}
      <div className="create-post-modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="create-post-modal-header">
          <h2>Tạo bài viết mới</h2>
          <button className="create-post-modal-close" onClick={() => handleClose(true)} disabled={loading}>
            <CloseIcon size={24} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="create-post-modal-form">
          <textarea
            value={content}
            onChange={(e) => setContent(e.target.value)}
            placeholder="Chia sẻ kỉ niệm của bạn..."
            className="create-post-textarea"
            rows="6"
          />

          {previewImages.length > 0 && (
            <div className="create-post-image-preview-container">
              {previewImages.map((preview, index) => (
                <div key={index} className="create-post-image-preview">
                  <img src={preview} alt={`Preview ${index + 1}`} />
                  <button
                    type="button"
                    onClick={() => removeImage(index)}
                    className="create-post-remove-image-button"
                    disabled={loading}
                  >
                    ×
                  </button>
                </div>
              ))}
            </div>
          )}

          <div className="create-post-modal-actions">
            {previewImages.length === 0 ? (
              <label className="create-post-image-upload-button" htmlFor="create-post-image-input">
                <CameraIcon size={18} style={{ display: 'inline-block', verticalAlign: 'middle', marginRight: '4px' }} />
                Chọn ảnh
                <input
                  id="create-post-image-input"
                  type="file"
                  multiple
                  accept="image/*"
                  onChange={handleImageChange}
                  style={{ display: 'none' }}
                  disabled={loading}
                />
              </label>
            ) : (
              <label className="create-post-image-upload-button" htmlFor="create-post-image-input">
                <CameraIcon size={18} style={{ display: 'inline-block', verticalAlign: 'middle', marginRight: '4px' }} />
                Thêm ảnh
                <input
                  id="create-post-image-input"
                  type="file"
                  multiple
                  accept="image/*"
                  onChange={handleImageChange}
                  style={{ display: 'none' }}
                  disabled={loading}
                />
              </label>
            )}
            <div className="create-post-modal-buttons">
              <button
                type="button"
                onClick={() => handleClose(true)}
                className="create-post-cancel-button"
                disabled={loading}
              >
                Hủy
              </button>
              <button
                type="submit"
                className="create-post-submit-button"
                disabled={loading || (!content.trim() && images.length === 0)}
              >
                {loading ? 'Đang đăng...' : 'Đăng bài'}
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>,
    document.body
  )
}

export default CreatePostModal

