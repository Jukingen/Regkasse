import { describe, expect, it } from 'vitest';

import { buildBackupTriggerRequestBody } from '@/features/backup/api/backupHooks';

describe('buildBackupTriggerRequestBody', () => {
  const tenantId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';

  it('encodes ambient tenant when params omit tenantId', () => {
    const body = buildBackupTriggerRequestBody({}, tenantId);
    expect(body.idempotencyKey).toMatch(new RegExp(`^manual-tenant-${tenantId}-\\d+$`));
  });

  it('prefers explicit tenantId over ambient', () => {
    const explicit = '11111111-2222-3333-4444-555555555555';
    const body = buildBackupTriggerRequestBody({ tenantId: explicit }, tenantId);
    expect(body.idempotencyKey).toMatch(new RegExp(`^manual-tenant-${explicit}-\\d+$`));
  });

  it('uses all-tenants prefix when requested', () => {
    const body = buildBackupTriggerRequestBody({ allTenants: true }, tenantId);
    expect(body.idempotencyKey).toMatch(/^manual-all-tenants-\d+$/);
  });

  it('falls back to manual stamp without tenant context', () => {
    const body = buildBackupTriggerRequestBody({});
    expect(body.idempotencyKey).toMatch(/^manual-\d+$/);
  });
});
