import './SkeletonLoader.css'

/**
 * SkeletonLoader — hiển thị trong khi AuthContext đang kiểm tra token.
 * Thiết kế khớp với theme light/dark của app (dùng CSS variables).
 */
export function SkeletonLoader() {
  return (
    <div className="skeleton-page" role="status" aria-label="Đang tải...">
      {/* ── Navbar ── */}
      <div className="skeleton-navbar">
        <div className="skeleton-block skeleton-logo" />
        <div className="skeleton-nav-actions">
          <div className="skeleton-block skeleton-avatar-sm" />
          <div className="skeleton-block skeleton-btn" />
        </div>
      </div>

      {/* ── Body ── */}
      <div className="skeleton-body">
        {/* Sidebar */}
        <aside className="skeleton-sidebar">
          <div className="skeleton-block skeleton-sidebar-item" />
          <div className="skeleton-block skeleton-sidebar-item short" />
          <div className="skeleton-block skeleton-sidebar-item" />
          <div className="skeleton-block skeleton-sidebar-item short" />
        </aside>

        {/* Feed */}
        <main className="skeleton-feed">
          {[1, 2, 3].map((i) => (
            <div key={i} className="skeleton-card">
              <div className="skeleton-card-header">
                <div className="skeleton-block skeleton-avatar" />
                <div className="skeleton-card-meta">
                  <div className="skeleton-block skeleton-title" />
                  <div className="skeleton-block skeleton-subtitle" />
                </div>
              </div>
              <div className="skeleton-block skeleton-image" />
              <div className="skeleton-card-lines">
                <div className="skeleton-block skeleton-line" />
                <div className="skeleton-block skeleton-line short" />
              </div>
            </div>
          ))}
        </main>
      </div>
    </div>
  )
}
