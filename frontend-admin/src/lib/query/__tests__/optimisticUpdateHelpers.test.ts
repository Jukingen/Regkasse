import { QueryClient } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';

import {
  beginOptimisticQueryUpdate,
  mapOptimisticList,
  mapOptimisticPagedItems,
  rollbackOptimisticQueryUpdate,
  settleOptimisticQueryUpdate,
} from '@/lib/query/optimisticUpdateHelpers';

describe('optimisticUpdateHelpers', () => {
  const listKey = ['admin', 'demo-list'] as const;

  it('snapshots, updates, rolls back, and invalidates', async () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    queryClient.setQueryData(listKey, [
      { id: 'a', name: 'Alpha' },
      { id: 'b', name: 'Beta' },
    ]);

    const ctx = await beginOptimisticQueryUpdate(
      queryClient,
      listKey,
      (old: Array<{ id: string; name: string }> | undefined) =>
        mapOptimisticList(old, (items) => items.filter((row) => row.id !== 'a'))
    );

    expect(queryClient.getQueryData(listKey)).toEqual([{ id: 'b', name: 'Beta' }]);

    rollbackOptimisticQueryUpdate(queryClient, ctx);
    expect(queryClient.getQueryData(listKey)).toEqual([
      { id: 'a', name: 'Alpha' },
      { id: 'b', name: 'Beta' },
    ]);

    settleOptimisticQueryUpdate(queryClient, listKey);
    await queryClient.cancelQueries({ queryKey: listKey });
  });

  it('mapOptimisticPagedItems patches items only', () => {
    const page = {
      items: [{ id: '1', name: 'x' }],
      pagination: { pageNumber: 1, pageSize: 10, totalCount: 1, totalPages: 1 },
    };
    const next = mapOptimisticPagedItems(page, (items) =>
      items.map((row) => (row.id === '1' ? { ...row, name: 'y' } : row))
    );
    expect(next?.items[0]?.name).toBe('y');
    expect(next?.pagination.totalCount).toBe(1);
  });
});
