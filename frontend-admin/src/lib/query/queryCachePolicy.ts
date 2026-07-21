/**
 * Shared TanStack Query cache timings for frontend-admin.
 *
 * Categories (see also `createAppQueryClient` defaults):
 * - Static / lookup — roles, permissions, tax rates, payment-method definitions
 * - Dynamic / user — users, tenants, products, payments, catalogs that change moderately
 * - Volatile / real-time — backup status, online orders, TSE health, live ops panels
 *
 * Prefer importing these constants over magic numbers. Mutation success paths must still
 * `invalidateQueries` — longer staleTime must not block post-mutation freshness.
 */

/** 15 minutes — rarely changing lookup catalogs. */
export const QUERY_STALE_STATIC_MS = 15 * 60 * 1000;

/** Keep static lookups in memory longer after unmount (navigating between admin screens). */
export const QUERY_GC_STATIC_MS = 30 * 60 * 1000;

/** 90 seconds — default for lists/detail that change on admin actions. */
export const QUERY_STALE_DYNAMIC_MS = 90 * 1000;

/** Default garbage collection for dynamic queries (aligned with QueryClient). */
export const QUERY_GC_DYNAMIC_MS = 5 * 60 * 1000;

/** 10 seconds — frequently changing ops data that still benefits from brief dedupe. */
export const QUERY_STALE_VOLATILE_MS = 10 * 1000;

/** Short retention for volatile screens (back-navigation within a few seconds). */
export const QUERY_GC_VOLATILE_MS = 30 * 1000;

/**
 * Never treat as fresh; drop from cache as soon as unused.
 * Use for live status endpoints where stale snapshots are misleading.
 */
export const QUERY_STALE_REALTIME_MS = 0;
export const QUERY_GC_REALTIME_MS = 0;

/** Spread into `useQuery({ ... })` for static/lookup data. */
export const queryCacheStatic = {
  staleTime: QUERY_STALE_STATIC_MS,
  gcTime: QUERY_GC_STATIC_MS,
} as const;

/** Spread into `useQuery({ ... })` for dynamic entity lists/details. */
export const queryCacheDynamic = {
  staleTime: QUERY_STALE_DYNAMIC_MS,
  gcTime: QUERY_GC_DYNAMIC_MS,
} as const;

/** Spread into `useQuery({ ... })` for volatile ops data (short freshness window). */
export const queryCacheVolatile = {
  staleTime: QUERY_STALE_VOLATILE_MS,
  gcTime: QUERY_GC_VOLATILE_MS,
} as const;

/** Spread into `useQuery({ ... })` when the UI must always refetch and never keep idle cache. */
export const queryCacheRealtime = {
  staleTime: QUERY_STALE_REALTIME_MS,
  gcTime: QUERY_GC_REALTIME_MS,
} as const;
