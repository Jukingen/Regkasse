import { describe, expect, it } from 'vitest';

import { permissionImplied } from '../permissionImplication';

describe('permissionImplied', () => {
  it('grants granular permission when parent composite is present', () => {
    expect(permissionImplied('user.create', ['user.manage'])).toBe(true);
  });

  it('grants user.view when user.manage is present', () => {
    expect(permissionImplied('user.view', ['user.manage'])).toBe(true);
  });

  it('grants digital.view via website.manage', () => {
    expect(permissionImplied('digital.view', ['website.manage'])).toBe(true);
    expect(permissionImplied('digital.create', ['website.manage'])).toBe(false);
  });

  it('grants digital.orders.view via digital.orders.manage', () => {
    expect(permissionImplied('digital.orders.view', ['digital.orders.manage'])).toBe(true);
  });

  it('system.critical satisfies any permission', () => {
    expect(permissionImplied('report.export', ['system.critical'])).toBe(true);
  });

  it('denies unrelated permission', () => {
    expect(permissionImplied('system.critical', ['user.view'])).toBe(false);
  });
});
