import { customInstance } from '@/lib/axios';

function closingPdfFileName(closingId: string, closingType?: string | null): string {
  const kind = closingType?.trim() || 'Daily';
  const prefix =
    kind === 'Monthly' ? 'Monatsabschluss' : kind === 'Yearly' ? 'Jahresabschluss' : 'Tagesabschluss';
  return `${prefix}_${closingId}.pdf`;
}

export async function downloadClosingReportPdf(
  closingId: string,
  options?: { language?: string; closingType?: string | null; fileName?: string },
): Promise<void> {
  const lang = encodeURIComponent((options?.language ?? 'de').split('-')[0] || 'de');
  const blob = await customInstance<Blob>({
    url: `/api/Tagesabschluss/closing/${encodeURIComponent(closingId)}/report.pdf?language=${lang}`,
    method: 'GET',
    responseType: 'blob',
  });
  const url = globalThis.URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download =
    options?.fileName ?? closingPdfFileName(closingId, options?.closingType);
  anchor.click();
  globalThis.URL.revokeObjectURL(url);
}
