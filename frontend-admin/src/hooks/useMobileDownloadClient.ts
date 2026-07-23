'use client';

import { useEffect, useState } from 'react';

import { isMobileDownloadClient } from '@/lib/download/mobileDownload';

/**
 * Tracks whether the current client should use mobile download UX
 * (UA / coarse pointer / narrow viewport).
 */
export function useMobileDownloadClient(): boolean {
  const [mobile, setMobile] = useState(false);

  useEffect(() => {
    const update = () => setMobile(isMobileDownloadClient());
    update();

    const mqNarrow = window.matchMedia('(max-width: 767px)');
    const mqCoarse = window.matchMedia('(pointer: coarse)');
    const onChange = () => update();

    mqNarrow.addEventListener('change', onChange);
    mqCoarse.addEventListener('change', onChange);
    window.addEventListener('resize', onChange);

    return () => {
      mqNarrow.removeEventListener('change', onChange);
      mqCoarse.removeEventListener('change', onChange);
      window.removeEventListener('resize', onChange);
    };
  }, []);

  return mobile;
}
