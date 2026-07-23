/**
 * Lightweight file-preview helpers (no syntax-highlighter package).
 */

export type PreviewableFileKind = 'json' | 'csv' | 'txt' | 'pdf' | 'unsupported';

export const FILE_PREVIEW_MAX_CHARS = 400_000;
export const FILE_PREVIEW_MAX_LINES = 5_000;
export const CSV_PREVIEW_MAX_ROWS = 200;

export function canPreviewFile(
  fileName: string,
  fileType?: string | null,
  mimeType?: string | null
): boolean {
  return detectPreviewKind(fileName, fileType, mimeType) !== 'unsupported';
}

export function detectPreviewKind(
  fileName: string,
  fileType?: string | null,
  mimeType?: string | null
): PreviewableFileKind {
  const ext = fileName.includes('.')
    ? fileName.slice(fileName.lastIndexOf('.') + 1).toLowerCase()
    : '';
  const type = (fileType ?? '').trim().toLowerCase().replace(/^\./, '');
  const mime = (mimeType ?? '').trim().toLowerCase();

  if (ext === 'json' || type === 'json' || mime.includes('json')) return 'json';
  if (ext === 'csv' || type === 'csv' || mime.includes('csv')) return 'csv';
  if (ext === 'pdf' || type === 'pdf' || mime.includes('pdf')) return 'pdf';
  if (
    ext === 'txt' ||
    ext === 'log' ||
    ext === 'md' ||
    type === 'txt' ||
    type === 'text' ||
    mime.startsWith('text/')
  ) {
    return 'txt';
  }
  return 'unsupported';
}

export function prettyPrintJson(raw: string): { text: string; truncated: boolean; lineCount: number } {
  let text = raw;
  let truncated = false;
  try {
    text = JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    // keep raw text
  }
  const lines = text.split(/\r?\n/);
  if (lines.length > FILE_PREVIEW_MAX_LINES) {
    text = lines.slice(0, FILE_PREVIEW_MAX_LINES).join('\n') + '\n…';
    truncated = true;
  } else if (text.length > FILE_PREVIEW_MAX_CHARS) {
    text = text.slice(0, FILE_PREVIEW_MAX_CHARS) + '\n…';
    truncated = true;
  }
  return { text, truncated, lineCount: text.split(/\r?\n/).length };
}

export function preparePlainText(raw: string): { text: string; truncated: boolean; lineCount: number } {
  let text = raw;
  let truncated = false;
  const lines = text.split(/\r?\n/);
  if (lines.length > FILE_PREVIEW_MAX_LINES) {
    text = lines.slice(0, FILE_PREVIEW_MAX_LINES).join('\n') + '\n…';
    truncated = true;
  } else if (text.length > FILE_PREVIEW_MAX_CHARS) {
    text = text.slice(0, FILE_PREVIEW_MAX_CHARS) + '\n…';
    truncated = true;
  }
  return { text, truncated, lineCount: text.split(/\r?\n/).length };
}

export type CsvPreviewTable = {
  headers: string[];
  rows: string[][];
  truncated: boolean;
  totalRowCount: number;
};

