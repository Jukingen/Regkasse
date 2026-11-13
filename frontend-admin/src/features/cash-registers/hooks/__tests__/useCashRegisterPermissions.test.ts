import { describe, expect, it } from 'vitest';

import { resolveCashRegisterPermissions } from '@/features/cash-registers/hooks/useCashRegisterPermissions';

const ownTenant = 'tenant-a';
const otherTenant = 'tenant-b';

describe('resolveCashRegisterPermissions', () => {
  it('grants SuperAdmin full access across tenants', () => {
    const flags = resolveCashRegisterPermissions({
      isSuperAdmin: true,
      userTenantId: ownTenant,
      registerTenantId: otherTenant,
      hasCashRegisterView: false,
      hasCashRegisterManage: false,
      hasReportView: false,
      hasReportExport: false,
      isDecommissioned: true,
      registerLoaded: true,
    });

    expect(flags).toMatchObject({
      canView: true,
      canEdit: true,
      canAssignUser: true,
      canOpen: true,
      canClose: true,
      canManageShifts: true,
      canViewReports: true,
      canExport: true,
      isDecommissioned: true,
    });
  });

  it('denies other-tenant registers for non-SuperAdmin', () => {
    const flags = resolveCashRegisterPermissions({
      isSuperAdmin: false,
      userTenantId: ownTenant,
      registerTenantId: otherTenant,
      hasCashRegisterView: true,
      hasCashRegisterManage: true,
      hasReportView: true,
      hasReportExport: true,
      isDecommissioned: false,
      registerLoaded: true,
    });

    expect(flags.canView).toBe(false);
    expect(flags.canAssignUser).toBe(false);
    expect(flags.canManageShifts).toBe(false);
    expect(flags.canViewReports).toBe(false);
    expect(flags.canExport).toBe(false);
  });

  it('gives Mandanten-Admin manage + reports on own tenant', () => {
    const flags = resolveCashRegisterPermissions({
      isSuperAdmin: false,
      userTenantId: ownTenant,
      registerTenantId: ownTenant,
      hasCashRegisterView: true,
      hasCashRegisterManage: true,
      hasReportView: true,
      hasReportExport: true,
      isDecommissioned: false,
      registerLoaded: true,
    });

    expect(flags.canView).toBe(true);
    expect(flags.canEdit).toBe(true);
    expect(flags.canAssignUser).toBe(true);
    expect(flags.canOpen).toBe(true);
    expect(flags.canManageShifts).toBe(true);
    expect(flags.canViewReports).toBe(true);
    expect(flags.canExport).toBe(true);
  });

  it('gives ReportViewer view/reports/export without operational actions', () => {
    const flags = resolveCashRegisterPermissions({
      isSuperAdmin: false,
      userTenantId: ownTenant,
      registerTenantId: ownTenant,
      hasCashRegisterView: true,
      hasCashRegisterManage: false,
      hasReportView: true,
      hasReportExport: true,
      isDecommissioned: false,
      registerLoaded: true,
    });

    expect(flags.canView).toBe(true);
    expect(flags.canEdit).toBe(false);
    expect(flags.canAssignUser).toBe(false);
    expect(flags.canOpen).toBe(false);
    expect(flags.canManageShifts).toBe(false);
    expect(flags.canViewReports).toBe(true);
    expect(flags.canExport).toBe(true);
  });

  it('denies assignment to a view-only cashier', () => {
    const flags = resolveCashRegisterPermissions({
      isSuperAdmin: false,
      userTenantId: ownTenant,
      registerTenantId: ownTenant,
      hasCashRegisterView: true,
      hasCashRegisterManage: false,
      hasReportView: false,
      hasReportExport: false,
      isDecommissioned: false,
      registerLoaded: true,
    });

    expect(flags.canView).toBe(true);
    expect(flags.canAssignUser).toBe(false);
  });
});
