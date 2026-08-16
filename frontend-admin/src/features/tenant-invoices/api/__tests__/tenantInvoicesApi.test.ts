import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  downloadTenantInvoice,
  fetchTenantInvoices,
  getTenantInvoices,
} from '@/features/tenant-invoices/api/tenantInvoices';

const mockCustomInstance = vi.fn();

vi.mock('@/lib/axios', () => ({
  customInstance: (config: unknown) => mockCustomInstance(config),
}));

describe('tenantInvoicesApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('getTenantInvoices sends pagination, status, and date filters', async () => {
    mockCustomInstance.mockResolvedValue({ items: [], totalCount: 0 });
    await getTenantInvoices({
      page: 2,
      pageSize: 10,
      status: 'paid',
      fromDate: '2026-01-01T00:00:00.000Z',
      toDate: '2026-01-31T23:59:59.000Z',
    });

    expect(mockCustomInstance).toHaveBeenCalledWith(
      expect.objectContaining({
        url: '/api/admin/billing/tenant-invoices',
        method: 'GET',
        params: {
          page: 2,
          pageSize: 10,
          status: 'paid',
          fromDate: '2026-01-01T00:00:00.000Z',
          toDate: '2026-01-31T23:59:59.000Z',
        },
      })
    );
  });

  it('fetchTenantInvoices maps fromUtc/toUtc aliases onto fromDate/toDate', async () => {
    mockCustomInstance.mockResolvedValue({ items: [], totalCount: 0 });
    await fetchTenantInvoices({
      fromUtc: '2026-02-01T00:00:00.000Z',
      toUtc: '2026-02-28T23:59:59.000Z',
    });

    expect(mockCustomInstance).toHaveBeenCalledWith(
      expect.objectContaining({
        params: expect.objectContaining({
          fromDate: '2026-02-01T00:00:00.000Z',
          toDate: '2026-02-28T23:59:59.000Z',
        }),
      })
    );
  });

  it('downloadTenantInvoice requests the PDF as a blob', async () => {
    const blob = new Blob(['pdf']);
    mockCustomInstance.mockResolvedValue(blob);

    await expect(downloadTenantInvoice('inv-1')).resolves.toBe(blob);
    expect(mockCustomInstance).toHaveBeenCalledWith({
      url: '/api/admin/billing/tenant-invoices/inv-1/pdf',
      method: 'GET',
      responseType: 'blob',
    });
  });
});
