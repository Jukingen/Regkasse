import type { TenantInvoiceStatus } from '@/features/tenant-invoices/api/tenantInvoices';
import type { AdminTranslationKey } from '@/i18n/translationKey';

export function getTenantInvoiceStatusColor(status: TenantInvoiceStatus): string {
  switch (normalizeTenantInvoiceStatus(status)) {
    case 'paid':
      return 'green';
    case 'unpaid':
      return 'orange';
    case 'overdue':
      return 'red';
    case 'cancelled':
      return 'orange';
    case 'refunded':
      return 'red';
    default:
      return 'default';
  }
}

export function getTenantInvoiceStatusLabelKey(status: TenantInvoiceStatus): AdminTranslationKey {
  switch (normalizeTenantInvoiceStatus(status)) {
    case 'unpaid':
      return 'tenantPortal.invoices.statuses.unpaid';
    case 'overdue':
      return 'tenantPortal.invoices.statuses.overdue';
    case 'cancelled':
      return 'tenantPortal.invoices.statuses.cancelled';
    case 'refunded':
      return 'tenantPortal.invoices.statuses.refunded';
    default:
      return 'tenantPortal.invoices.statuses.paid';
  }
}

export function getTenantInvoiceFileName(invoiceNumber: string): string {
  const trimmed = invoiceNumber.trim();
  return trimmed ? `RE-${trimmed}.pdf` : 'invoice.pdf';
}

export function normalizeTenantInvoiceStatus(status: TenantInvoiceStatus): string {
  const key = status.trim().toLowerCase();
  if (key === 'active') return 'paid';
  return key;
}
