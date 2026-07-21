import { customInstance } from '@/lib/axios';

export type ReportPdfType =
  | 'tagesabschluss'
  | 'monatsbeleg'
  | 'jahresbeleg'
  | 'startbeleg'
  | 'nullbeleg'
  | 'schlussbeleg'
  | 'receipt';

export type DownloadReportPdfOptions = {
  language?: string;
  signal?: AbortSignal;
};

export function reportPdfTypeFromClosingType(closingType?: string | null): ReportPdfType {
  switch (closingType?.trim()) {
    case 'Monthly':
      return 'monatsbeleg';
    case 'Yearly':
      return 'jahresbeleg';
    default:
      return 'tagesabschluss';
  }
}

export function reportPdfTypeFromSpecialReceiptKind(kind?: string | null): ReportPdfType {
  switch (kind?.trim()) {
    case 'Startbeleg':
      return 'startbeleg';
    case 'Nullbeleg':
      return 'nullbeleg';
    case 'Schlussbeleg':
      return 'schlussbeleg';
    case 'Monatsbeleg':
      return 'monatsbeleg';
    case 'Jahresbeleg':
      return 'jahresbeleg';
    default:
      return 'receipt';
  }
}

export const downloadReportPdf = async (
  reportType: string,
  reportId: string,
  options?: DownloadReportPdfOptions
): Promise<Blob> => {
  const trimmedType = reportType.trim();
  const trimmedId = reportId.trim();
  if (!trimmedType || !trimmedId) {
    throw new Error('reportType and reportId are required');
  }

  return customInstance<Blob>({
    url: `/api/admin/reports/pdf/${encodeURIComponent(trimmedType)}/${trimmedId}`,
    method: 'GET',
    params: options?.language ? { language: options.language } : undefined,
    responseType: 'blob',
    signal: options?.signal,
  });
};

export function triggerReportPdfBlobDownload(blob: Blob, fileName: string): void {
  const pdfBlob =
    blob instanceof Blob ? blob : new Blob([blob as BlobPart], { type: 'application/pdf' });
  const url = window.URL.createObjectURL(pdfBlob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName.endsWith('.pdf') ? fileName : `${fileName}.pdf`;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  window.URL.revokeObjectURL(url);
}
