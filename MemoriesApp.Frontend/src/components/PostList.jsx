import PostCard from './PostCard'
import './PostList.css'

function PostList({ posts, currentUserId, onLike, onComment, onUpdate, onDelete }) {
  if (posts.length === 0) {
    return (
      <div className="empty-state">
        <div className="empty-icon">🌱</div>
        <h3 className="empty-title">Chưa có kỷ niệm nào</h3>
        <p className="empty-subtitle">Hãy là người đầu tiên chia sẻ khoảnh khắc đáng nhớ của hai bạn!</p>
      </div>
    )
  }

  return (
    <div className="post-list">
      {posts.map((post) => (
        <PostCard
          key={post.id}
          post={post}
          currentUserId={currentUserId}
          onLike={onLike}
          onComment={onComment}
          onUpdate={onUpdate}
          onDelete={onDelete}
        />
      ))}
    </div>
  )
}

export default PostList
