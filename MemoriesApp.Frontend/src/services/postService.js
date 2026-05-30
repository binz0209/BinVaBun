import api from './authService'

export const postService = {
  getPosts: async (params = {}) => {
    const { page = 1, pageSize = 10, content, timeType, beforeDate, afterDate, exactDate, fromDate, toDate } = params
    const queryParams = { page, pageSize }
    
    if (content) queryParams.content = content
    if (timeType) queryParams.timeType = timeType
    if (beforeDate) queryParams.beforeDate = beforeDate
    if (afterDate) queryParams.afterDate = afterDate
    if (exactDate) queryParams.exactDate = exactDate
    if (fromDate) queryParams.fromDate = fromDate
    if (toDate) queryParams.toDate = toDate
    
    const response = await api.get('/posts', { params: queryParams })
    return response.data
  },

  createPost: async (content, images) => {
    const formData = new FormData()
    formData.append('content', content)
    if (images && images.length > 0) {
      images.forEach((image) => {
        formData.append('images', image)
      })
    }
    const response = await api.post('/posts', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
    return response.data
  },

  toggleLike: async (postId) => {
    const response = await api.post(`/posts/${postId}/like`)
    return response.data
  },

  addComment: async (postId, content) => {
    console.log('Sending comment - postId:', postId, 'content:', content, 'type of content:', typeof content)
    
    // Đảm bảo content là string
    const commentContent = String(content || '').trim()
    
    if (!commentContent) {
      throw new Error('Nội dung bình luận không được để trống')
    }
    
    const payload = {
      content: commentContent
    }
    
    console.log('Payload to send:', JSON.stringify(payload))
    
    const response = await api.post(`/posts/${postId}/comments`, payload, {
      headers: {
        'Content-Type': 'application/json'
      }
    })
    console.log('Comment response:', response.data)
    return response.data
  },

  updatePost: async (postId, content, keepImages, newImages) => {
    const formData = new FormData()
    formData.append('content', content)
    if (keepImages && keepImages.length > 0) {
      keepImages.forEach((url) => {
        formData.append('KeepImages', url)
      })
    }
    if (newImages && newImages.length > 0) {
      newImages.forEach((image) => {
        formData.append('NewImages', image)
      })
    }
    const response = await api.put(`/posts/${postId}`, formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
    return response.data
  },

  deletePost: async (postId) => {
    await api.delete(`/posts/${postId}`)
    // Không cần return data, chỉ cần xóa thành công
  },

  getPost: async (postId) => {
    const response = await api.get(`/posts/${postId}`)
    return response.data
  },

  getComments: async (postId, page = 1, pageSize = 10) => {
    const response = await api.get(`/posts/${postId}/comments`, {
      params: { page, pageSize }
    })
    return response.data
  }
}

