import { describe, expect, it } from 'vitest';

import {
  AUDIT_LOG_LIST_DEFAULTS,
  toAuditLogStatusUrlParam,
} from '@/features/audit-logs/constants/auditLogFilters';

/** Mirrors URL serialization in useAuditLogSearchParams.setParams for unit tests. */
function buildAuditLogQueryString(merged: {
  page: number;
  pageSize: number;
  startDate?: string;
  endDate?: string;
  action?: string;
  userId?: string;
  entityType?: string;
  status?: string;
}): string {
  const qp = new URLSearchParams();
  if (merged.page !== AUDIT_LOG_LIST_DEFAULTS.page) qp.set('page', String(merged.page));
  if (merged.pageSize !== AUDIT_LOG_LIST_DEFAULTS.pageSize)
    qp.set('pageSize', String(merged.pageSize));
  if (merged.startDate) qp.set('startDate', merged.startDate);
  if (merged.endDate) qp.set('endDate', merged.endDate);
  if (merged.action) qp.set('action', merged.action);
  if (merged.userId) qp.set('userId', merged.userId);
  if (merged.entityType) qp.set('entityType', merged.entityType);
  if (merged.status) qp.set('status', toAuditLogStatusUrlParam(merged.status));
  return qp.toString();
}

describe('audit log URL query params', () => {
  it('omits default page and pageSize', () => {
    expect(
      buildAuditLogQueryString({
        page: 1,
        pageSize: 10,
        userId: 'u-1',
        entityType: 'Payment',
        status: 'Failed',
      })
    ).toBe('userId=u-1&entityType=Payment&status=Failure');
  });

  it('includes non-default pagination', () => {
    expect(
      buildAuditLogQueryString({
        page: 2,
        pageSize: 25,
        startDate: '2026-05-01',
        endDate: '2026-05-18',
      })
    ).toBe('page=2&pageSize=25&startDate=2026-05-01&endDate=2026-05-18');
  });
});
