import api from './authService'

export const profileService = {
  getProfile: async () => {
    const response = await api.get('/profile')
    return response.data
  },

  updateProfile: async (displayName, avatar) => {
    const formData = new FormData()
    formData.append('displayName', displayName)
    if (avatar) {
      formData.append('avatar', avatar)
    }
    const response = await api.put('/profile', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
    return response.data
  },

  changePassword: async (oldPassword, newPassword) => {
    const response = await api.post('/profile/change-password', {
      oldPassword,
      newPassword
    })
    return response.data
  }
}

