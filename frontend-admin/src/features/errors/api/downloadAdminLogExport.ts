/**
 * Elmah / error log export — GET /api/admin/errors/export
 */
import { AXIOS_INSTANCE } from '@/lib/axios';
import { getEffectiveTenantSlug } from '@/features/auth/services/devTenant';
import { buildLogExportFileName } from '@/features/errors/utils/logExportFileName';
import { triggerBrowserDownload } from '@/lib/zip/packFilesIntoZipBlob';

export type LogExportFormat = 'txt' | 'csv' | 'json';

/** Download error log export; prefers Content-Disposition filename from the API. */
export async function downloadAdminLogExport(format: LogExportFormat = 'txt'): Promise<void> {
  const res = await AXIOS_INSTANCE.get<Blob>('/api/admin/errors/export', {
    params: { format },
    responseType: 'blob',
  });
  const disposition = res.headers['content-disposition'] as string | undefined;
  const match = disposition?.match(/filename\*?=(?:UTF-8'')?["']?([^"';]+)/i);
  const fileName =
    (match?.[1] ? decodeURIComponent(match[1].trim()) : null) ??
    buildLogExportFileName(getEffectiveTenantSlug(), format);
  triggerBrowserDownload(res.data, fileName);
}
