'use client';

import { useMemo, type ReactNode } from 'react';

import {
  type JsonHighlightToken,
  countSearchMatches,
  tokenizeJsonLine,
} from '@/lib/preview/filePreview';

const TOKEN_COLORS: Record<JsonHighlightToken['type'], string> = {
  key: '#9cdcfe',
  string: '#ce9178',
  number: '#b5cea8',
  boolean: '#569cd6',
  null: '#569cd6',
  punct: '#d4d4d4',
  plain: '#d4d4d4',
};

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
      <mark
        key={`m-${key++}`}
        style={{ background: '#e6c207', color: '#111', padding: 0 }}
      >
        {value.slice(idx, idx + needle.length)}
      </mark>
    );
    from = idx + needle.length;
  }
  return parts;
}

/**
 * Pretty JSON with line numbers + light syntax colors + optional search marks.
 */
export function JsonCodePreview({ text, searchQuery = '', maxHeight = 420 }: Props) {
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
        background: '#1e1e1e',
        borderRadius: 6,
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
          color: '#858585',
          userSelect: 'none',
          borderRight: '1px solid #333',
          whiteSpace: 'pre',
        }}
      >
        {lines.map((_, i) => `${i + 1}\n`).join('')}
      </div>
      <pre style={{ margin: 0, padding: 12, color: '#d4d4d4', whiteSpace: 'pre' }}>
        {lines.map((line, lineIdx) => {
          const tokens = tokenizeJsonLine(line);
          return (
            <div key={`l-${lineIdx}`}>
              {tokens.map((tok, tokIdx) => (
                <span key={`t-${lineIdx}-${tokIdx}`} style={{ color: TOKEN_COLORS[tok.type] }}>
                  {highlightSearch(tok.value, searchQuery)}
                </span>
              ))}
              {'\n'}
            </div>
          );
        })}
      </pre>
    </div>
  );
}
