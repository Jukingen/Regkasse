import { describe, expect, it } from 'vitest';

import { AuditLogStatus } from '@/api/generated/model/auditLogStatus';
import {
  parseAuditLogStatusFromUrl,
  toAuditLogStatusApiParam,
  toAuditLogStatusUrlParam,
} from '@/features/audit-logs/constants/auditLogFilters';

describe('audit log status URL/API mapping', () => {
  it('parses Failure URL alias to Failed', () => {
    expect(parseAuditLogStatusFromUrl('Failure')).toBe('Failed');
  });

  it('serializes Failed as Failure in shareable URLs', () => {
    expect(toAuditLogStatusUrlParam('Failed')).toBe('Failure');
    expect(toAuditLogStatusUrlParam('Success')).toBe('Success');
  });

  it('maps filter values to generated AuditLogStatus ordinals', () => {
    expect(toAuditLogStatusApiParam('Failed')).toBe(AuditLogStatus.Failed);
    expect(toAuditLogStatusApiParam('Success')).toBe(AuditLogStatus.Success);
    expect(toAuditLogStatusApiParam('Warning')).toBe(AuditLogStatus.Warning);
    expect(toAuditLogStatusApiParam(undefined)).toBeUndefined();
  });
});
