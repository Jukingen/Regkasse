import { describe, expect, it, vi } from 'vitest';

vi.mock('@/lib/axios', () => ({
  customInstance: vi.fn(),
  AXIOS_INSTANCE: { get: vi.fn() },
}));

import { AXIOS_INSTANCE, customInstance } from '@/lib/axios';

import {
  exportFiskalyErrors,
  getFiskalyErrorById,
  getFiskalyErrorStats,
  getFiskalyErrors,
  setFiskalyErrorReview,
} from '../fiskalyErrors';

describe('fiskalyErrors api', () => {
  it('loads the error list with filters', async () => {
    const mock = vi.mocked(customInstance);
    mock.mockResolvedValueOnce({
      items: [],
      totalCount: 4,
      page: 1,
      pageSize: 25,
      totalPages: 1,
    });

    const result = await getFiskalyErrors({
      operationType: 'cancel',
      reviewStatus: 'open',
      search: 'R-1',
    });

    expect(result.totalCount).toBe(4);
    expect(mock).toHaveBeenCalledWith(
      expect.objectContaining({
        url: '/api/admin/fiskaly/errors',
        method: 'GET',
        params: expect.objectContaining({ reviewStatus: 'open', search: 'R-1' }),
      })
    );
  });

  it('loads error stats', async () => {
    const mock = vi.mocked(customInstance);
    mock.mockResolvedValueOnce({
      fromUtc: '2026-08-01T00:00:00Z',
      toUtc: '2026-08-28T00:00:00Z',
      kpis: {
        totalErrors: 4,
        errorRatePercent: 20,
        totalOperations: 20,
        mostCommonErrorCount: 3,
        tenantWithMostErrorsCount: 4,
        openCount: 2,
        resolvedCount: 1,
        knownIssueCount: 1,
      },
      daily: [],
      weekly: [],
      topErrors: [],
      byOperationType: [],
      byTenant: [],
    });

    const result = await getFiskalyErrorStats({ operationType: 'cancel' });
    expect(result.kpis.totalErrors).toBe(4);
    expect(mock).toHaveBeenCalledWith(
      expect.objectContaining({
        url: '/api/admin/fiskaly/errors/stats',
        method: 'GET',
      })
    );
  });

  it('loads error detail', async () => {
    const mock = vi.mocked(customInstance);
    mock.mockResolvedValueOnce({ id: '1', errorCode: 'E_CLIENT_ERROR' });
    const result = await getFiskalyErrorById('1');
    expect(result.errorCode).toBe('E_CLIENT_ERROR');
    expect(mock).toHaveBeenCalledWith(
      expect.objectContaining({
        url: '/api/admin/fiskaly/errors/1',
        method: 'GET',
      })
    );
  });

  it('puts review status', async () => {
    const mock = vi.mocked(customInstance);
    mock.mockResolvedValueOnce({ id: '1', reviewStatus: 'resolved' });

    const result = await setFiskalyErrorReview('1', 'resolved');
    expect(result.reviewStatus).toBe('resolved');
    expect(mock).toHaveBeenCalledWith(
      expect.objectContaining({
        url: '/api/admin/fiskaly/errors/1/resolve',
        method: 'PUT',
        data: { reviewStatus: 'resolved' },
      })
    );
  });

  it('exports a csv blob', async () => {
    vi.mocked(AXIOS_INSTANCE.get).mockResolvedValueOnce({
      data: new Blob(['csv']),
      headers: { 'content-disposition': 'attachment; filename="fiskaly-errors.csv"' },
    });

    const result = await exportFiskalyErrors({ reviewStatus: 'open', format: 'csv' });
    expect(result.fileName).toBe('fiskaly-errors.csv');
    expect(AXIOS_INSTANCE.get).toHaveBeenCalledWith(
      '/api/admin/fiskaly/errors/export',
      expect.objectContaining({
        params: { reviewStatus: 'open', format: 'csv' },
        responseType: 'blob',
      })
    );
  });
});
