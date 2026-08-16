import { describe, expect, it } from 'vitest';

import { MANAGER_ADMIN_PERMISSIONS } from '@/shared/__tests__/fixtures/adminAppPermissionFixtures';
import { ADMIN_SIDEBAR_GROUP_KEYS } from '@/shared/adminSidebarNavigation';
import { SIDEBAR_LAYOUT_ROWS, SIDEBAR_NAV_ITEM_CATALOG } from '@/shared/adminSidebarRegistry';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import { AppPermissions, PERMISSIONS } from '@/shared/auth/permissions';
import { ROUTE_PERMISSIONS } from '@/shared/auth/routePermissions';

describe('sidebarRegistryCatalog', () => {
  it('references only defined catalog ids in layout rows', () => {
    const ids = new Set(Object.keys(SIDEBAR_NAV_ITEM_CATALOG));

    for (const row of SIDEBAR_LAYOUT_ROWS) {
      if (row.kind === 'leaves' || row.kind === 'nested') {
        for (const id of row.catalogIds) {
          expect(ids.has(id), `Unknown catalog id: ${id}`).toBe(true);
        }
        continue;
      }
      if (row.kind !== 'group') continue;
      for (const block of row.blocks) {
        if (block.kind === 'leaves' || block.kind === 'nested') {
          for (const id of block.catalogIds) {
            expect(ids.has(id), `Unknown catalog id: ${id}`).toBe(true);
          }
          if (block.kind === 'nested') {
            for (const child of block.childGroups ?? []) {
              for (const id of child.catalogIds) {
                expect(ids.has(id), `Unknown child catalog id: ${id}`).toBe(true);
              }
            }
          }
        }
      }
    }
  });

  it('uses unique menuKey per catalog item', () => {
    const keys = Object.values(SIDEBAR_NAV_ITEM_CATALOG).map((x) => x.menuKey);
    expect(new Set(keys).size).toBe(keys.length);
  });

  it('aligns catalog permission with ROUTE_PERMISSIONS when declared', () => {
    for (const item of Object.values(SIDEBAR_NAV_ITEM_CATALOG)) {
      if (item.permission === undefined) continue;
      expect(ROUTE_PERMISSIONS[item.menuKey], item.menuKey).toEqual(item.permission);
    }
  });

  it('shows dashboard for any user with permission claims', () => {
    expect(isMenuItemAllowed('/dashboard', ['product.view'])).toBe(true);
    expect(isMenuItemAllowed('/dashboard', [])).toBe(false);
  });

  it('hides Kassenverwaltung without cash_register.manage', () => {
    expect(isMenuItemAllowed('/kassenverwaltung', [AppPermissions.CashRegisterManage])).toBe(true);
    expect(isMenuItemAllowed('/kassenverwaltung', [AppPermissions.CashRegisterView])).toBe(false);
    expect(isMenuItemAllowed('/kassenverwaltung', ['product.view'])).toBe(false);
    expect(isMenuItemAllowed('/kassenverwaltung', [])).toBe(false);
  });

  it('hides Super Admin-only sidebar leaves from Manager permissions', () => {
    const managerPerms = [...MANAGER_ADMIN_PERMISSIONS];
    for (const key of [
      '/admin/tenants',
      '/admin/tenants/create',
      '/admin/billing',
      '/admin/cash-registers',
    ]) {
      expect(isMenuItemAllowed(key, managerPerms), key).toBe(false);
    }
    // license.manage → license.view (implication); platform shell still gates /admin/licenses via role.
    expect(isMenuItemAllowed('/admin/licenses', managerPerms)).toBe(true);
    expect(isMenuItemAllowed('/admin/license-management', managerPerms)).toBe(true);
  });

  it('declares system.critical on Super Admin platform catalog leaves', () => {
    expect(SIDEBAR_NAV_ITEM_CATALOG.superAdminTenants.permission).toBe(PERMISSIONS.SYSTEM_CRITICAL);
    expect(SIDEBAR_NAV_ITEM_CATALOG.superAdminCreateTenant.permission).toBe(
      PERMISSIONS.SYSTEM_CRITICAL
    );
    expect(SIDEBAR_NAV_ITEM_CATALOG.billingOverview.permission).toEqual([
      PERMISSIONS.SYSTEM_CRITICAL,
    ]);
    expect(SIDEBAR_NAV_ITEM_CATALOG.superAdminCashRegisters.permission).toBe(
      PERMISSIONS.SYSTEM_CRITICAL
    );
  });

  it('splits platform admin IA into Verwaltung / Sicherheit / Deployment / Monitoring groups', () => {
    const groupIds = SIDEBAR_LAYOUT_ROWS.filter((r) => r.kind === 'group').map((r) => r.group);
    expect(groupIds).toContain('administration');
    expect(groupIds).toContain('securityTse');
    expect(groupIds).toContain('deploymentSystem');
    expect(groupIds).toContain('monitoringLogs');

    const byGroup = Object.fromEntries(
      SIDEBAR_LAYOUT_ROWS.filter((r) => r.kind === 'group').map((r) => {
        const ids: string[] = [];
        for (const block of r.blocks) {
          if (block.kind === 'leaves' || block.kind === 'nested') {
            ids.push(...block.catalogIds);
          }
        }
        return [r.group, ids];
      })
    );

    expect(byGroup.administration).toEqual(
      expect.arrayContaining(['superAdminTenants', 'superAdminCashRegisters'])
    );
    expect(byGroup.administration).not.toContain('superAdminDataManagement');
    expect(byGroup.administration).not.toContain('adminTseManagement');
    expect(byGroup.settings).toEqual(
      expect.arrayContaining([
        'settingsDataManagement',
        'digitalServicesManage',
        'digitalServiceRequests',
      ])
    );
    expect(byGroup.license).not.toContain('digitalServicesManage');
    expect(byGroup.securityTse).toEqual(
      expect.arrayContaining(['adminTseManagement', 'superAdminApprovals', 'adminTseLogs'])
    );
    expect(byGroup.operations).toEqual(expect.arrayContaining(['onlineOrders']));
    expect(byGroup.deploymentSystem).toEqual(
      expect.arrayContaining([
        'superAdminDeployments',
        'superAdminFeatureFlags',
        'superAdminMaintenance',
      ])
    );
    expect(byGroup.monitoringLogs).toEqual(
      expect.arrayContaining(['adminMonitoring', 'adminRiskDashboard', 'elmahErrors'])
    );
  });

  it('nests TSE leaves under Sicherheit & TSE subgroups', () => {
    const security = SIDEBAR_LAYOUT_ROWS.find(
      (r) => r.kind === 'group' && r.group === 'securityTse'
    );
    expect(security?.kind).toBe('group');
    if (security?.kind !== 'group') return;

    const nested = security.blocks.filter((b) => b.kind === 'nested');
    expect(nested.map((b) => (b.kind === 'nested' ? b.menuKey : ''))).toEqual([
      ADMIN_SIDEBAR_GROUP_KEYS.tseManagement,
      ADMIN_SIDEBAR_GROUP_KEYS.tseOpsFailover,
      ADMIN_SIDEBAR_GROUP_KEYS.tseAnalyticsMonitoring,
      ADMIN_SIDEBAR_GROUP_KEYS.tseReportsFinance,
      ADMIN_SIDEBAR_GROUP_KEYS.tseAdvanced,
      ADMIN_SIDEBAR_GROUP_KEYS.tseDiagnostics,
    ]);
  });

  it('nests RKSV leaves under IA subgroups and Online-Bestellungen under Verkauf', () => {
    const rksv = SIDEBAR_LAYOUT_ROWS.find((r) => r.kind === 'group' && r.group === 'rksv');
    expect(rksv?.kind).toBe('group');
    if (rksv?.kind !== 'group') return;

    const nestedKeys = rksv.blocks
      .filter((b) => b.kind === 'nested')
      .map((b) => (b.kind === 'nested' ? b.menuKey : ''));
    expect(nestedKeys).toEqual([
      ADMIN_SIDEBAR_GROUP_KEYS.rksvCurrentStatus,
      ADMIN_SIDEBAR_GROUP_KEYS.rksvBelegeExport,
      ADMIN_SIDEBAR_GROUP_KEYS.rksvFinanzOnline,
      ADMIN_SIDEBAR_GROUP_KEYS.rksvAuditPruefung,
    ]);

    const belege = rksv.blocks.find(
      (b) => b.kind === 'nested' && b.menuKey === ADMIN_SIDEBAR_GROUP_KEYS.rksvBelegeExport
    );
    expect(belege?.kind).toBe('nested');
    if (belege?.kind !== 'nested') return;
    expect(belege.childGroups?.map((c) => c.menuKey)).toEqual([
      ADMIN_SIDEBAR_GROUP_KEYS.specialReceipts,
    ]);
    expect(belege.catalogIds).toEqual(['rksvTestsDepExport', 'rksvTestsSignatureVerify']);

    const hub = rksv.blocks.find((b) => b.kind === 'rksvHub');
    expect(hub).toMatchObject({
      kind: 'rksvHub',
      menuKey: ADMIN_SIDEBAR_GROUP_KEYS.rksvTools,
      labelKey: 'nav.rksv.werkzeuge',
    });

    const operations = SIDEBAR_LAYOUT_ROWS.find(
      (r) => r.kind === 'group' && r.group === 'operations'
    );
    expect(operations?.kind).toBe('group');
    if (operations?.kind !== 'group') return;
    const sales = operations.blocks.find(
      (b) => b.kind === 'nested' && b.menuKey === ADMIN_SIDEBAR_GROUP_KEYS.salesTransactions
    );
    expect(sales?.kind).toBe('nested');
    if (sales?.kind !== 'nested') return;
    expect(sales.catalogIds).toContain('onlineOrders');
  });

  it('nests Einstellungen leaves under IA subgroups', () => {
    const settings = SIDEBAR_LAYOUT_ROWS.find((r) => r.kind === 'group' && r.group === 'settings');
    expect(settings?.kind).toBe('group');
    if (settings?.kind !== 'group') return;

    const nestedKeys = settings.blocks
      .filter((b) => b.kind === 'nested')
      .map((b) => (b.kind === 'nested' ? b.menuKey : ''));
    expect(nestedKeys).toEqual([
      ADMIN_SIDEBAR_GROUP_KEYS.settingsGeneral,
      ADMIN_SIDEBAR_GROUP_KEYS.settingsFinancesTaxes,
      ADMIN_SIDEBAR_GROUP_KEYS.settingsOperations,
      ADMIN_SIDEBAR_GROUP_KEYS.digitalServices,
      ADMIN_SIDEBAR_GROUP_KEYS.settingsCompliance,
    ]);
  });

  it('hides RKSV test helper from Manager sidebar permissions', () => {
    const managerPerms = [...MANAGER_ADMIN_PERMISSIONS];
    expect(isMenuItemAllowed('/rksv/sb/test-helper', managerPerms)).toBe(false);
    expect(SIDEBAR_NAV_ITEM_CATALOG.specialReceiptTestHelper.permission).toBe(
      PERMISSIONS.SYSTEM_CRITICAL
    );
  });
});