/** Minimal RFC4180-ish CSV parser for preview (handles quotes and commas). */
export function parseCsvPreview(raw: string): CsvPreviewTable {
  const rows: string[][] = [];
  let cell = '';
  let row: string[] = [];
  let inQuotes = false;

  for (let i = 0; i < raw.length; i++) {
    const ch = raw[i]!;
    const next = raw[i + 1];
    if (inQuotes) {
      if (ch === '"' && next === '"') {
        cell += '"';
        i += 1;
      } else if (ch === '"') {
        inQuotes = false;
      } else {
        cell += ch;
      }
      continue;
    }
    if (ch === '"') {
      inQuotes = true;
      continue;
    }
    if (ch === ',') {
      row.push(cell);
      cell = '';
      continue;
    }
    if (ch === '\n' || (ch === '\r' && next === '\n')) {
      row.push(cell);
      cell = '';
      if (row.some((c) => c.length > 0) || row.length > 1) rows.push(row);
      row = [];
      if (ch === '\r') i += 1;
      continue;
    }
    if (ch === '\r') {
      row.push(cell);
      cell = '';
      if (row.some((c) => c.length > 0) || row.length > 1) rows.push(row);
      row = [];
      continue;
    }
    cell += ch;
  }
  if (cell.length > 0 || row.length > 0) {
    row.push(cell);
    rows.push(row);
  }

  if (rows.length === 0) {
    return { headers: [], rows: [], truncated: false, totalRowCount: 0 };
  }

  const headers = rows[0]!.map((h, idx) => (h.trim() ? h : `Col ${idx + 1}`));
  const dataRows = rows.slice(1);
  const truncated = dataRows.length > CSV_PREVIEW_MAX_ROWS;
  return {
    headers,
    rows: dataRows.slice(0, CSV_PREVIEW_MAX_ROWS),
    truncated,
    totalRowCount: dataRows.length,
  };
}

export type JsonHighlightToken =
  | { type: 'key' | 'string' | 'number' | 'boolean' | 'null' | 'punct' | 'plain'; value: string };

/** Tokenize a single pretty-printed JSON line for lightweight highlighting. */
export function tokenizeJsonLine(line: string): JsonHighlightToken[] {
  const tokens: JsonHighlightToken[] = [];
  let i = 0;
  while (i < line.length) {
    const ch = line[i]!;
    if (/\s/.test(ch)) {
      let j = i + 1;
      while (j < line.length && /\s/.test(line[j]!)) j += 1;
      tokens.push({ type: 'plain', value: line.slice(i, j) });
      i = j;
      continue;
    }
    if (ch === '"' ) {
      let j = i + 1;
      let escaped = false;
      while (j < line.length) {
        const c = line[j]!;
        if (escaped) {
          escaped = false;
          j += 1;
          continue;
        }
        if (c === '\\') {
          escaped = true;
          j += 1;
          continue;
        }
        if (c === '"') {
          j += 1;
          break;
        }
        j += 1;
      }
      const str = line.slice(i, j);
      let k = j;
      while (k < line.length && /\s/.test(line[k]!)) k += 1;
      const isKey = line[k] === ':';
      tokens.push({ type: isKey ? 'key' : 'string', value: str });
      i = j;
      continue;
    }
    if (ch === '{' || ch === '}' || ch === '[' || ch === ']' || ch === ',' || ch === ':') {
      tokens.push({ type: 'punct', value: ch });
      i += 1;
      continue;
    }
    if (/[-\d]/.test(ch)) {
      let j = i + 1;
      while (j < line.length && /[\d.eE+-]/.test(line[j]!)) j += 1;
      tokens.push({ type: 'number', value: line.slice(i, j) });
      i = j;
      continue;
    }
    if (line.slice(i, i + 4) === 'true') {
      tokens.push({ type: 'boolean', value: 'true' });
      i += 4;
      continue;
    }
    if (line.slice(i, i + 5) === 'false') {
      tokens.push({ type: 'boolean', value: 'false' });
      i += 5;
      continue;
    }
    if (line.slice(i, i + 4) === 'null') {
      tokens.push({ type: 'null', value: 'null' });
      i += 4;
      continue;
    }
    tokens.push({ type: 'plain', value: ch });
    i += 1;
  }
  return tokens;
}

export function countSearchMatches(text: string, query: string): number {
  const q = query.trim();
  if (!q) return 0;
  const lower = text.toLowerCase();
  const needle = q.toLowerCase();
  let count = 0;
  let from = 0;
  while (from < lower.length) {
    const idx = lower.indexOf(needle, from);
    if (idx < 0) break;
    count += 1;
    from = idx + needle.length;
  }
  return count;
}
