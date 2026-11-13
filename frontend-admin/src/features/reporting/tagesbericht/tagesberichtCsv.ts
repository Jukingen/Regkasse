import type { TagesberichtListRow } from './tagesberichtStatus';

function csvCell(value: string | number): string {
  const text = String(value ?? '');
  if (/[",\n\r]/.test(text)) {
    return `"${text.replace(/"/g, '""')}"`;
  }
  return text;
}

export function buildTagesberichtCsv(
  rows: TagesberichtListRow[],
  headers: { date: string; register: string; status: string; gross: string; submission: string }
): string {
  const lines = [
    [headers.date, headers.register, headers.status, headers.gross, headers.submission]
      .map(csvCell)
      .join(','),
    ...rows.map((row) =>
      [
        (row.viennaBusinessDate ?? '').slice(0, 10),
        row.registerNumber ?? row.cashRegisterId,
        row.reportStatus,
        row.grossSalesAmount,
        row.submission?.lifecycle ?? '',
      ]
        .map(csvCell)
        .join(',')
    ),
  ];
  return `${lines.join('\n')}\n`;
}

export function downloadTextFile(content: string, fileName: string, mime = 'text/csv;charset=utf-8'): void {
  const blob = new Blob([content], { type: mime });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}
