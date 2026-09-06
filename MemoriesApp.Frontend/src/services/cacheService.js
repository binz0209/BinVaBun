/**
 * cacheService.js — sessionStorage cache layer
 * Stale-while-revalidate: hiển thị data cũ ngay → fetch mới ở background → update UI
 */

const DEFAULT_TTL = 5 * 60 * 1000 // 5 phút

export const cacheService = {
  /**
   * Lưu data vào cache với TTL
   */
  set(key, data, ttl = DEFAULT_TTL) {
    try {
      const entry = {
        data,
        timestamp: Date.now(),
        ttl
      }
      sessionStorage.setItem(`cache_${key}`, JSON.stringify(entry))
    } catch (e) {
      // sessionStorage đầy → xóa hết cache cũ
      this.clearAll()
    }
  },

  /**
   * Lấy data từ cache. Trả về null nếu không có hoặc đã expired.
   * Nếu allowStale = true, trả về data dù đã expired (cho stale-while-revalidate).
   */
  get(key, allowStale = false) {
    try {
      const raw = sessionStorage.getItem(`cache_${key}`)
      if (!raw) return null

      const entry = JSON.parse(raw)
      const age = Date.now() - entry.timestamp

      if (age > entry.ttl && !allowStale) {
        sessionStorage.removeItem(`cache_${key}`)
        return null
      }

      return entry.data
    } catch {
      return null
    }
  },

  /**
   * Kiểm tra cache còn fresh không (chưa expired)
   */
  isFresh(key) {
    try {
      const raw = sessionStorage.getItem(`cache_${key}`)
      if (!raw) return false

      const entry = JSON.parse(raw)
      return (Date.now() - entry.timestamp) < entry.ttl
    } catch {
      return false
    }
  },

  /**
   * Xóa một key
   */
  remove(key) {
    sessionStorage.removeItem(`cache_${key}`)
  },

  /**
   * Xóa tất cả cache
   */
  clearAll() {
    const keysToRemove = []
    for (let i = 0; i < sessionStorage.length; i++) {
      const key = sessionStorage.key(i)
      if (key?.startsWith('cache_')) {
        keysToRemove.push(key)
      }
    }
    keysToRemove.forEach(k => sessionStorage.removeItem(k))
  }
}
