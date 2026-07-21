/**
 * Optimistic list-mutation helpers (TanStack Query).
 *
 * ## When to use
 * Prefer this for **list-mutating** CRUD where the UI can patch cache safely:
 * update/delete/status toggles on known rows (products, categories, tenants,
 * online-order status, digital-service rows, activity/alert read state, roles).
 *
 * ## When NOT to use
 * - **Create** without a client-stable id / full row shape → invalidate only
 * - **Fiscal / payment / TSE / RKSV** mutations → wait for server truth
 * - **Password reset / auth / impersonation** → side effects outside list cache
 * - Bulk ops that rewrite most of the list unpredictably (optional: invalidate only)
 *
 * ## Standard lifecycle
 * 1. `onMutate` → {@link beginOptimisticQueryUpdate}: cancel in-flight fetches,
 *    snapshot previous entries, apply updater
 * 2. `onError` → {@link rollbackOptimisticQueryUpdate}: restore snapshots
 * 3. `onSettled` → {@link settleOptimisticQueryUpdate}: invalidate so server
 *    state converges (success and error)
 *
 * Example:
 *
 * ```ts
 * return useMutation({
 *   mutationFn: (id) => deleteItem(id),
 *   onMutate: async (id) =>
 *     beginOptimisticQueryUpdate(queryClient, LIST_KEY, (old: Item[] | undefined) =>
 *       old?.filter((row) => row.id !== id),
 *     ),
 *   onError: (_err, _vars, ctx) => rollbackOptimisticQueryUpdate(queryClient, ctx),
 *   onSettled: () => settleOptimisticQueryUpdate(queryClient, LIST_KEY),
 * });
 * ```
 */
import type { QueryClient, QueryKey } from '@tanstack/react-query';

/** Snapshot of query entries touched during an optimistic update. */
export type OptimisticQuerySnapshot<TData = unknown> = {
  previous: Array<[QueryKey, TData | undefined]>;
};

/**
 * Cancel matching queries, snapshot cache, apply updater to all matching entries.
 * Return value is the `onMutate` context for rollback.
 */
export async function beginOptimisticQueryUpdate<TData>(
  queryClient: QueryClient,
  queryKey: QueryKey,
  updater: (old: TData | undefined) => TData | undefined
): Promise<OptimisticQuerySnapshot<TData>> {
  await queryClient.cancelQueries({ queryKey });
  const previous = queryClient.getQueriesData<TData>({ queryKey });
  queryClient.setQueriesData<TData>({ queryKey }, updater);
  return { previous };
}

/** Restore cache entries from an {@link beginOptimisticQueryUpdate} snapshot. */
export function rollbackOptimisticQueryUpdate<TData>(
  queryClient: QueryClient,
  context: OptimisticQuerySnapshot<TData> | undefined
): void {
  if (!context?.previous.length) return;
  for (const [key, data] of context.previous) {
    queryClient.setQueryData(key, data);
  }
}

/** Invalidate so the list reconverges with the server (call from `onSettled`). */
export function settleOptimisticQueryUpdate(queryClient: QueryClient, queryKey: QueryKey): void {
  void queryClient.invalidateQueries({ queryKey });
}

/**
 * Invalidate several prefixes after an optimistic mutation settles.
 * Useful when one mutation touches list + detail + related hubs.
 */
export function settleOptimisticQueryUpdates(
  queryClient: QueryClient,
  queryKeys: readonly QueryKey[]
): void {
  for (const queryKey of queryKeys) {
    settleOptimisticQueryUpdate(queryClient, queryKey);
  }
}

/** Map over array-shaped list caches; leave non-arrays unchanged. */
export function mapOptimisticList<TItem>(
  old: TItem[] | undefined,
  mapper: (items: TItem[]) => TItem[]
): TItem[] | undefined {
  if (!old) return old;
  return mapper(old);
}

/** Patch one row in an `{ items: T[] }` list response (e.g. products). */
export function mapOptimisticPagedItems<TItem, TPage extends { items: TItem[] }>(
  old: TPage | undefined,
  mapper: (items: TItem[]) => TItem[]
): TPage | undefined {
  if (!old?.items) return old;
  return { ...old, items: mapper(old.items) };
}
