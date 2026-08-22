'use client';

import { usePathname, useSearchParams } from 'next/navigation';
import { useEffect, useRef, useState } from 'react';

/** How long the top bar stays visible after a client-side route change. */
export const PAGE_LOADER_VISIBLE_MS = 250;

/**
 * Thin top bar shown briefly on client-side route changes (App Router).
 * Mount inside `<Suspense>` when used from a layout that does not already suspend.
 */
export function PageLoader() {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const searchKey = searchParams.toString();
  const [active, setActive] = useState(false);
  const isFirstRender = useRef(true);

  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }
    setActive(true);
    const timer = window.setTimeout(() => setActive(false), PAGE_LOADER_VISIBLE_MS);
    return () => window.clearTimeout(timer);
  }, [pathname, searchKey]);

  if (!active) return null;

  return (
    <div
      role="progressbar"
      data-page-loader
      aria-hidden
      className="fa-page-loader"
      style={{
        position: 'fixed',
        top: 0,
        left: 0,
        right: 0,
        height: 3,
        zIndex: 9999,
        overflow: 'hidden',
        pointerEvents: 'none',
        background: 'var(--ant-color-fill-quaternary, #f0f0f0)',
      }}
    >
      <div
        style={{
          height: '100%',
          width: '40%',
          background: 'var(--ant-color-primary, #1677ff)',
          animation: 'fa-page-loader-slide 0.7s ease-in-out infinite',
        }}
      />
    </div>
  );
}
