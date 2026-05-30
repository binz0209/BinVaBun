import api from './authService'

export const calendarService = {
  getMarkedDates: async () => {
    const response = await api.get('/calendar')
    return response.data
  },

  toggleMarkedDate: async (date) => {
    const response = await api.post('/calendar', { date })
    return response.data
  },

  batchUpdate: async (addedDates, removedDateIds) => {
    const response = await api.post('/calendar/batch', { addedDates, removedDateIds })
    return response.data
  }
}
