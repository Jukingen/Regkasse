import { describe, expect, it } from 'vitest';

import { buildRoleMenuPreview, summarizeMenuPreview } from '../roleMenuPreview';
import { PERMISSIONS } from '@/shared/auth/permissions';

const t = (key: string) => key;

describe('buildRoleMenuPreview', () => {
  it('marks most leaves hidden for empty permissions (non-superadmin)', () => {
    const tree = buildRoleMenuPreview('Cashier', [], t);
    const stats = summarizeMenuPreview(tree);
    expect(stats.hidden + stats.visible + stats.partial).toBeGreaterThan(0);
    expect(stats.hidden).toBeGreaterThan(0);
  });

  it('shows more leaves for Manager with broad permissions', () => {
    const empty = summarizeMenuPreview(buildRoleMenuPreview('Cashier', [], t));
    const full = summarizeMenuPreview(
      buildRoleMenuPreview('Manager', Object.values(PERMISSIONS), t)
    );
    expect(full.visible).toBeGreaterThan(empty.visible);
  });
});
