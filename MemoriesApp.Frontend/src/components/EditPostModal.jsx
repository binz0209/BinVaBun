import { useState, useEffect } from 'react'
import { createPortal } from 'react-dom'
import { CameraIcon } from './Icons'
import './EditPostModal.css'

function EditPostModal({ post, onClose, onSave }) {
  const [content, setContent] = useState(post.content)
  const [keepImages, setKeepImages] = useState(post.images || [])
  const [newImages, setNewImages] = useState([])
  const [previewNewImages, setPreviewNewImages] = useState([])
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    setContent(post.content)
    setKeepImages(post.images || [])
    setNewImages([])
    setPreviewNewImages([])
  }, [post])

  // ESC key, click outside handler và prevent body scroll
  useEffect(() => {
    // Lock body scroll khi modal mở
    const originalOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    const handleEsc = (e) => {
      if (e.key === 'Escape' && !loading) {
        onClose()
      }
    }

    // Prevent scroll propagation khi scroll đến cuối modal
    const handleWheel = (e) => {
      const modalContent = e.currentTarget.querySelector('.modal-content')
      if (!modalContent) return

      const { scrollTop, scrollHeight, clientHeight } = modalContent
      const isAtTop = scrollTop === 0
      const isAtBottom = scrollTop + clientHeight >= scrollHeight - 1

      if ((isAtTop && e.deltaY < 0) || (isAtBottom && e.deltaY > 0)) {
        e.preventDefault()
      }
    }

    document.addEventListener('keydown', handleEsc)
    const overlay = document.querySelector('.modal-overlay')
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
  }, [loading, onClose])

  const handleImageChange = (e) => {
    const files = Array.from(e.target.files)
    setNewImages(files)
    
    const previews = files.map(file => URL.createObjectURL(file))
    setPreviewNewImages(previews)
  }

  const removeKeepImage = (index) => {
    const newKeepImages = keepImages.filter((_, i) => i !== index)
    setKeepImages(newKeepImages)
  }

  const removeNewImage = (index) => {
    const newImagesList = newImages.filter((_, i) => i !== index)
    const newPreviews = previewNewImages.filter((_, i) => i !== index)
    setNewImages(newImagesList)
    setPreviewNewImages(newPreviews)
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!content.trim() && keepImages.length === 0 && newImages.length === 0) return

    setLoading(true)
    try {
      await onSave(content, keepImages, newImages)
    } catch (error) {
      alert('Có lỗi xảy ra khi cập nhật bài viết')
    } finally {
      setLoading(false)
    }
  }

  return createPortal(
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>Chỉnh sửa bài viết</h2>
          <button className="close-button" onClick={onClose}>×</button>
        </div>

        <form onSubmit={handleSubmit} className="edit-post-form">
          <textarea
            value={content}
            onChange={(e) => setContent(e.target.value)}
            placeholder="Chia sẻ kỉ niệm của bạn..."
            className="edit-textarea"
            rows="4"
          />

          {keepImages.length > 0 && (
            <div className="images-section">
              <h3>Ảnh hiện tại (click để xóa)</h3>
              <div className="image-preview-container">
                {keepImages.map((image, index) => (
                  <div key={index} className="image-preview">
                    <img src={image} alt={`Keep ${index + 1}`} />
                    <button
                      type="button"
                      onClick={() => removeKeepImage(index)}
                      className="remove-image-button"
                    >
                      ×
                    </button>
                  </div>
                ))}
              </div>
            </div>
          )}

          {previewNewImages.length > 0 && (
            <div className="images-section">
              <h3>Ảnh mới</h3>
              <div className="image-preview-container">
                {previewNewImages.map((preview, index) => (
                  <div key={index} className="image-preview">
                    <img src={preview} alt={`New ${index + 1}`} />
                    <button
                      type="button"
                      onClick={() => removeNewImage(index)}
                      className="remove-image-button"
                    >
                      ×
                    </button>
                  </div>
                ))}
              </div>
            </div>
          )}

          <div className="modal-actions">
            <label className="image-upload-button">
              <CameraIcon size={18} /> Thêm ảnh
              <input
                type="file"
                multiple
                accept="image/*"
                onChange={handleImageChange}
                style={{ display: 'none' }}
              />
            </label>
            <div className="action-buttons">
              <button type="button" onClick={onClose} className="cancel-button">
                Hủy
              </button>
              <button
                type="submit"
                className="save-button"
                disabled={loading || (!content.trim() && keepImages.length === 0 && newImages.length === 0)}
              >
                {loading ? 'Đang lưu...' : 'Lưu'}
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>,
    document.body
  )
}

export default EditPostModal

