import { describe, expect, it } from 'vitest';

import type { TenantDigitalServiceRow } from '@/features/digital-services/api/tenantDigitalServicesApi';
import {
  canAccessDigitalServices,
  canCreateDigitalApp,
  canCreateDigitalWeb,
  canGenerateDigitalApp,
  canGenerateDigitalWebsite,
  canPreviewDigitalWeb,
  canRequestDigitalApp,
  canRequestDigitalWeb,
  canUseDigitalApp,
  canUseDigitalWeb,
  isAnyDigitalServiceAvailable,
} from '@/features/digital/digitalServicePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

function statusRow(
  website: { isAvailable: boolean; isEnabled?: boolean; isActive?: boolean },
  app: { isAvailable: boolean; isEnabled?: boolean; isActive?: boolean }
): TenantDigitalServiceRow {
  return {
    tenantId: 't1',
    name: 'Cafe',
    slug: 'cafe',
    website: {
      serviceType: 'website',
      isEnabled: website.isEnabled ?? true,
      isActive: website.isActive ?? true,
      isAvailable: website.isAvailable,
      price: 10,
      customPrice: null,
      listPrice: 10,
      currency: 'EUR',
      activatedAt: null,
      deactivatedAt: null,
      deactivationReason: null,
    },
    app: {
      serviceType: 'app',
      isEnabled: app.isEnabled ?? true,
      isActive: app.isActive ?? true,
      isAvailable: app.isAvailable,
      price: 20,
      customPrice: null,
      listPrice: 20,
      currency: 'EUR',
      activatedAt: null,
      deactivatedAt: null,
      deactivationReason: null,
    },
  };
}

describe('digitalServicePermissions', () => {
  it('allows SuperAdmin regardless of claims', () => {
    expect(canAccessDigitalServices({ permissions: [] }, true)).toBe(true);
    expect(canCreateDigitalWeb({ permissions: [] }, true)).toBe(true);
    expect(canCreateDigitalApp({ permissions: [] }, true)).toBe(true);
  });

  it('allows digital.view for portal access', () => {
    expect(canAccessDigitalServices({ permissions: [PERMISSIONS.DIGITAL_VIEW] }, false)).toBe(true);
  });

  it('allows website.manage for portal and request/preview but not create', () => {
    const user = { permissions: [PERMISSIONS.WEBSITE_MANAGE] };
    expect(canAccessDigitalServices(user, false)).toBe(true);
    expect(canPreviewDigitalWeb(user, false)).toBe(true);
    expect(canRequestDigitalWeb(user, false)).toBe(true);
    expect(canRequestDigitalApp(user, false)).toBe(true);
    expect(canCreateDigitalWeb(user, false)).toBe(false);
    expect(canCreateDigitalApp(user, false)).toBe(false);
    expect(canUseDigitalWeb(user, false)).toBe(false);
    expect(canUseDigitalApp(user, false)).toBe(false);
  });

  it('denies users without digital permissions', () => {
    expect(canAccessDigitalServices({ permissions: [PERMISSIONS.SETTINGS_VIEW] }, false)).toBe(
      false
    );
    expect(canCreateDigitalWeb({ permissions: [PERMISSIONS.DIGITAL_VIEW] }, false)).toBe(false);
    expect(canCreateDigitalApp({ permissions: [PERMISSIONS.DIGITAL_VIEW] }, false)).toBe(false);
  });

  it('allows digital.create for generate', () => {
    expect(canCreateDigitalWeb({ permissions: [PERMISSIONS.DIGITAL_CREATE] }, false)).toBe(true);
    expect(canCreateDigitalApp({ permissions: [PERMISSIONS.DIGITAL_CREATE] }, false)).toBe(true);
  });

  it('allows digital.request', () => {
    expect(canRequestDigitalWeb({ permissions: [PERMISSIONS.DIGITAL_REQUEST] }, false)).toBe(true);
    expect(canRequestDigitalApp({ permissions: [PERMISSIONS.DIGITAL_REQUEST] }, false)).toBe(true);
  });

  it('allows digital.preview', () => {
    expect(canPreviewDigitalWeb({ permissions: [PERMISSIONS.DIGITAL_PREVIEW] }, false)).toBe(true);
  });

  it('isAnyDigitalServiceAvailable requires at least one surface', () => {
    expect(isAnyDigitalServiceAvailable(undefined)).toBe(true);
    expect(
      isAnyDigitalServiceAvailable(statusRow({ isAvailable: false }, { isAvailable: false }))
    ).toBe(false);
    expect(
      isAnyDigitalServiceAvailable(statusRow({ isAvailable: true }, { isAvailable: false }))
    ).toBe(true);
  });

  it('canGenerateDigitalWebsite combines create permission and status', () => {
    const user = { permissions: [PERMISSIONS.DIGITAL_CREATE] };
    const webDown = statusRow({ isAvailable: false }, { isAvailable: true });
    const appDown = statusRow({ isAvailable: true }, { isAvailable: false });
    expect(canGenerateDigitalWebsite(user, false, webDown)).toBe(false);
    expect(canGenerateDigitalWebsite(user, false, appDown)).toBe(true);
    expect(canGenerateDigitalApp(user, false, appDown)).toBe(false);
    expect(canGenerateDigitalApp(user, false, webDown)).toBe(true);
  });
});
