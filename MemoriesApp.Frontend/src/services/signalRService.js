import * as signalR from '@microsoft/signalr'

class SignalRService {
  constructor() {
    this.connection = null
    this.listeners = new Map()
  }

  connect(token) {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      return Promise.resolve()
    }

    // VITE_API_URL có /api ở cuối, cần bỏ /api để lấy base URL cho SignalR
    let apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'
    // Loại bỏ BOM character và trim whitespace
    apiUrl = apiUrl.replace(/^\uFEFF/, '').trim()
    const baseUrl = apiUrl.replace(/\/api$/, '') // Bỏ /api ở cuối nếu có
    const hubUrl = `${baseUrl}/memoriesHub`
    
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token,
        skipNegotiation: false,
        headers: {
          'Authorization': `Bearer ${token}`
        }
      })
      .withAutomaticReconnect()
      .build()

    // Register all event handlers
    this.connection.on('NewPost', (post) => {
      this.emit('NewPost', post)
    })

    this.connection.on('PostUpdated', (post) => {
      this.emit('PostUpdated', post)
    })

    this.connection.on('PostDeleted', (postId) => {
      this.emit('PostDeleted', postId)
    })

    this.connection.on('NewComment', (postId, comment) => {
      this.emit('NewComment', { postId, comment })
    })

    this.connection.on('PostLiked', (postId, isLiked, likesCount, userId) => {
      this.emit('PostLiked', { postId, isLiked, likesCount, userId })
    })

    this.connection.on('NewNotification', (notification) => {
      this.emit('NewNotification', notification)
    })

    this.connection.on('DateMarked', (date) => {
      this.emit('DateMarked', date)
    })

    this.connection.on('DateUnmarked', (dateId) => {
      this.emit('DateUnmarked', dateId)
    })

    this.connection.on('DatesBatchUpdated', (data) => {
      this.emit('DatesBatchUpdated', data)
    })

    return this.connection.start()
  }

  disconnect() {
    if (this.connection) {
      return this.connection.stop()
    }
    return Promise.resolve()
  }

  on(event, callback) {
    if (!this.listeners.has(event)) {
      this.listeners.set(event, [])
    }
    this.listeners.get(event).push(callback)
  }

  off(event, callback) {
    if (this.listeners.has(event)) {
      const callbacks = this.listeners.get(event)
      const index = callbacks.indexOf(callback)
      if (index > -1) {
        callbacks.splice(index, 1)
      }
    }
  }

  emit(event, data) {
    if (this.listeners.has(event)) {
      this.listeners.get(event).forEach(callback => callback(data))
    }
  }
}

export default new SignalRService()

