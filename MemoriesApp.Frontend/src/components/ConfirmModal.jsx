import { createPortal } from 'react-dom';
import './ConfirmModal.css';

export function ConfirmModal({ isOpen, onClose, onConfirm, title, message, confirmText = 'Xác nhận', cancelText = 'Hủy' }) {
  if (!isOpen) return null;

  return createPortal(
    <div className="confirm-modal-overlay" onClick={onClose}>
      <div className="confirm-modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="confirm-icon">⚠️</div>
        <h3 className="confirm-title">{title}</h3>
        <p className="confirm-message">{message}</p>
        <div className="confirm-actions">
          <button className="confirm-cancel-btn" onClick={onClose}>
            {cancelText}
          </button>
          <button className="confirm-submit-btn" onClick={() => {
            onConfirm();
            onClose();
          }}>
            {confirmText}
          </button>
        </div>
      </div>
    </div>,
    document.body
  );
}
