import { describe, expect, it } from 'vitest';

import {
  PLATFORM_ADMIN_GROUP_HUB_HREF,
  buildPlatformAdminBreadcrumbs,
  resolvePlatformAdminBreadcrumbGroup,
} from '@/shared/adminPlatformBreadcrumbs';
import { ADMIN_OVERVIEW_HREF } from '@/shared/adminShellLabels';
import { buildPathBreadcrumbs } from '@/shared/buildPathBreadcrumbs';

describe('adminPlatformBreadcrumbs', () => {
  const t = (key: string) => {
    const map: Record<string, string> = {
      'common.breadcrumb.overview': 'Overview',
      'nav.administration': 'Verwaltung',
      'nav.securityTse': 'Sicherheit & TSE',
      'nav.deploymentSystem': 'Deployment & System',
      'nav.monitoringLogs': 'Monitoring & Logs',
      'nav.adminTseManagement': 'TSE-Verwaltung',
      'nav.adminTseFailover': 'TSE-Failover',
      'nav.deployments': 'Deployments',
      'nav.deploymentTenants': 'Mandanten-Deployments',
      'nav.accessRoles': 'Zugriff & Rollen',
      'nav.adminMonitoring': 'Monitoring',
      'nav.errorLogs': 'Error Logs (Elmah)',
      'nav.adminRiskDashboard': 'Risiko-Dashboard',
      'nav.tenants': 'Mandanten',
      'access.hub.pageTitle': 'Zugriff & Rollen',
      'access.roles.pageTitle': 'Rollen & Berechtigungen',
    };
    return map[key] ?? key;
  };

  it('resolves platform IA groups from pathname', () => {
    expect(resolvePlatformAdminBreadcrumbGroup('/admin/access')).toBe('administration');
    expect(resolvePlatformAdminBreadcrumbGroup('/admin/tenants')).toBe('administration');
    expect(resolvePlatformAdminBreadcrumbGroup('/admin/tse-management')).toBe('securityTse');
    expect(resolvePlatformAdminBreadcrumbGroup('/admin/tse/failover')).toBe('securityTse');
    expect(resolvePlatformAdminBreadcrumbGroup('/admin/approvals')).toBe('securityTse');
    expect(resolvePlatformAdminBreadcrumbGroup('/admin/deployments')).toBe('deploymentSystem');
    expect(resolvePlatformAdminBreadcrumbGroup('/admin/feature-flags')).toBe('deploymentSystem');
    expect(resolvePlatformAdminBreadcrumbGroup('/admin/monitoring')).toBe('monitoringLogs');
    expect(resolvePlatformAdminBreadcrumbGroup('/admin/errors')).toBe('monitoringLogs');
    expect(resolvePlatformAdminBreadcrumbGroup('/settings/company')).toBeNull();
  });

  it('builds Overview → Sicherheit & TSE → leaf', () => {
    expect(buildPlatformAdminBreadcrumbs(t, 'securityTse', { title: 'TSE-Verwaltung' })).toEqual([
      { title: 'Overview', href: ADMIN_OVERVIEW_HREF },
      { title: 'Sicherheit & TSE', href: PLATFORM_ADMIN_GROUP_HUB_HREF.securityTse },
      { title: 'TSE-Verwaltung' },
    ]);
  });

  it('auto path breadcrumbs inject platform groups', () => {
    expect(buildPathBreadcrumbs('/admin/tse-management', t).map((c) => c.title)).toEqual([
      'Overview',
      'Sicherheit & TSE',
      'TSE-Verwaltung',
    ]);
    expect(buildPathBreadcrumbs('/admin/tse/failover', t).map((c) => c.title)).toEqual([
      'Overview',
      'Sicherheit & TSE',
      'TSE-Failover',
    ]);
    expect(buildPathBreadcrumbs('/admin/deployments', t).map((c) => c.title)).toEqual([
      'Overview',
      'Deployment & System',
      'Deployments',
    ]);
    expect(buildPathBreadcrumbs('/admin/deployments/tenants', t).map((c) => c.title)).toEqual([
      'Overview',
      'Deployment & System',
      'Deployments',
      'Mandanten-Deployments',
    ]);
    expect(buildPathBreadcrumbs('/admin/monitoring', t).map((c) => c.title)).toEqual([
      'Overview',
      'Monitoring & Logs',
      'Monitoring',
    ]);
    expect(buildPathBreadcrumbs('/admin/access', t).map((c) => c.title)).toEqual([
      'Overview',
      'Verwaltung',
      'Zugriff & Rollen',
    ]);
    expect(buildPathBreadcrumbs('/admin/access/roles', t).map((c) => c.title)).toEqual([
      'Overview',
      'Verwaltung',
      'Zugriff & Rollen',
      'nav.rolesPermissions',
    ]);
  });
});
