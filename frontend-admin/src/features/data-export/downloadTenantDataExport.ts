import { AXIOS_INSTANCE } from '@/lib/axios';

export type TenantExportType =
  | 'products'
  | 'customers'
  | 'receipts'
  | 'payments'
  | 'users'
  | 'orders'
  | 'audit_logs'
  | 'inventory'
  | 'tse_signatures';
export type TenantExportFormat = 'csv' | 'xlsx';

function fileNameFromDisposition(header: string | undefined, fallback: string): string {
  if (!header) return fallback;
  const star = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(header);
  if (star?.[1]) return decodeURIComponent(star[1].replace(/"/g, '').trim());
  const plain = /filename="?([^";]+)"?/i.exec(header);
  return plain?.[1] ?? fallback;
}

/** Download GET /api/admin/export/{type}?format=csv|xlsx and save the streamed file. */
export async function downloadTenantDataExport(
  type: TenantExportType,
  format: TenantExportFormat
): Promise<void> {
  const res = await AXIOS_INSTANCE.get<Blob>(`/api/admin/export/${type}`, {
    params: { format },
    responseType: 'blob',
  });
  const disposition = res.headers['content-disposition'] as string | undefined;
  const fileName = fileNameFromDisposition(disposition, `${type}.${format}`);
  const url = URL.createObjectURL(res.data);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}
