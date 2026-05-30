import { useState } from 'react'
import { postService } from '../services/postService'
import { CameraIcon } from './Icons'
import './PostForm.css'

function PostForm({ onPostCreated }) {
  const [content, setContent] = useState('')
  const [images, setImages] = useState([])
  const [previewImages, setPreviewImages] = useState([])
  const [loading, setLoading] = useState(false)

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
      setContent('')
      setImages([])
      setPreviewImages([])
      e.target.reset()
    } catch (error) {
      console.error('Error creating post:', error)
      alert('Có lỗi xảy ra khi đăng bài')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="post-form-container">
      <form onSubmit={handleSubmit} className="post-form">
        <textarea
          value={content}
          onChange={(e) => setContent(e.target.value)}
          placeholder="Chia sẻ kỉ niệm của bạn..."
          className="post-textarea"
          rows="4"
        />

        {previewImages.length > 0 && (
          <div className="image-preview-container">
            {previewImages.map((preview, index) => (
              <div key={index} className="image-preview">
                <img src={preview} alt={`Preview ${index + 1}`} />
                <button
                  type="button"
                  onClick={() => removeImage(index)}
                  className="remove-image-button"
                >
                  ×
                </button>
              </div>
            ))}
          </div>
        )}

        <div className="post-form-actions">
          {previewImages.length === 0 ? (
            <label className="image-upload-button">
              <CameraIcon size={18} /> Chọn ảnh
              <input
                type="file"
                multiple
                accept="image/*"
                onChange={handleImageChange}
                style={{ display: 'none' }}
              />
            </label>
          ) : (
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
          )}
          <button
            type="submit"
            className="submit-button"
            disabled={loading || (!content.trim() && images.length === 0)}
          >
            {loading ? 'Đang đăng...' : 'Đăng bài'}
          </button>
        </div>
      </form>
    </div>
  )
}

export default PostForm

