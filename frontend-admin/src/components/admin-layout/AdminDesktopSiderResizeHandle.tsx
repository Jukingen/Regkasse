'use client';

import React, { useCallback, useEffect, useRef } from 'react';

type AdminDesktopSiderResizeHandleProps = {
  onWidthChange: (nextWidth: number) => void;
  minWidth: number;
  maxWidth: number;
  ariaLabel: string;
};

/**
 * Desktop-only drag handle on the right edge of the Sider. Uses pointer capture for stable dragging.
 */
export function AdminDesktopSiderResizeHandle({
  onWidthChange,
  minWidth,
  maxWidth,
  ariaLabel,
}: AdminDesktopSiderResizeHandleProps) {
  const startXRef = useRef(0);
  const startWidthRef = useRef(0);
  const activeRef = useRef(false);

  const clamp = useCallback(
    (w: number) => Math.min(maxWidth, Math.max(minWidth, w)),
    [minWidth, maxWidth]
  );

  useEffect(() => {
    const onMove = (e: PointerEvent) => {
      if (!activeRef.current) return;
      const delta = e.clientX - startXRef.current;
      onWidthChange(clamp(startWidthRef.current + delta));
    };
    const onUp = () => {
      activeRef.current = false;
    };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
    window.addEventListener('pointercancel', onUp);
    return () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
      window.removeEventListener('pointercancel', onUp);
    };
  }, [clamp, onWidthChange]);

  const onPointerDown = useCallback(
    (e: React.PointerEvent) => {
      e.preventDefault();
      activeRef.current = true;
      startXRef.current = e.clientX;
      const aside = (e.currentTarget as HTMLElement).closest(
        '.ant-layout-sider'
      ) as HTMLElement | null;
      startWidthRef.current = aside?.getBoundingClientRect().width ?? minWidth;
      (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
    },
    [minWidth]
  );

  return (
    <div
      className="admin-desktop-sider-resize-handle"
      role="separator"
      aria-orientation="vertical"
      aria-label={ariaLabel}
      onPointerDown={onPointerDown}
    />
  );
}
