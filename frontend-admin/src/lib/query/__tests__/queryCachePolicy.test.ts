import { describe, expect, it } from 'vitest';

import {
  QUERY_GC_DYNAMIC_MS,
  QUERY_GC_REALTIME_MS,
  QUERY_GC_STATIC_MS,
  QUERY_GC_VOLATILE_MS,
  QUERY_STALE_DYNAMIC_MS,
  QUERY_STALE_REALTIME_MS,
  QUERY_STALE_STATIC_MS,
  QUERY_STALE_VOLATILE_MS,
  queryCacheDynamic,
  queryCacheRealtime,
  queryCacheStatic,
  queryCacheVolatile,
} from '@/lib/query/queryCachePolicy';

describe('queryCachePolicy', () => {
  it('exposes static/lookup timings in the 10–15 minute band', () => {
    expect(QUERY_STALE_STATIC_MS).toBe(15 * 60 * 1000);
    expect(QUERY_GC_STATIC_MS).toBeGreaterThan(QUERY_STALE_STATIC_MS);
    expect(queryCacheStatic).toEqual({
      staleTime: QUERY_STALE_STATIC_MS,
      gcTime: QUERY_GC_STATIC_MS,
    });
  });

  it('exposes dynamic timings in the 1–2 minute band', () => {
    expect(QUERY_STALE_DYNAMIC_MS).toBe(90 * 1000);
    expect(QUERY_GC_DYNAMIC_MS).toBe(5 * 60 * 1000);
    expect(queryCacheDynamic).toEqual({
      staleTime: QUERY_STALE_DYNAMIC_MS,
      gcTime: QUERY_GC_DYNAMIC_MS,
    });
  });

  it('exposes volatile timings near 10 seconds', () => {
    expect(QUERY_STALE_VOLATILE_MS).toBe(10_000);
    expect(QUERY_GC_VOLATILE_MS).toBe(30_000);
    expect(queryCacheVolatile).toEqual({
      staleTime: QUERY_STALE_VOLATILE_MS,
      gcTime: QUERY_GC_VOLATILE_MS,
    });
  });

  it('exposes realtime as never-cached', () => {
    expect(QUERY_STALE_REALTIME_MS).toBe(0);
    expect(QUERY_GC_REALTIME_MS).toBe(0);
    expect(queryCacheRealtime).toEqual({
      staleTime: 0,
      gcTime: 0,
    });
  });
});
