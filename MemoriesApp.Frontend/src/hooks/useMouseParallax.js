import { useState, useCallback, useRef } from 'react';

/**
 * useMouseParallax hook
 * Calculates perspective and rotation transforms based on mouse position
 * to create a 3D Apple-like hover effect on cards.
 * 
 * @param {Object} options Configuration options
 * @param {number} options.maxTilt Maximum tilt angle in degrees
 * @param {number} options.scale Hover scale multiplier
 * @returns {Object} { ref, style, onMouseMove, onMouseLeave }
 */
export function useMouseParallax(options = {}) {
  const { maxTilt = 5, scale = 1.02 } = options;
  const ref = useRef(null);
  const [style, setStyle] = useState({
    transform: 'perspective(1000px) rotateX(0deg) rotateY(0deg) scale(1)',
    transition: 'transform 400ms cubic-bezier(0.175, 0.885, 0.32, 1.15)'
  });

  const onMouseMove = useCallback((e) => {
    if (!ref.current) return;
    
    // Disable heavy 3D effect on mobile devices (using touch/coarse pointers)
    if (window.matchMedia('(pointer: coarse)').matches) return;

    const rect = ref.current.getBoundingClientRect();
    
    // Calculate mouse position relative to the center of the element (-1 to 1)
    const x = (e.clientX - rect.left) / rect.width;
    const y = (e.clientY - rect.top) / rect.height;
    
    const multiplierX = (x - 0.5) * 2;
    const multiplierY = (y - 0.5) * 2;

    // Invert Y axis for natural rotation (mouse up -> element tilts up)
    const rotateX = multiplierY * -maxTilt;
    const rotateY = multiplierX * maxTilt;

    setStyle({
      transform: `perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) scale(${scale})`,
      transition: 'transform 100ms cubic-bezier(0.25, 1, 0.5, 1)' // Faster transition during active tracking
    });
  }, [maxTilt, scale]);

  const onMouseLeave = useCallback(() => {
    setStyle({
      transform: 'perspective(1000px) rotateX(0deg) rotateY(0deg) scale(1)',
      transition: 'transform 800ms cubic-bezier(0.175, 0.885, 0.32, 1.15)' // Smooth spring snap back
    });
  }, []);

  return { ref, style, onMouseMove, onMouseLeave };
}
