'use client';

import { useCallback, useEffect, useState } from 'react';

export type LimitEventAction = 'set' | 'reset' | 'scenario' | 'cache';

export type LimitEventLogEntry = {
  id: string;
  atIso: string;
  action: LimitEventAction;
  detail: string;
};

const STORAGE_KEY = 'regkasse.dev-limit-test-log';
const MAX_ENTRIES = 50;

function readStored(): LimitEventLogEntry[] {
  if (typeof window === 'undefined') return [];
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [];
    return parsed.filter(
      (row): row is LimitEventLogEntry =>
        typeof row === 'object' &&
        row != null &&
        typeof (row as LimitEventLogEntry).id === 'string' &&
        typeof (row as LimitEventLogEntry).atIso === 'string' &&
        typeof (row as LimitEventLogEntry).action === 'string' &&
        typeof (row as LimitEventLogEntry).detail === 'string'
    );
  } catch {
    return [];
  }
}

function persist(entries: LimitEventLogEntry[]): void {
  if (typeof window === 'undefined') return;
  try {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(entries.slice(0, MAX_ENTRIES)));
  } catch {
    // sessionStorage may be unavailable
  }
}

export function useLimitEventLog() {
  const [entries, setEntries] = useState<LimitEventLogEntry[]>([]);

  useEffect(() => {
    setEntries(readStored());
  }, []);

  const append = useCallback((action: LimitEventAction, detail: string) => {
    setEntries((prev) => {
      const next: LimitEventLogEntry[] = [
        {
          id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
          atIso: new Date().toISOString(),
          action,
          detail,
        },
        ...prev,
      ].slice(0, MAX_ENTRIES);
      persist(next);
      return next;
    });
  }, []);

  const clear = useCallback(() => {
    persist([]);
    setEntries([]);
  }, []);

  return { entries, append, clear };
}
