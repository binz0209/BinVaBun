import React from 'react';
import { useMouseParallax } from '../hooks/useMouseParallax';

/**
 * A highly reusable spatial glass panel component.
 * It applies standard Liquid Glass tokens and optional 3D parallax hover.
 * 
 * @param {boolean} enable3D Whether to apply mouse-based 3D rotation
 * @param {boolean} interactive Whether this panel should glow/lift on hover
 * @param {string} className Additional CSS classes
 * @param {React.ReactNode} children
 */
export function GlassPanel({ enable3D = false, interactive = false, className = '', children, ...props }) {
  const { ref, style, onMouseMove, onMouseLeave } = useMouseParallax({ maxTilt: 3, scale: 1.01 });

  const containerStyle = enable3D ? style : undefined;
  const hoverHandlers = enable3D ? { onMouseMove, onMouseLeave } : {};

  return (
    <div 
      ref={enable3D ? ref : undefined}
      className={`glass-panel ${interactive ? 'interactive' : ''} ${className}`}
      style={containerStyle}
      {...hoverHandlers}
      {...props}
    >
      {children}
    </div>
  );
}
