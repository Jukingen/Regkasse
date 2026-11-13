/**
 * Customer export download — GET /api/Customer/export
 */
import { AXIOS_INSTANCE } from '@/lib/axios';
import { getEffectiveTenantSlug } from '@/features/auth/services/devTenant';
import { buildCustomerExportFileName } from '@/features/customers/utils/customerExportFileName';

export type CustomerExportFormat = 'csv' | 'json';

/** Download customer export; prefers Content-Disposition filename from the API. */
export async function downloadCustomerExport(
  format: CustomerExportFormat,
  options?: { isActive?: boolean }
): Promise<void> {
  const res = await AXIOS_INSTANCE.get<Blob>('/api/Customer/export', {
    params: { format, isActive: options?.isActive },
    responseType: 'blob',
  });
  const disposition = res.headers['content-disposition'] as string | undefined;
  const match = disposition?.match(/filename="?([^";]+)"?/i);
  const fileName =
    match?.[1] ?? buildCustomerExportFileName(getEffectiveTenantSlug(), format);
  const url = URL.createObjectURL(res.data);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}
