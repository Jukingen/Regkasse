import type { Page, Route } from '@playwright/test';

import { makeE2eJwt } from './jwt';

const DEMO_TENANT_ID = '22222222-2222-4222-8222-222222222222';

const SUPER_ADMIN_ME = {
  id: '11111111-1111-4111-8111-111111111111',
  userName: 'admin@admin.com',
  email: 'admin@admin.com',
  firstName: 'Admin',
  lastName: 'User',
  role: 'SuperAdmin',
  roles: ['SuperAdmin'],
  permissions: [
    'system.critical',
    'tenant.manage',
    'user.view',
    'user.manage',
    'settings.view',
    'settings.manage',
    'backup.manage',
    'report.view',
    'report.export',
    'audit.view',
  ],
  isActive: true,
  mustChangePasswordOnNextLogin: false,
  tenantId: DEMO_TENANT_ID,
  tenantSlug: 'dev',
  tenantDisplayName: 'Dev Tenant',
  appContext: 'admin',
};

const DEMO_TENANT = {
  id: DEMO_TENANT_ID,
  name: 'Dev Tenant',
  slug: 'dev',
  email: 'dev@example.com',
  isActive: true,
  status: 'Active',
  licenseValid: true,
  licenseValidUntilUtc: '2099-12-31T00:00:00Z',
};

async function json(route: Route, status: number, body: unknown) {
  await route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}

/**
 * Intercepts backend API calls so CI E2E does not need a live ASP.NET + Postgres stack.
 * Live runs should skip this helper (`E2E_LIVE=1`).
 */
