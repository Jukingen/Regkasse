'use client';

import { useMemo, type ReactNode } from 'react';

import { countSearchMatches } from '@/lib/preview/filePreview';

type Props = {
  text: string;
  searchQuery?: string;
  maxHeight?: number | string;
};

function highlightSearch(value: string, query: string): ReactNode {
  const q = query.trim();
  if (!q) return value;
  const lower = value.toLowerCase();
  const needle = q.toLowerCase();
  const parts: ReactNode[] = [];
  let from = 0;
  let key = 0;
  while (from < value.length) {
    const idx = lower.indexOf(needle, from);
    if (idx < 0) {
      parts.push(value.slice(from));
      break;
    }
    if (idx > from) parts.push(value.slice(from, idx));
    parts.push(
      <mark key={`m-${key++}`} style={{ background: '#ffe58f', color: '#111', padding: 0 }}>
        {value.slice(idx, idx + needle.length)}
      </mark>
    );
    from = idx + needle.length;
  }
  return parts;
}

/** Plain text / TXT preview with line numbers and search marks. */
export function TextCodePreview({ text, searchQuery = '', maxHeight = 420 }: Props) {
  const lines = useMemo(() => text.split(/\r?\n/), [text]);
  const matchCount = useMemo(
    () => countSearchMatches(text, searchQuery),
    [text, searchQuery]
  );

  return (
    <div
      style={{
        display: 'grid',
        gridTemplateColumns: 'auto 1fr',
        maxHeight,
        overflow: 'auto',
        background: 'var(--ant-color-fill-quaternary, #f5f5f5)',
        borderRadius: 6,
        border: '1px solid var(--ant-color-border, #f0f0f0)',
        fontFamily: 'ui-monospace, SFMono-Regular, Menlo, Consolas, monospace',
        fontSize: 12,
        lineHeight: 1.45,
      }}
      data-match-count={matchCount}
    >
      <div
        aria-hidden
        style={{
          padding: '12px 8px 12px 12px',
          textAlign: 'right',
          color: '#8c8c8c',
          userSelect: 'none',
          borderRight: '1px solid var(--ant-color-border, #f0f0f0)',
          whiteSpace: 'pre',
        }}
      >
        {lines.map((_, i) => `${i + 1}\n`).join('')}
      </div>
      <pre style={{ margin: 0, padding: 12, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
        {lines.map((line, idx) => (
          <div key={`l-${idx}`}>
            {highlightSearch(line, searchQuery)}
            {'\n'}
          </div>
        ))}
      </pre>
    </div>
  );
}
