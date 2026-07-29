'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

import { copyTextToClipboard } from '@/lib/clipboard';

/**
 * Clipboard helper with HTTPS `navigator.clipboard` + HTTP `execCommand` fallback
 * (via {@link copyTextToClipboard}).
 */
export function useClipboard(resetMs = 2000) {
  const [copied, setCopied] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, []);

  const copy = useCallback(
    async (text: string): Promise<boolean> => {
      const ok = await copyTextToClipboard(text);
      if (!ok) return false;

      setCopied(true);
      if (timerRef.current) clearTimeout(timerRef.current);
      timerRef.current = setTimeout(() => setCopied(false), resetMs);
      return true;
    },
    [resetMs]
  );

  return { copy, copied };
}
