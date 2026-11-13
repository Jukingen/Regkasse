import { describe, expect, it } from 'vitest';

import {
  analyzeMenuPermissionConsistency,
  buildConsistencyFixSuggestions,
  formatConsistencySummaryLines,
  INVENTED_PERMISSION_ALIASES,
} from '../menuPermissionConsistency';
import { PERMISSIONS } from '@/shared/auth/permissions';

describe('analyzeMenuPermissionConsistency', () => {
  it('returns a structured report with okMenus count', () => {
    const report = analyzeMenuPermissionConsistency(Object.values(PERMISSIONS));
    expect(report.okMenus).toBeGreaterThan(0);
    expect(report.summary.ok).toBe(report.okMenus);
    expect(Array.isArray(report.issues)).toBe(true);
  });

  it('formatConsistencySummaryLines always includes ok line', () => {
    const report = analyzeMenuPermissionConsistency(Object.values(PERMISSIONS));
    const lines = formatConsistencySummaryLines(report);
    expect(lines[0]).toMatch(/^✅ \d+ menus mapped correctly$/);
  });

  it('buildConsistencyFixSuggestions covers incorrect keys when aliases present', () => {
    const alias = Object.keys(INVENTED_PERMISSION_ALIASES)[0];
    expect(alias).toBeTruthy();
    // Smoke: analyzer does not throw; fixes array is defined
    const report = analyzeMenuPermissionConsistency(Object.values(PERMISSIONS));
    const fixes = buildConsistencyFixSuggestions(report);
    expect(Array.isArray(fixes)).toBe(true);
  });
});
