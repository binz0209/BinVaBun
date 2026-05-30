import './Skeleton.css';

export function PostSkeleton() {
  return (
    <div className="skeleton-card">
      <div className="skeleton-header">
        <div className="skeleton-avatar"></div>
        <div className="skeleton-author">
          <div className="skeleton-line w-32"></div>
          <div className="skeleton-line w-24 sm"></div>
        </div>
      </div>
      <div className="skeleton-content">
        <div className="skeleton-line full"></div>
        <div className="skeleton-line full"></div>
        <div className="skeleton-line w-3/4"></div>
      </div>
      <div className="skeleton-actions">
        <div className="skeleton-action"></div>
        <div className="skeleton-action"></div>
      </div>
    </div>
  );
}