export async function installAdminApiMocks(page: Page): Promise<void> {
  const accessToken = makeE2eJwt();

  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const method = request.method();
    const url = new URL(request.url());
    const path = url.pathname;

    if (method === 'OPTIONS') {
      await route.fulfill({ status: 204 });
      return;
    }

    if (path === '/api/tenants/switcher' && method === 'GET') {
      await json(route, 200, [DEMO_TENANT]);
      return;
    }

    if (path === '/api/csrf/token' && method === 'GET') {
      await json(route, 200, { token: 'e2e-csrf-token' });
      return;
    }

    if (path === '/api/Auth/login' && method === 'POST') {
      const body = request.postDataJSON() as {
        loginIdentifier?: string;
        password?: string;
      };
      const identifier = String(body.loginIdentifier ?? '')
        .trim()
        .toLowerCase();
      const password = String(body.password ?? '');

      if (
        password !== 'Admin123!' ||
        (identifier !== 'admin@admin.com' && identifier !== 'admin')
      ) {
        await json(route, 401, {
          message: 'Invalid login credentials',
          title: 'Unauthorized',
        });
        return;
      }

      await json(route, 200, {
        token: accessToken,
        refreshToken: 'e2e-refresh',
        requires2FA: false,
        user: {
          tenantId: SUPER_ADMIN_ME.tenantId,
          tenantSlug: SUPER_ADMIN_ME.tenantSlug,
          mustChangePasswordOnNextLogin: false,
        },
      });
      return;
    }

    if (path === '/api/Auth/me' && method === 'GET') {
      await json(route, 200, SUPER_ADMIN_ME);
      return;
    }

    if (path === '/api/admin/user/preferences') {
      if (method === 'GET') {
        await json(route, 200, {
          themeMode: 'system',
          densityMode: 'standard',
          defaultPage: '/dashboard',
          dateFormat: 'DD.MM.YYYY',
          timeFormat: '24h',
          reducedAnimations: false,
          updatedAtUtc: '2020-01-01T00:00:00.000Z',
        });
        return;
      }
      if (method === 'PUT' || method === 'POST') {
        const body = (request.postDataJSON() as Record<string, unknown> | null) ?? {};
        await json(route, 200, {
          themeMode: body.themeMode ?? 'system',
          densityMode: body.densityMode ?? 'standard',
          defaultPage: body.defaultPage ?? '/dashboard',
          dateFormat: body.dateFormat ?? 'DD.MM.YYYY',
          timeFormat: body.timeFormat ?? '24h',
          reducedAnimations: body.reducedAnimations === true,
          updatedAtUtc: '2020-01-01T00:00:00.000Z',
        });
        return;
      }
    }

    if (
      (path === '/api/tenants/current' || path.endsWith('/tenants/current')) &&
      method === 'GET'
    ) {
      await json(route, 200, {
        id: DEMO_TENANT.id,
        slug: DEMO_TENANT.slug,
        name: DEMO_TENANT.name,
        licenseValid: true,
        licenseValidUntilUtc: '2099-12-31T00:00:00Z',
      });
      return;
    }

    if (path === '/api/admin/tenants/slug-availability' && method === 'GET') {
      const slug = url.searchParams.get('slug') ?? 'e2e-tenant';
      await json(route, 200, {
        normalizedSlug: slug,
        isValid: true,
        available: true,
      });
      return;
    }

    if (path === '/api/admin/tenants/slug-suggestions' && method === 'GET') {
      await json(route, 200, { suggestions: ['e2e-cafe', 'e2e-cafe-1'] });
      return;
    }

    if (path === '/api/admin/tenants' && method === 'GET') {
      await json(route, 200, [DEMO_TENANT]);
      return;
    }

    if (path === '/api/admin/tenants' && method === 'POST') {
      const body = request.postDataJSON() as { name?: string; slug?: string };
      await json(route, 201, {
        ...DEMO_TENANT,
        id: '33333333-3333-4333-8333-333333333333',
        name: body.name ?? 'E2E Tenant',
        slug: body.slug ?? 'e2e-tenant',
      });
      return;
    }

    if (path === '/api/admin/users' && method === 'GET') {
      await json(route, 200, []);
      return;
    }

    if (path === '/api/admin/users/username-suggestions' && method === 'GET') {
      const role = (url.searchParams.get('role') ?? 'Cashier').toLowerCase();
      await json(route, 200, {
        suggestedUsername: `${role}1`,
        availableNumbers: [1, 2, 3],
      });
      return;
    }

    if (path === '/api/UserManagement' && method === 'GET') {
      await json(route, 200, {
        items: [],
        pagination: { page: 1, pageSize: 20, totalCount: 0, totalPages: 0 },
      });
      return;
    }

    if (path === '/api/admin/users' && method === 'POST') {
      await json(route, 201, {
        id: '44444444-4444-4444-8444-444444444444',
        email: 'new.user@example.com',
        userName: 'cashier1',
        generatedPassword: 'TempPass1!',
      });
      return;
    }

    if (/\/api\/admin\/tenants\/[^/]+\/users\/quick$/.test(path) && method === 'POST') {
      await json(route, 201, {
        userId: '55555555-5555-4555-8555-555555555555',
        email: 'quick.user@example.com',
        userName: 'cashier2',
        generatedPassword: 'QuickPass1!',
        forcePasswordChangeOnNextLogin: true,
        success: true,
        role: 'Cashier',
      });
      return;
    }

    if (/\/api\/admin\/tenants\/[^/]+\/users$/.test(path) && method === 'POST') {
      await json(route, 201, {
        userId: '66666666-6666-4666-8666-666666666666',
        id: '66666666-6666-4666-8666-666666666666',
        email: 'tenant.user@example.com',
        userName: 'manager1',
        generatedPassword: 'TenantPass1!',
        temporaryPassword: 'TenantPass1!',
        forcePasswordChangeOnNextLogin: true,
        success: true,
      });
      return;
    }

    if (path === '/api/rksv/monatsbeleg/status-overview' && method === 'GET') {
      await json(route, 200, []);
      return;
    }

    if (path.startsWith('/api/rksv/monatsbeleg/status/') && method === 'GET') {
      await json(route, 200, {
        cashRegisterId: path.split('/').pop(),
        missingMonths: [],
        status: { missingMonths: [] },
      });
      return;
    }

    if (path === '/api/admin/activities/unread-count' && method === 'GET') {
      await json(route, 200, { unreadCount: 0 });
      return;
    }

    if (path === '/api/admin/activities' && method === 'GET') {
      await json(route, 200, { items: [], total: 0, limit: 50, offset: 0 });
      return;
    }

    if (path === '/api/admin/activities/notification-config' && method === 'GET') {
      await json(route, 200, {
        inAppEnabled: true,
        emailEnabled: false,
        emailRecipients: [],
        webhookEnabled: false,
        enabledEvents: {},
        severityThreshold: {},
      });
      return;
    }

    if (path === '/api/admin/activities/stream' && method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'text/event-stream',
        body: ':\n\n',
      });
      return;
    }

    // Prefer empty arrays for unknown list GETs — `{}` breaks `.filter` / `.map` callers.
    if (method === 'GET') {
      const wantsArray =
        path.endsWith('s') ||
        path.includes('/list') ||
        path.includes('/overview') ||
        path.includes('/switcher');
      await json(route, 200, wantsArray ? [] : {});
      return;
    }

    await json(route, 200, { success: true });
  });
}
