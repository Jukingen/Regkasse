import { describe, expect, it } from 'vitest';

import {
  isInitialQueryLoading,
  isQuietQueryRefetch,
  queryTableLoading,
} from '@/lib/query/queryUiState';

function q(
  partial: Partial<{
    isLoading: boolean;
    isFetching: boolean;
    isError: boolean;
    isPending: boolean;
    data: unknown;
    isPlaceholderData: boolean;
  }>
) {
  return {
    isLoading: false,
    isFetching: false,
    isError: false,
    isPending: false,
    data: undefined,
    isPlaceholderData: false,
    ...partial,
  };
}

describe('queryUiState', () => {
  it('detects initial loading', () => {
    expect(isInitialQueryLoading(q({ isLoading: true }))).toBe(true);
    expect(isInitialQueryLoading(q({ isLoading: true, data: [] }))).toBe(false);
    expect(isInitialQueryLoading(q({ isLoading: true, isPlaceholderData: true, data: [] }))).toBe(
      false
    );
  });

  it('treats poll refetch with data as quiet', () => {
    expect(isQuietQueryRefetch(q({ isFetching: true, data: [{ id: 1 }] }))).toBe(true);
    expect(isQuietQueryRefetch(q({ isFetching: true, isLoading: true }))).toBe(false);
    expect(isQuietQueryRefetch(q({ isFetching: true, isPlaceholderData: true, data: [] }))).toBe(
      true
    );
  });

  it('table loading defaults to initial only', () => {
    expect(queryTableLoading(q({ isLoading: true }))).toBe(true);
    expect(queryTableLoading(q({ isFetching: true, data: [] }))).toBe(false);
    expect(queryTableLoading(q({ isFetching: true, data: [] }), { showQuietRefetch: true })).toBe(
      true
    );
  });
});
