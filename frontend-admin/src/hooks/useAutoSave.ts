'use client';

import { useEffect, useRef, useState } from 'react';

export type UseAutoSaveOptions = {
  /** When false, debounced saves are skipped. Default true. */
  enabled?: boolean;
  /**
   * Skip the first effect run so hydrated/server values do not immediately save.
   * Default true.
   */
  skipInitial?: boolean;
};

export type UseAutoSaveResult = {
  saving: boolean;
  /** True after a successful save until the next pending change. */
  saved: boolean;
  /** True when the last save attempt failed. */
  error: boolean;
};

/**
 * Debounced auto-save for form values.
 * Callers decide persistence (localStorage draft and/or API) inside `onSave`.
 */
export function useAutoSave<T>(
  data: T,
  onSave: (data: T) => Promise<void>,
  delay: number = 3000,
  options?: UseAutoSaveOptions
): UseAutoSaveResult {
  const enabled = options?.enabled ?? true;
  const skipInitial = options?.skipInitial ?? true;

  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState(false);

  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const skipRef = useRef(skipInitial);
  const onSaveRef = useRef(onSave);
  onSaveRef.current = onSave;

  useEffect(() => {
    if (!enabled) return;

    if (skipRef.current) {
      skipRef.current = false;
      return;
    }

    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
    }

    setSaved(false);
    setError(false);

    timeoutRef.current = setTimeout(() => {
      void (async () => {
        setSaving(true);
        try {
          await onSaveRef.current(data);
          setSaved(true);
          setError(false);
        } catch {
          setError(true);
          setSaved(false);
        } finally {
          setSaving(false);
        }
      })();
    }, delay);

    return () => {
      if (timeoutRef.current) {
        clearTimeout(timeoutRef.current);
      }
    };
    // Intentionally depend on `data` identity/value changes from the form.
  }, [data, delay, enabled]);

  return { saving, saved, error };
}

/** localStorage draft helpers for form recovery (optional companion to useAutoSave). */
export function readAutoSaveDraft<T>(storageKey: string): T | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = window.localStorage.getItem(storageKey);
    if (!raw) return null;
    return JSON.parse(raw) as T;
  } catch {
    return null;
  }
}

export function writeAutoSaveDraft(storageKey: string, value: unknown): void {
  if (typeof window === 'undefined') return;
  window.localStorage.setItem(storageKey, JSON.stringify(value));
}

export function clearAutoSaveDraft(storageKey: string): void {
  if (typeof window === 'undefined') return;
  window.localStorage.removeItem(storageKey);
}
