import { useNavigate, useLocation } from 'react-router-dom';
import { HomeIcon, UserIcon, PlusIcon } from './Icons';
import './BottomNav.css';

export function BottomNav({ onOpenCreatePost }) {
  const navigate = useNavigate();
  const location = useLocation();

  const isActive = (path) => location.pathname === path;

  return (
    <nav className="bottom-nav">
      <div className="bottom-nav-content">
        <button 
          className={`nav-item ${isActive('/') ? 'active' : ''}`}
          onClick={() => navigate('/')}
        >
          <HomeIcon size={24} />
          <span>Trang chủ</span>
        </button>

        <button 
          className="nav-item create-btn"
          onClick={onOpenCreatePost}
        >
          <div className="create-icon-wrapper">
            <PlusIcon size={28} />
          </div>
        </button>

        <button 
          className={`nav-item ${isActive('/profile') ? 'active' : ''}`}
          onClick={() => navigate('/profile')}
        >
          <UserIcon size={24} />
          <span>Cá nhân</span>
        </button>
      </div>
    </nav>
  );
}
