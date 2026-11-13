import { customInstance } from '@/lib/axios';

export type SubscriptionInvoiceDto = {
  id: string;
  tenantId: string;
  tenantName: string;
  tenantSlug: string;
  invoiceNumber: string;
  periodStartUtc: string;
  periodEndUtc: string;
  licenseType: string;
  amountNet: number;
  vatRate: number;
  amountVat: number;
  amountGross: number;
  currency: string;
  status: string;
  issuedAtUtc: string;
  paidAtUtc?: string | null;
  paymentMethod?: string | null;
  paymentReference?: string | null;
  voidReason?: string | null;
  voidedAtUtc?: string | null;
  emailSentAtUtc?: string | null;
};

export type MonthlyInvoiceGenerationResult = {
  created: number;
  skipped: number;
  failed: number;
};

export type MarkPaidRequest = {
  paidAt?: string | null;
  paymentMethod?: string | null;
  reference?: string | null;
};

export type VoidInvoiceRequest = {
  reason?: string | null;
};

export type SubscriptionInvoiceListParams = {
  page?: number;
  pageSize?: number;
  status?: string;
  tenantId?: string;
  fromUtc?: string;
  toUtc?: string;
};

export async function listSubscriptionInvoices(
  params: SubscriptionInvoiceListParams
): Promise<SubscriptionInvoiceDto[]> {
  return customInstance<SubscriptionInvoiceDto[]>({
    url: '/api/admin/invoices',
    method: 'GET',
    params,
  });
}

export async function getSubscriptionInvoice(id: string): Promise<SubscriptionInvoiceDto> {
  return customInstance<SubscriptionInvoiceDto>({
    url: `/api/admin/invoices/${id}`,
    method: 'GET',
  });
}

export async function downloadSubscriptionInvoicePdf(id: string, fileName?: string): Promise<void> {
  const blob = await customInstance<Blob>({
    url: `/api/admin/invoices/${id}/pdf`,
    method: 'GET',
    responseType: 'blob',
  });
  const url = globalThis.URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName ?? `invoice-${id}.pdf`;
  anchor.click();
  globalThis.URL.revokeObjectURL(url);
}

export async function generateMonthlySubscriptionInvoices(): Promise<MonthlyInvoiceGenerationResult> {
  return customInstance<MonthlyInvoiceGenerationResult>({
    url: '/api/admin/invoices/generate-monthly',
    method: 'POST',
  });
}

export async function markSubscriptionInvoicePaid(
  id: string,
  body: MarkPaidRequest
): Promise<SubscriptionInvoiceDto> {
  return customInstance<SubscriptionInvoiceDto>({
    url: `/api/admin/invoices/${id}/mark-paid`,
    method: 'POST',
    data: body,
  });
}

export async function voidSubscriptionInvoice(
  id: string,
  body: VoidInvoiceRequest
): Promise<SubscriptionInvoiceDto> {
  return customInstance<SubscriptionInvoiceDto>({
    url: `/api/admin/invoices/${id}/void`,
    method: 'POST',
    data: body,
  });
}
