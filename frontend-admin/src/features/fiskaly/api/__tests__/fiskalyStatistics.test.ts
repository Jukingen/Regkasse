import { describe, expect, it, vi } from 'vitest';

vi.mock('@/lib/axios', () => ({
  customInstance: vi.fn(),
  AXIOS_INSTANCE: { get: vi.fn() },
}));

import { AXIOS_INSTANCE, customInstance } from '@/lib/axios';

import { exportFiskalyStatistics, getFiskalyStatistics } from '../fiskalyStatistics';

describe('fiskalyStatistics api', () => {
  it('loads statistics with filters', async () => {
    const mock = vi.mocked(customInstance);
    mock.mockResolvedValueOnce({
      fromUtc: '2026-08-01T00:00:00Z',
      toUtc: '2026-08-28T00:00:00Z',
      kpis: { totalOperations: 3, successRatePercent: 66.67, totalErrors: 1, successCount: 2, failedCount: 1, inFlightCount: 0 },
      daily: [],
      byOperationType: [],
      byStatus: [],
      monthly: [],
    });

    const result = await getFiskalyStatistics({
      operationType: 'cancel',
      tenantId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    });

    expect(result.kpis.totalOperations).toBe(3);
    expect(mock).toHaveBeenCalledWith(
      expect.objectContaining({
        url: '/api/admin/fiskaly/statistics',
        method: 'GET',
        params: expect.objectContaining({ operationType: 'cancel' }),
      })
    );
  });

  it('exports a blob with filename from content-disposition', async () => {
    vi.mocked(AXIOS_INSTANCE.get).mockResolvedValueOnce({
      data: new Blob(['csv']),
      headers: { 'content-disposition': 'attachment; filename="fiskaly-statistics.csv"' },
    });

    const result = await exportFiskalyStatistics({ format: 'csv' });
    expect(result.fileName).toBe('fiskaly-statistics.csv');
    expect(AXIOS_INSTANCE.get).toHaveBeenCalledWith(
      '/api/admin/fiskaly/statistics/export',
      expect.objectContaining({
        params: { format: 'csv' },
        responseType: 'blob',
      })
    );
  });
});
