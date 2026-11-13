import { describe, expect, it } from 'vitest';

import {
  canPreviewFile,
  countSearchMatches,
  detectPreviewKind,
  parseCsvPreview,
  preparePlainText,
  prettyPrintJson,
  tokenizeJsonLine,
} from '@/lib/preview/filePreview';

describe('detectPreviewKind / canPreviewFile', () => {
  it('detects by extension and type', () => {
    expect(detectPreviewKind('a.json')).toBe('json');
    expect(detectPreviewKind('a.CSV')).toBe('csv');
    expect(detectPreviewKind('notes.txt')).toBe('txt');
    expect(detectPreviewKind('doc.pdf')).toBe('pdf');
    expect(detectPreviewKind('x.zip')).toBe('unsupported');
    expect(detectPreviewKind('blob', 'JSON')).toBe('json');
    expect(canPreviewFile('x.zip')).toBe(false);
    expect(canPreviewFile('x.json')).toBe(true);
  });
});

describe('prettyPrintJson', () => {
  it('pretty-prints valid JSON', () => {
    const { text, truncated, lineCount } = prettyPrintJson('{"a":1}');
    expect(text).toContain('\n');
    expect(text).toContain('"a"');
    expect(truncated).toBe(false);
    expect(lineCount).toBeGreaterThan(1);
  });

  it('keeps invalid JSON as raw text', () => {
    const { text } = prettyPrintJson('not-json');
    expect(text).toBe('not-json');
  });
});

describe('parseCsvPreview', () => {
  it('parses headers and rows with quotes', () => {
    const csv = 'name,city\n"Cafe, Muster",Wien\nBar,Graz\n';
    const table = parseCsvPreview(csv);
    expect(table.headers).toEqual(['name', 'city']);
    expect(table.rows).toHaveLength(2);
    expect(table.rows[0]).toEqual(['Cafe, Muster', 'Wien']);
    expect(table.truncated).toBe(false);
  });
});

describe('tokenizeJsonLine', () => {
  it('marks keys and strings', () => {
    const tokens = tokenizeJsonLine('  "exportType": "DEP-Export",');
    const types = tokens.map((t) => t.type);
    expect(types).toContain('key');
    expect(types).toContain('string');
    expect(types).toContain('punct');
  });

  it('marks numbers and booleans', () => {
    const tokens = tokenizeJsonLine('  "ok": true, "n": 12.5');
    expect(tokens.some((t) => t.type === 'boolean' && t.value === 'true')).toBe(true);
    expect(tokens.some((t) => t.type === 'number' && t.value === '12.5')).toBe(true);
  });
});

describe('countSearchMatches / preparePlainText', () => {
  it('counts case-insensitive matches', () => {
    expect(countSearchMatches('Hello hello HELLO', 'hello')).toBe(3);
    expect(countSearchMatches('abc', '')).toBe(0);
  });

  it('prepares plain text line counts', () => {
    const { lineCount } = preparePlainText('a\nb\nc');
    expect(lineCount).toBe(3);
  });
});
