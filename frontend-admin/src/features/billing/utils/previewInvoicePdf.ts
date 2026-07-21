import type { LicenseSalePreviewRequest } from '@/api/generated/model';
import { customInstance } from '@/lib/axios';

export async function fetchLicenseSalePreviewPdf(
  request: LicenseSalePreviewRequest
): Promise<Blob> {
  return customInstance<Blob>({
    url: '/api/admin/billing/license-sales/preview-pdf',
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    data: request,
    responseType: 'blob',
  });
}
