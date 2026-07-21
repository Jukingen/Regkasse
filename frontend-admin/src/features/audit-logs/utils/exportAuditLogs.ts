import dayjs from 'dayjs';

import { AXIOS_INSTANCE } from '@/lib/axios';

export type AuditLogExportFormat = 'json' | 'csv';

export type AuditLogExportQueryParams = Record<string, string>;

function exportFilename(format: AuditLogExportFormat): string {
  return `audit_logs_${dayjs().format('YYYYMMDD_HHmmss')}.${format}`;
}

function triggerBlobDownload(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

async function readBlobPreview(blob: Blob): Promise<{ contentType: string; text: string }> {
  const contentType = String(
    blob.type || (blob as Blob & { _contentType?: string }).type || ''
  ).toLowerCase();
  const text = await blob.text();
  return { contentType, text };
}

function parseErrorMessageFromBody(text: string, fallback: string): string {
  try {
    const parsed = JSON.parse(text) as { message?: string };
    return typeof parsed?.message === 'string' ? parsed.message : fallback;
  } catch {
    return fallback;
  }
}

/** Download audit log export via GET /api/AuditLog/export (same filters as list). */
export async function downloadAuditLogExport(
  format: AuditLogExportFormat,
  query: AuditLogExportQueryParams,
  options: { exportFailedMessage: string }
): Promise<void> {
  const headers = format === 'csv' ? { Accept: 'text/csv' } : { Accept: 'application/json' };

  const res = await AXIOS_INSTANCE.get<Blob>('/api/AuditLog/export', {
    params: { format, ...query },
    responseType: 'blob',
    headers,
  });

  const blob = res.data;
  const contentTypeHeader = String(res.headers?.['content-type'] ?? '').toLowerCase();
  const { contentType: blobType, text } = await readBlobPreview(blob);
  const contentType = contentTypeHeader || blobType;

  if (format === 'json') {
    if (contentType.includes('application/json')) {
      try {
        const parsed = JSON.parse(text);
        if (Array.isArray(parsed)) {
          triggerBlobDownload(
            new Blob([text], { type: 'application/json' }),
            exportFilename('json')
          );
          return;
        }
        throw new Error(parseErrorMessageFromBody(text, options.exportFailedMessage));
      } catch (e) {
        if (e instanceof Error && e.message !== options.exportFailedMessage) throw e;
        throw new Error(parseErrorMessageFromBody(text, options.exportFailedMessage));
      }
    }
  }

  if (format === 'csv') {
    if (!contentType.includes('text/csv')) {
      throw new Error(parseErrorMessageFromBody(text, options.exportFailedMessage));
    }
    triggerBlobDownload(new Blob([text], { type: 'text/csv' }), exportFilename('csv'));
    return;
  }

  triggerBlobDownload(blob, exportFilename(format));
}
