import { describe, expect, it } from 'vitest';

import { permissionImplied } from '../permissionImplication';

describe('permissionImplied', () => {
  it('grants granular permission when parent composite is present', () => {
    expect(permissionImplied('user.create', ['user.manage'])).toBe(true);
  });

  it('denies unrelated permission', () => {
    expect(permissionImplied('system.critical', ['user.view'])).toBe(false);
  });
});
