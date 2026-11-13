'use client';

import { clsx } from 'clsx';
import { usePathname } from 'next/navigation';
import { ReactNode, useEffect, useRef, useState } from 'react';

import styles from './PageTransition.module.css';

const ENTER_MS = 250;

/**
 * Lightweight CSS enter transition for App Router navigations.
 * Does not remount children (preserves nested layout state and loading UI).
 * Place via `template.tsx` so the shell (sidebar/header) stays stable.
 */
export function PageTransition({ children }: { children: ReactNode }) {
  const pathname = usePathname() ?? '';
  const prevPathnameRef = useRef(pathname);
  const [enter, setEnter] = useState(true);

  useEffect(() => {
    const pathChanged = prevPathnameRef.current !== pathname;
    prevPathnameRef.current = pathname;

    let raf = 0;
    if (pathChanged) {
      // Drop the class for one frame so the enter animation can restart.
      setEnter(false);
      raf = requestAnimationFrame(() => setEnter(true));
    }

    const clearTimer = window.setTimeout(() => setEnter(false), ENTER_MS);
    return () => {
      if (raf) cancelAnimationFrame(raf);
      window.clearTimeout(clearTimer);
    };
  }, [pathname]);

  return (
    <div
      className={clsx(styles.root, enter && styles.enter)}
      data-page-transition=""
      data-pathname={pathname}
    >
      {children}
    </div>
  );
}

/** Duration used by CSS / tests — keep in sync with `PageTransition.module.css`. */
export const PAGE_TRANSITION_MS = ENTER_MS;
