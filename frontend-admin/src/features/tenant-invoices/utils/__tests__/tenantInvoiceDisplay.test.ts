import { describe, expect, it } from 'vitest';

import {
  getTenantInvoiceFileName,
  getTenantInvoiceStatusColor,
  getTenantInvoiceStatusLabelKey,
  normalizeTenantInvoiceStatus,
} from '@/features/tenant-invoices/utils/tenantInvoiceDisplay';

describe('tenantInvoiceDisplay', () => {
  it('maps invoice statuses to tag colors', () => {
    expect(getTenantInvoiceStatusColor('paid')).toBe('green');
    expect(getTenantInvoiceStatusColor('active')).toBe('green');
    expect(getTenantInvoiceStatusColor('unpaid')).toBe('orange');
    expect(getTenantInvoiceStatusColor('overdue')).toBe('red');
    expect(getTenantInvoiceStatusColor('cancelled')).toBe('orange');
    expect(getTenantInvoiceStatusColor('refunded')).toBe('red');
    expect(getTenantInvoiceStatusColor('unknown')).toBe('default');
  });

  it('maps invoice statuses to i18n keys', () => {
    expect(getTenantInvoiceStatusLabelKey('paid')).toBe('tenantPortal.invoices.statuses.paid');
    expect(getTenantInvoiceStatusLabelKey('active')).toBe('tenantPortal.invoices.statuses.paid');
    expect(getTenantInvoiceStatusLabelKey('unpaid')).toBe('tenantPortal.invoices.statuses.unpaid');
    expect(getTenantInvoiceStatusLabelKey('overdue')).toBe(
      'tenantPortal.invoices.statuses.overdue'
    );
    expect(getTenantInvoiceStatusLabelKey('cancelled')).toBe(
      'tenantPortal.invoices.statuses.cancelled'
    );
    expect(getTenantInvoiceStatusLabelKey('refunded')).toBe(
      'tenantPortal.invoices.statuses.refunded'
    );
  });

  it('normalizes legacy active status to paid', () => {
    expect(normalizeTenantInvoiceStatus('active')).toBe('paid');
    expect(normalizeTenantInvoiceStatus('Paid')).toBe('paid');
  });

  it('builds a download file name from the invoice number', () => {
    expect(getTenantInvoiceFileName('2026-001')).toBe('RE-2026-001.pdf');
    expect(getTenantInvoiceFileName('  ')).toBe('invoice.pdf');
  });
});
