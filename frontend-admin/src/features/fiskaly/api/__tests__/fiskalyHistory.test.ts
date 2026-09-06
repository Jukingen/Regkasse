import { describe, expect, it, vi } from 'vitest';

import { fetchAllFiskalyHistory } from '../fiskalyHistory';

vi.mock('@/lib/axios', () => ({
  customInstance: vi.fn(),
}));

import { customInstance } from '@/lib/axios';

describe('fetchAllFiskalyHistory', () => {
  it('pages until totalCount is reached', async () => {
    const mock = vi.mocked(customInstance);
    mock
      .mockResolvedValueOnce({
        items: [{ id: '1' }, { id: '2' }],
        totalCount: 3,
        page: 1,
        pageSize: 2,
        totalPages: 2,
      })
      .mockResolvedValueOnce({
        items: [{ id: '3' }],
        totalCount: 3,
        page: 2,
        pageSize: 2,
        totalPages: 2,
      });

    const rows = await fetchAllFiskalyHistory({ status: 'Failed' }, 2000);

    expect(rows.map((r) => r.id)).toEqual(['1', '2', '3']);
    expect(mock).toHaveBeenCalledTimes(2);
  });
});
