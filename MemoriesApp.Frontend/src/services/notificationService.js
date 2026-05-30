import api from './authService'

export const notificationService = {
  getNotifications: async () => {
    const response = await api.get('/notifications')
    return response.data
  },

  markAsRead: async (id) => {
    await api.post(`/notifications/${id}/read`)
  },

  markAllAsRead: async () => {
    await api.post('/notifications/read-all')
  }
}

