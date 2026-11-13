import dayjs from 'dayjs';
import { describe, expect, it } from 'vitest';

import { buildActivityLogQueryParams, type ActivityLogFilters } from '../useActivityLog';

function filters(search: string): ActivityLogFilters {
  return {
    dateRange: [dayjs('2026-11-01'), dayjs('2026-11-13')],
    userId: undefined,
    actionType: '',
    search,
    page: 1,
    pageSize: 20,
  };
}

describe('buildActivityLogQueryParams', () => {
  it('sends a trimmed search term to GET /api/AuditLog', () => {
    const params = buildActivityLogQueryParams(filters('  kasse@shop.at  '));
    expect(params.search).toBe('kasse@shop.at');
  });

  it('omits search when the box is empty', () => {
    const params = buildActivityLogQueryParams(filters('   '));
    expect(params.search).toBeUndefined();
  });
});
