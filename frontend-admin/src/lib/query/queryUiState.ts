/**
 * TanStack Query v5 UI helpers — avoid loading "flash" on background refetch.
 *
 * - Initial load: show skeleton / full spinner (`isLoading` + no data / not placeholder)
 * - Refetch with existing data: keep previous rows; optional quiet table spinner (`isQuietRefetch`)
 * - Pagination: pair with `placeholderData: keepPreviousData` (v5 identity helper)
 */
import type { UseQueryResult } from '@tanstack/react-query';

type QueryLike = Pick<
  UseQueryResult<unknown, unknown>,
  'isLoading' | 'isFetching' | 'isError' | 'isPending' | 'data' | 'isPlaceholderData'
>;

/** True while the first successful result has not arrived yet. */
export function isInitialQueryLoading(query: QueryLike): boolean {
  return query.isLoading && query.data === undefined && !query.isPlaceholderData;
}

/**
 * True when a fetch is in flight but prior/placeholder data can stay visible
 * (pagination, filter toggle with keepPreviousData, or background poll).
 */
export function isQuietQueryRefetch(query: QueryLike): boolean {
  if (!query.isFetching) return false;
  if (query.isPlaceholderData) return true;
  return query.data !== undefined && !query.isLoading;
}

/**
 * Ant Design Table `loading` prop: spinner only on initial load or placeholder transition,
 * not on silent background polls.
 */
export function queryTableLoading(
  query: QueryLike,
  options?: { showQuietRefetch?: boolean }
): boolean {
  if (isInitialQueryLoading(query)) return true;
  if (options?.showQuietRefetch && isQuietQueryRefetch(query)) return true;
  return false;
}
