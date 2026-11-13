/**
 * Shared helpers for manual admin API clients (non-Orval).
 * Keeps SecondParameter / unwrapData free of TypeScript `any`.
 */

/** Second argument of a two-arg function (e.g. `customInstance` options). */
export type SecondParameter<T extends (...args: never[]) => unknown> = Parameters<T>[1];

/**
 * Many admin endpoints return either `T` or `{ data: T }`.
 * Runtime behaviour matches the previous `res?.data !== undefined` unwrap.
 */
export function unwrapData<T>(res: unknown): T {
  if (typeof res === 'object' && res !== null && 'data' in res) {
    const data = (res as { data: unknown }).data;
    if (data !== undefined) {
      return data as T;
    }
  }
  return res as T;
}
