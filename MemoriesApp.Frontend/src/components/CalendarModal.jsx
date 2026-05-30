import { useState, useEffect } from 'react'
import { createPortal } from 'react-dom'
import { CloseIcon } from './Icons'
import { calendarService } from '../services/calendarService'
import signalRService from '../services/signalRService'
import './CalendarModal.css'

const DAYS_OF_WEEK = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7']
const MONTHS = ['Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6', 'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12']

function CalendarModal({ isOpen, onClose }) {
  const [currentDate, setCurrentDate] = useState(new Date())
  const [markedDates, setMarkedDates] = useState([])
  
  // Local changes state
  const [localAddedDates, setLocalAddedDates] = useState([])
  const [localRemovedDateIds, setLocalRemovedDateIds] = useState([])
  
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (isOpen) {
      loadMarkedDates()
    }
  }, [isOpen])

  useEffect(() => {
    // Listen for real-time updates
    const handleDateMarked = (newDate) => {
      setMarkedDates(prev => {
        if (prev.some(d => d.id === newDate.id)) return prev
        return [...prev, newDate]
      })
    }

    const handleDateUnmarked = (deletedId) => {
      setMarkedDates(prev => prev.filter(d => d.id !== deletedId))
    }

    signalRService.on('DateMarked', handleDateMarked)
    signalRService.on('DateUnmarked', handleDateUnmarked)

    return () => {
      signalRService.off('DateMarked', handleDateMarked)
      signalRService.off('DateUnmarked', handleDateUnmarked)
    }
  }, [])

  useEffect(() => {
    if (!isOpen) return

    const originalOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    return () => {
      document.body.style.overflow = originalOverflow
    }
  }, [isOpen])

  const loadMarkedDates = async () => {
    try {
      setLoading(true)
      const data = await calendarService.getMarkedDates()
      setMarkedDates(data)
    } catch (error) {
      console.error('Error loading marked dates:', error)
    } finally {
      setLoading(false)
    }
  }

  const handlePrevMonth = () => {
    setCurrentDate(new Date(currentDate.getFullYear(), currentDate.getMonth() - 1, 1))
  }

  const handleNextMonth = () => {
    setCurrentDate(new Date(currentDate.getFullYear(), currentDate.getMonth() + 1, 1))
  }

  const isSameDay = (date1, date2) => {
    return date1.getFullYear() === date2.getFullYear() &&
           date1.getMonth() === date2.getMonth() &&
           date1.getDate() === date2.getDate()
  }

  const isMarkedVisually = (day) => {
    // Is it in the original data and not removed?
    const originalDate = markedDates.find(md => isSameDay(new Date(md.date), day))
    if (originalDate) {
      return !localRemovedDateIds.includes(originalDate.id)
    }
    // Is it newly added?
    return localAddedDates.some(ld => isSameDay(new Date(ld), day))
  }

  const handleDayClick = (day) => {
    if (!day) return
    
    const dateStr = `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}T00:00:00Z`
    
    const originalDate = markedDates.find(md => isSameDay(new Date(md.date), day))
    
    if (isMarkedVisually(day)) {
      // Unmark it
      if (originalDate) {
        setLocalRemovedDateIds(prev => [...prev, originalDate.id])
      } else {
        setLocalAddedDates(prev => prev.filter(ld => !isSameDay(new Date(ld), day)))
      }
    } else {
      // Mark it
      if (originalDate && localRemovedDateIds.includes(originalDate.id)) {
        setLocalRemovedDateIds(prev => prev.filter(id => id !== originalDate.id))
      } else {
        setLocalAddedDates(prev => [...prev, dateStr])
      }
    }
  }

  const handleSave = async () => {
    if (localAddedDates.length === 0 && localRemovedDateIds.length === 0) {
      handleClose()
      return
    }

    try {
      setSaving(true)
      await calendarService.batchUpdate(localAddedDates, localRemovedDateIds)
      // signalR will trigger updates, but we can optimistically close
      handleClose()
    } catch (error) {
      console.error('Error batch updating dates:', error)
      const errorMsg = error.response?.data?.message || error.response?.data?.title || error.message || 'Unknown error';
      alert('Không thể lưu: ' + errorMsg)
    } finally {
      setSaving(false)
    }
  }

  const handleClose = () => {
    setLocalAddedDates([])
    setLocalRemovedDateIds([])
    onClose()
  }

  const calculateStats = () => {
    // Start with original dates
    let allValidDates = [...markedDates.filter(md => !localRemovedDateIds.includes(md.id)).map(md => new Date(md.date))]
    
    // Add local additions
    allValidDates = [...allValidDates, ...localAddedDates.map(ld => new Date(ld))]

    const total = allValidDates.length
    const thisYear = allValidDates.filter(d => d.getFullYear() === currentDate.getFullYear()).length
    const thisMonth = allValidDates.filter(d => d.getFullYear() === currentDate.getFullYear() && d.getMonth() === currentDate.getMonth()).length

    return { total, thisYear, thisMonth }
  }

  const renderCalendar = () => {
    const year = currentDate.getFullYear()
    const month = currentDate.getMonth()
    
    const firstDay = new Date(year, month, 1).getDay()
    const daysInMonth = new Date(year, month + 1, 0).getDate()
    
    const today = new Date()
    const days = []

    // Empty cells before the first day of the month
    for (let i = 0; i < firstDay; i++) {
      days.push(<div key={`empty-start-${i}`} className="calendar-day empty"></div>)
    }

    for (let i = 1; i <= daysInMonth; i++) {
      const currentDay = new Date(year, month, i)
      const isToday = isSameDay(currentDay, today)
      const isMarked = isMarkedVisually(currentDay)

      days.push(
        <div 
          key={`day-${i}`} 
          className={`calendar-day ${isToday ? 'today' : ''} ${isMarked ? 'marked' : ''}`}
          onClick={() => handleDayClick(currentDay)}
        >
          {i}
        </div>
      )
    }

    // Thêm các ô trống ở cuối để luôn luôn có đủ 42 ô (6 tuần x 7 ngày), tránh thay đổi chiều cao
    const totalCells = days.length
    const remainingCells = 42 - totalCells
    for (let i = 0; i < remainingCells; i++) {
      days.push(<div key={`empty-end-${i}`} className="calendar-day empty"></div>)
    }

    return days
  }

  if (!isOpen) return null

  const stats = calculateStats()
  const hasChanges = localAddedDates.length > 0 || localRemovedDateIds.length > 0

  return createPortal(
    <div className="calendar-modal-overlay" onClick={handleClose}>
      <div className="calendar-modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="calendar-modal-header">
          <h2>Kỷ niệm của chúng mình</h2>
          <button className="calendar-modal-close" onClick={handleClose}>
            <CloseIcon size={24} />
          </button>
        </div>
        
        <div className="calendar-modal-body">
          <div className="calendar-controls">
            <button className="calendar-nav-btn" onClick={handlePrevMonth}>‹</button>
            <div className="calendar-month-year">
              {MONTHS[currentDate.getMonth()]} {currentDate.getFullYear()}
            </div>
            <button className="calendar-nav-btn" onClick={handleNextMonth}>›</button>
          </div>

          <div className="calendar-grid">
            {DAYS_OF_WEEK.map(day => (
              <div key={day} className="calendar-day-header">{day}</div>
            ))}
            {renderCalendar()}
          </div>
          
          <div className="calendar-stats">
            <div className="stat-item">
              <span className="stat-value">{stats.thisMonth}</span>
              <span className="stat-label">Tháng này</span>
            </div>
            <div className="stat-item">
              <span className="stat-value">{stats.thisYear}</span>
              <span className="stat-label">Năm nay</span>
            </div>
            <div className="stat-item">
              <span className="stat-value">{stats.total}</span>
              <span className="stat-label">Tổng cộng</span>
            </div>
          </div>
        </div>

        <div className="calendar-modal-footer">
          <button 
            className="calendar-save-btn" 
            onClick={handleSave}
            disabled={saving || (!hasChanges && !saving)}
          >
            {saving ? 'Đang lưu...' : (hasChanges ? 'Lưu thay đổi' : 'Đóng')}
          </button>
        </div>
      </div>
    </div>,
    document.body
  )
}

export default CalendarModal
