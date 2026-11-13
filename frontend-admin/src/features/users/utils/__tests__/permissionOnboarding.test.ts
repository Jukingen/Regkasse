import { describe, expect, it } from 'vitest';

import { summarizePermissionChanges } from '../permissionChangesSummary';
import { analyzePermissionHealth } from '../permissionHealthCheck';
import { PERMISSIONS } from '@/shared/auth/permissions';

describe('analyzePermissionHealth', () => {
  it('warns when payment rights lack TSE sign', () => {
    const report = analyzePermissionHealth({
      granted: [PERMISSIONS.PAYMENT_TAKE, PERMISSIONS.SALE_CREATE],
      catalogSize: 100,
    });
    expect(report.issues.some((i) => i.id === 'pos-fiscal')).toBe(true);
  });

  it('warns when too many permissions are granted', () => {
    const granted = Array.from({ length: 95 }, (_, i) => `perm.${i}`);
    const report = analyzePermissionHealth({
      granted,
      catalogSize: 100,
    });
    expect(report.issues.some((i) => i.id === 'too-many')).toBe(true);
  });

  it('flags empty grants', () => {
    const report = analyzePermissionHealth({ granted: [], catalogSize: 50 });
    expect(report.issues.some((i) => i.id === 'empty')).toBe(true);
  });
});

describe('summarizePermissionChanges', () => {
  it('reports added and removed keys', () => {
    const summary = summarizePermissionChanges(['a', 'b'], ['b', 'c']);
    expect(summary.added).toEqual(['c']);
    expect(summary.removed).toEqual(['a']);
    expect(summary.unchangedCount).toBe(1);
  });
});
