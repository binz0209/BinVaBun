import { useState, useEffect } from 'react'
import { createPortal } from 'react-dom'
import { CloseIcon } from './Icons'
import './FilterModal.css'

function FilterModal({ isOpen, onClose, onApplyFilter }) {
  const [includeContent, setIncludeContent] = useState(false)
  const [contentText, setContentText] = useState('')
  const [timeFilter, setTimeFilter] = useState(false)
  const [timeType, setTimeType] = useState('') // 'before', 'after', 'exact', 'range'
  const [beforeDate, setBeforeDate] = useState('')
  const [afterDate, setAfterDate] = useState('')
  const [exactDate, setExactDate] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')

  const handleApply = () => {
    const filter = {
      includeContent: includeContent ? contentText : '',
      timeFilter: timeFilter ? {
        type: timeType,
        beforeDate: timeType === 'before' ? beforeDate : '',
        afterDate: timeType === 'after' ? afterDate : '',
        exactDate: timeType === 'exact' ? exactDate : '',
        fromDate: timeType === 'range' ? fromDate : '',
        toDate: timeType === 'range' ? toDate : ''
      } : null
    }
    onApplyFilter(filter)
    onClose()
  }

  const handleReset = () => {
    setIncludeContent(false)
    setContentText('')
    setTimeFilter(false)
    setTimeType('')
    setBeforeDate('')
    setAfterDate('')
    setExactDate('')
    setFromDate('')
    setToDate('')
    onApplyFilter({ includeContent: '', timeFilter: null })
  }

  // Prevent body scroll khi modal mở
  useEffect(() => {
    if (!isOpen) return

    const originalOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    // Prevent scroll propagation khi scroll đến cuối modal
    const handleWheel = (e) => {
      const modalContent = e.currentTarget.querySelector('.filter-modal-content')
      if (!modalContent) return

      const { scrollTop, scrollHeight, clientHeight } = modalContent
      const isAtTop = scrollTop === 0
      const isAtBottom = scrollTop + clientHeight >= scrollHeight - 1

      if ((isAtTop && e.deltaY < 0) || (isAtBottom && e.deltaY > 0)) {
        e.preventDefault()
      }
    }

    const overlay = document.querySelector('.filter-modal-overlay')
    if (overlay) {
      overlay.addEventListener('wheel', handleWheel, { passive: false })
    }

    return () => {
      if (overlay) {
        overlay.removeEventListener('wheel', handleWheel)
      }
      document.body.style.overflow = originalOverflow
    }
  }, [isOpen])

  if (!isOpen) return null

  return createPortal(
    <div className="filter-modal-overlay" onClick={onClose}>
      <div className="filter-modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="filter-modal-header">
          <h2>Lọc bài viết</h2>
          <button className="filter-modal-close" onClick={onClose}>
            <CloseIcon size={24} />
          </button>
        </div>

        <div className="filter-modal-body">
          {/* Bao gồm nội dung */}
          <div className="filter-option">
            <label className="filter-checkbox-label">
              <input
                type="checkbox"
                checked={includeContent}
                onChange={(e) => setIncludeContent(e.target.checked)}
              />
              <span>Bao gồm nội dung</span>
            </label>
            {includeContent && (
              <input
                type="text"
                className="filter-content-input"
                placeholder="Nhập nội dung cần tìm..."
                value={contentText}
                onChange={(e) => setContentText(e.target.value)}
              />
            )}
          </div>

          {/* Thời gian */}
          <div className="filter-option">
            <label className="filter-checkbox-label">
              <input
                type="checkbox"
                checked={timeFilter}
                onChange={(e) => {
                  setTimeFilter(e.target.checked)
                  if (!e.target.checked) {
                    setTimeType('')
                  }
                }}
              />
              <span>Thời gian</span>
            </label>
            {timeFilter && (
              <div className="filter-time-options">
                <select
                  className="filter-time-type-select"
                  value={timeType}
                  onChange={(e) => setTimeType(e.target.value)}
                >
                  <option value="">Chọn loại lọc thời gian</option>
                  <option value="before">Trước ngày</option>
                  <option value="after">Sau ngày</option>
                  <option value="exact">Ngày chính xác</option>
                  <option value="range">Từ ngày ... tới ngày ...</option>
                </select>

                {timeType === 'before' && (
                  <input
                    type="date"
                    className="filter-date-input"
                    value={beforeDate}
                    onChange={(e) => setBeforeDate(e.target.value)}
                  />
                )}

                {timeType === 'after' && (
                  <input
                    type="date"
                    className="filter-date-input"
                    value={afterDate}
                    onChange={(e) => setAfterDate(e.target.value)}
                  />
                )}

                {timeType === 'exact' && (
                  <input
                    type="date"
                    className="filter-date-input"
                    value={exactDate}
                    onChange={(e) => setExactDate(e.target.value)}
                  />
                )}

                {timeType === 'range' && (
                  <div className="filter-date-range">
                    <input
                      type="date"
                      className="filter-date-input"
                      placeholder="Từ ngày"
                      value={fromDate}
                      onChange={(e) => setFromDate(e.target.value)}
                    />
                    <span className="filter-date-separator">đến</span>
                    <input
                      type="date"
                      className="filter-date-input"
                      placeholder="Tới ngày"
                      value={toDate}
                      onChange={(e) => setToDate(e.target.value)}
                    />
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        <div className="filter-modal-actions">
          <button className="filter-reset-button" onClick={handleReset}>
            Đặt lại
          </button>
          <button className="filter-apply-button" onClick={handleApply}>
            Áp dụng
          </button>
        </div>
      </div>
    </div>,
    document.body
  )
}

export default FilterModal

