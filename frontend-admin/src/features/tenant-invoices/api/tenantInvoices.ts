import { customInstance } from '@/lib/axios';

export type TenantInvoiceStatus = 'paid' | 'unpaid' | 'overdue' | 'cancelled' | 'refunded' | string;

export type TenantInvoiceDto = {
  id: string;
  invoiceNumber: string;
  issuedAt: string;
  invoiceDateUtc: string;
  amountNet: number;
  vatAmount: number;
  amountGross: number;
  currency: string;
  status: TenantInvoiceStatus;
  licenseKey?: string | null;
  licensePlan?: string | null;
  downloadUrl: string;
  pdfUrl: string;
};

export type TenantInvoiceListResponse = {
  items: TenantInvoiceDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  activeCount: number;
  cancelledCount: number;
};

export type TenantInvoiceListParams = {
  page?: number;
  pageSize?: number;
  status?: string;
  fromDate?: string;
  toDate?: string;
  fromUtc?: string;
  toUtc?: string;
};

export const tenantInvoiceQueryKeys = {
  all: ['admin', 'billing', 'tenant-invoices'] as const,
  list: (params: TenantInvoiceListParams) =>
    [...tenantInvoiceQueryKeys.all, 'list', params] as const,
};

export async function fetchTenantInvoices(
  params: TenantInvoiceListParams = {},
  signal?: AbortSignal
): Promise<TenantInvoiceListResponse> {
  return customInstance<TenantInvoiceListResponse>({
    url: '/api/admin/billing/tenant-invoices',
    method: 'GET',
    params: {
      page: params.page,
      pageSize: params.pageSize,
      status: params.status,
      fromDate: params.fromDate ?? params.fromUtc,
      toDate: params.toDate ?? params.toUtc,
    },
    signal,
  });
}

export async function downloadTenantInvoicePdf(invoiceId: string): Promise<Blob> {
  return customInstance<Blob>({
    url: `/api/admin/billing/tenant-invoices/${invoiceId}/pdf`,
    method: 'GET',
    responseType: 'blob',
  });
}

/** @deprecated Use fetchTenantInvoices */
export const getTenantInvoices = fetchTenantInvoices;

/** @deprecated Use downloadTenantInvoicePdf */
export const downloadTenantInvoice = downloadTenantInvoicePdf;
