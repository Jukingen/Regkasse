/** Shared CSV helpers for admin exports (RFC-style escaping). */

export function escapeCsvCell(value: unknown): string {
  const s = value == null ? '' : String(value);
  if (/[",\n\r]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

export function csvRow(cells: unknown[]): string {
  return cells.map(escapeCsvCell).join(',');
}

export function rowsToCsv(rows: unknown[][]): string {
  return rows.map((row) => csvRow(row)).join('\n');
}

export function downloadCsvText(content: string, fileName: string): void {
  const blob = new Blob([content], { type: 'text/csv;charset=utf-8' });
  const url = globalThis.URL.createObjectURL(blob);
  const a = globalThis.document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  globalThis.URL.revokeObjectURL(url);
}
