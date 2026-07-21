import { describe, expect, it } from 'vitest';

import { backupRedirectFromLegacySearch } from '@/shared/backupLegacyRedirect';

describe('backupLegacyRedirect', () => {
  it('maps legacy tab query to canonical routes', () => {
    expect(backupRedirectFromLegacySearch('tab=configuration')).toBe('/backup/configuration');
    expect(backupRedirectFromLegacySearch('tab=log')).toBe('/backup/audit');
    expect(backupRedirectFromLegacySearch('tab=monitoring')).toBe('/backup/runs');
    expect(backupRedirectFromLegacySearch('')).toBe('/backup/dashboard');
  });

  it('preserves runId on dashboard redirect', () => {
    expect(backupRedirectFromLegacySearch('tab=operations&runId=abc-123')).toBe(
      '/backup/dashboard?runId=abc-123'
    );
  });
});
