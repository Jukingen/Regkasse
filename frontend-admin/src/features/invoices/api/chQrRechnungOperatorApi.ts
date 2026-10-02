import { customInstance } from '@/lib/axios';

export type ChQrBankUploadConfirmation = {
  uploadedBy: string;
  uploadedAtUtc: string;
  bankReference: string;
};

export function downloadChQrInvoicePdf(tenantId: string, invoiceId: string): Promise<Blob> {
  return customInstance<Blob>({
    url: `/api/admin/tenants/${tenantId}/ch-qr-invoices/${invoiceId}/pdf`,
    method: 'GET',
    responseType: 'blob',
  });
}

export function confirmChQrBankUpload(
  tenantId: string,
  invoiceId: string,
  body: ChQrBankUploadConfirmation
): Promise<{ recorded?: boolean }> {
  return customInstance<{ recorded?: boolean }>({
    url: `/api/admin/tenants/${tenantId}/ch-qr-invoices/${invoiceId}/upload-confirmation`,
    method: 'POST',
    data: body,
  });
}
