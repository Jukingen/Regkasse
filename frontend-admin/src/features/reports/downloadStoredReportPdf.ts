import { triggerReportPdfBlobDownload } from '@/features/reports/api/reportPdfApi';
import { customInstance } from '@/lib/axios';

export async function downloadStoredClosingReportPdf(
  closingId: string,
  options?: { language?: string; signal?: AbortSignal }
): Promise<Blob> {
  const params = options?.language ? `?language=${encodeURIComponent(options.language)}` : '';
  return customInstance<Blob>({
    url: `/api/admin/report-pdfs/closing/${closingId}${params}`,
    method: 'GET',
    responseType: 'blob',
    signal: options?.signal,
  });
}

export async function downloadStoredReceiptReportPdf(
  paymentId: string,
  options?: { signal?: AbortSignal }
): Promise<Blob> {
  return customInstance<Blob>({
    url: `/api/admin/report-pdfs/receipt/${paymentId}`,
    method: 'GET',
    responseType: 'blob',
    signal: options?.signal,
  });
}

export function triggerPdfBlobDownload(blob: Blob, fileName: string): void {
  triggerReportPdfBlobDownload(blob, fileName);
}
