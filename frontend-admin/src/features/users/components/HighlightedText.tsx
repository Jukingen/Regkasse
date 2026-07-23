'use client';

import React, { useMemo } from 'react';

import { escapeRegExp } from '../utils/permissionSearchIndex';

type HighlightedTextProps = {
  text: string;
  query: string;
  /** Optional className for the mark element */
  markClassName?: string;
};

/**
 * Highlights case-insensitive occurrences of `query` inside `text`.
 * Returns plain text when query is empty.
 */
export function HighlightedText({
  text,
  query,
  markClassName = 'permission-search-mark',
}: HighlightedTextProps) {
  const parts = useMemo(() => {
    const q = query.trim();
    if (!q || !text) return null;
    try {
      const re = new RegExp(`(${escapeRegExp(q)})`, 'gi');
      return text.split(re);
    } catch {
      return null;
    }
  }, [text, query]);

  if (!parts) return <>{text}</>;

  const qLower = query.trim().toLowerCase();
  return (
    <>
      {parts.map((part, i) =>
        part.toLowerCase() === qLower ? (
          <mark
            key={`h-${i}`}
            className={markClassName}
            style={{
              backgroundColor: 'rgba(250, 173, 20, 0.35)',
              color: 'inherit',
              padding: 0,
            }}
          >
            {part}
          </mark>
        ) : (
          <React.Fragment key={`t-${i}`}>{part}</React.Fragment>
        )
      )}
    </>
  );
}
