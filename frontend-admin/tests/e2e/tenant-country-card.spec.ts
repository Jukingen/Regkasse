import { type Page, expect, test } from '@playwright/test';

import { injectAdminSession, preparePage, expectAuthenticatedShell } from './helpers/auth';
import { makeE2eJwt } from './helpers/jwt';

const TENANT_ID = '22222222-2222-4222-8222-222222222222';

const TENANT_DETAIL = {
  id: TENANT_ID,
  name: 'Dev Tenant',
  slug: 'dev',
  email: 'dev@example.com',
  isActive: true,
  status: 'active',
  createdAt: '2026-01-01T00:00:00Z',
  country: 'AT',
  vatRegime: 'AT_RKSV_STANDARD',
  vatId: 'ATU12345678',
  billingCountry: null,
  taxExempt: false,
};

function meFor(role: 'SuperAdmin' | 'Manager') {
  return {
    id: '11111111-1111-4111-8111-111111111111',
    userName: role === 'Manager' ? 'manager1' : 'admin@admin.com',
    email: role === 'Manager' ? 'manager@example.com' : 'admin@admin.com',
    firstName: 'Admin',
    lastName: 'User',
    role,
    roles: [role],
    permissions:
      role === 'Manager'
        ? ['user.view', 'user.manage', 'settings.view', 'backup.manage', 'license.manage']
        : ['system.critical', 'tenant.manage', 'user.view', 'user.manage', 'settings.manage'],
    isActive: true,
    mustChangePasswordOnNextLogin: false,
    tenantId: TENANT_ID,
    tenantSlug: 'dev',
    tenantDisplayName: 'Dev Tenant',
    appContext: 'admin',
  };
}

async function openTenantDetail(page: Page, role: 'SuperAdmin' | 'Manager') {
  await preparePage(page);
  await page.route('**/api/Auth/me', async (route) => {
    if (route.request().method() !== 'GET') {
      await route.fallback();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(meFor(role)),
    });
  });
  await page.route(`**/api/admin/tenants/${TENANT_ID}`, async (route) => {
    if (route.request().method() !== 'GET') {
      await route.fallback();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(TENANT_DETAIL),
    });
  });
  await page.route(`**/api/admin/tenants/${TENANT_ID}/country-impact**`, async (route) => {
    const country = new URL(route.request().url()).searchParams.get('country');
    const incompatible = country === 'DE';
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        affectedRowCount: 2,
        incompatibleRowCount: incompatible ? 1 : 0,
      }),
    });
  });

  await injectAdminSession(page, makeE2eJwt({ role, sub: role === 'Manager' ? 'e2e-manager' : 'e2e-super-admin' }));
  await page.goto(`/admin/tenants/${TENANT_ID}`, { waitUntil: 'domcontentloaded' });
  await expectAuthenticatedShell(page);
}

test.describe('Tenant country card', () => {
  test('Super Admin can edit the country selector', async ({ page }) => {
    await openTenantDetail(page, 'SuperAdmin');

    const card = page.getByTestId('tenant-country-card');
    await expect(card).toBeVisible({ timeout: 20_000 });
    await expect(card).toContainText('Land & Fiskalregime');
    await expect(page.getByTestId('tenant-country-view-only')).toHaveCount(0);

    await page.getByTestId('tenant-country-edit').click();
    const country = card.getByLabel('Land');
    await expect(country).toBeVisible();
    await expect(country).toBeEnabled();
    await country.click();
    const countryList = page.locator('.ant-select-dropdown:visible');
    await expect(countryList).toBeVisible();
    await expect(countryList).toContainText('DE — Germany');
    await expect(countryList).not.toContainText('EU_DEFAULT');
  });

  test('Mandanten-Admin sees the card read-only', async ({ page }) => {
    const browserConsole: string[] = [];
    page.on('console', (msg) => browserConsole.push(`[${msg.type()}] ${msg.text()}`));
    page.on('pageerror', (err) => browserConsole.push(`[pageerror] ${err.message}`));
    await preparePage(page);
    await page.route('**/api/Auth/me', async (route) => {
      if (route.request().method() !== 'GET') {
        await route.fallback();
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(meFor('Manager')),
      });
    });
    await page.route('**/api/company/settings', async (route) => {
      const path = new URL(route.request().url()).pathname;
      if (route.request().method() !== 'GET' || path !== '/api/company/settings') {
        await route.fallback();
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          tenantId: TENANT_ID,
          companyName: 'Dev Tenant',
          companyTaxNumber: 'ATU12345678',
          country: 'AT',
          vatRegime: 'AT_RKSV_STANDARD',
          vatId: 'ATU12345678',
          billingCountry: null,
          taxExempt: false,
          createdAt: '2026-01-01T00:00:00Z',
          currency: 'EUR',
          language: 'de-DE',
          timeZone: 'Europe/Vienna',
          dateFormat: 'dd.MM.yyyy',
          timeFormat: 'HH:mm:ss',
        }),
      });
    });
    await injectAdminSession(page, makeE2eJwt({ role: 'Manager', sub: 'e2e-manager' }));
    await page.goto('/tenant/profile', { waitUntil: 'domcontentloaded' });
    await expectAuthenticatedShell(page);

    const card = page.getByTestId('tenant-country-card');
    try {
      await expect(card).toBeVisible({ timeout: 20_000 });
    } catch (err) {
      console.log(`BROWSER CONSOLE:\n${browserConsole.join('\n')}`);
      throw err;
    }
    await expect(page.getByTestId('tenant-country-view-only')).toBeVisible();
    await expect(page.getByTestId('tenant-country-view-only')).toContainText(
      'Nur Ansicht. Änderungen sind Super-Administratoren vorbehalten.'
    );
    await expect(page.getByTestId('tenant-country-edit')).toHaveCount(0);
    await expect(card.getByLabel('Land')).toHaveCount(0);
  });

  test('blocks confirm when signed fiscal documents would be invalidated', async ({ page }) => {
    await openTenantDetail(page, 'SuperAdmin');
    await expect(page.getByTestId('tenant-country-card')).toBeVisible({ timeout: 20_000 });
    await page.getByTestId('tenant-country-edit').click();

    const country = page.getByTestId('tenant-country-card').getByLabel('Land');
    await country.click();
    await page.keyboard.type('DE');
    await page.keyboard.press('Enter');
    await page.getByTestId('tenant-country-save').click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(
      'Der Wechsel ist nicht möglich. 1 signierte Belege wären unter dem neuen Land ungültig.'
    );
    await expect(page.getByTestId('tenant-country-affected-count')).toContainText(
      '2 bestehende Fiskalbelege sind betroffen'
    );
    await expect(dialog.getByRole('button', { name: 'Wechseln' })).toBeDisabled();
  });
});
