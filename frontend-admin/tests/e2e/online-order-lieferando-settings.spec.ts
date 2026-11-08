import { expect, test, type Page } from '@playwright/test';

import { injectAdminSession, loginAsAdmin, preparePage } from './helpers/auth';
import { isLiveE2E } from './helpers/env';
import { makeE2eJwt } from './helpers/jwt';

const PROFILE_SIMULATION_KEY = 'regkasse.admin.profileSimulation';
const PRODUCTS_PATH = '/api/admin/products';
const MAPPINGS_PATH = '/api/admin/online-order-integrations/product-mappings';
const PROVIDERS_PATH = '/api/admin/online-order-integrations/providers';
const CREDENTIALS_PATH = '/api/admin/online-order-integrations/lieferando/credentials';

async function installMarketplaceMocks(page: Page) {
  await page.route(`**${PRODUCTS_PATH}*`, async (route) => {
    if (route.request().method() !== 'GET') {
      await route.fallback();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [{ id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', name: 'Pizza', isActive: true }],
        page: 1,
        pageSize: 100,
        totalCount: 1,
      }),
    });
  });

  await page.route(`**${MAPPINGS_PATH}*`, async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [] }),
      });
      return;
    }
    await route.fallback();
  });
}

async function pinVerticalProfile(page: Page, profileId: string) {
  await page.addInitScript((key) => {
    window.localStorage.setItem(key, 'actual');
  }, PROFILE_SIMULATION_KEY);
  await page.route('**/api/admin/vertical-profile', async (route) => {
    if (route.request().method() !== 'GET') {
      await route.fallback();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        profileId,
        name: profileId,
        posLayout: 'standard',
        posFeatures: {},
      }),
    });
  });
}

async function openAs(page: Page, role: 'SuperAdmin' | 'Manager') {
  if (!isLiveE2E()) {
    if (role === 'Manager') {
      await page.route('**/api/Auth/me', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: '33333333-3333-4333-8333-333333333333',
            userName: 'manager1',
            email: 'manager@example.test',
            firstName: 'Mandant',
            lastName: 'Admin',
            role: 'Manager',
            roles: ['Manager'],
            permissions: ['digital.orders.manage', 'user.view', 'settings.view'],
            isActive: true,
            mustChangePasswordOnNextLogin: false,
            tenantId: '22222222-2222-4222-8222-222222222222',
            tenantSlug: 'dev',
            tenantDisplayName: 'Dev Tenant',
            appContext: 'admin',
          }),
        });
      });
      await injectAdminSession(page, makeE2eJwt({ role: 'Manager', sub: 'e2e-manager' }));
    } else {
      await injectAdminSession(page);
    }
  } else {
    await loginAsAdmin(page);
  }
  await page.goto('/admin/online-order-integrations', { waitUntil: 'domcontentloaded' });
}

test.describe('Lieferando provider settings', () => {
  test('gastronomy super admin sees the enable switch and no Lieferando secret', async ({ page }) => {
    await preparePage(page);
    await pinVerticalProfile(page, 'gastronomy');
    await installMarketplaceMocks(page);
    await page.route(`**${PROVIDERS_PATH}**`, async (route) => {
      const provider = new URL(route.request().url()).searchParams.get('provider');
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          provider: provider ?? 'wolt',
          enabled: provider === 'lieferando',
          credentialStatus: provider === 'lieferando' || provider === 'foodora' ? 'tenant' : 'configured',
        }),
      });
    });
    await openAs(page, 'SuperAdmin');

    await expect(page.getByRole('switch', { name: 'Lieferando aktiv' })).toBeVisible({ timeout: 20_000 });
    await expect(page.getByLabel('Authorization-Schlüssel')).toHaveCount(0);
    await expect(page.getByLabel('HMAC-Geheimnis')).toHaveCount(0);
    await expect(page.getByLabel('X-Flyt-API-Key')).toHaveCount(0);
  });

  test('tenant admin cannot edit secrets while Lieferando is disabled', async ({ page }) => {
    await preparePage(page);
    await pinVerticalProfile(page, 'gastronomy');
    await installMarketplaceMocks(page);
    await page.route(`**${CREDENTIALS_PATH}**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ providerEnabled: false, credentialStatus: 'missing' }),
      });
    });
    await openAs(page, 'Manager');

    const section = page.locator('section').filter({ has: page.getByRole('heading', { name: 'Lieferando-Zugang' }) });
    await expect(section.getByText('Vom Super-Admin deaktiviert', { exact: true })).toBeVisible({
      timeout: 20_000,
    });
    await expect(page.getByLabel('Authorization-Schlüssel')).toBeDisabled();
    await expect(page.getByLabel('HMAC-Geheimnis')).toBeDisabled();
    await expect(page.getByLabel('X-Flyt-API-Key')).toBeDisabled();
    await expect(page.getByLabel('Marken-ID')).toBeDisabled();
    await expect(page.getByRole('button', { name: 'Lieferando speichern' })).toBeDisabled();
  });

  test('tenant admin saves Lieferando credentials and reload hides the plaintext', async ({ page }) => {
    await preparePage(page);
    await pinVerticalProfile(page, 'gastronomy');
    await installMarketplaceMocks(page);
    let configured = false;
    await page.route(`**${CREDENTIALS_PATH}**`, async (route) => {
      if (route.request().method() === 'PUT') {
        const body = route.request().postDataJSON() as {
          clientId?: string;
          clientSecret?: string;
          chainId?: string;
          webhookToken?: string;
        };
        expect(body.clientId).toBe('jet-key');
        expect(body.clientSecret).toBe('hmac-secret');
        expect(body.chainId).toBe('brand-1');
        expect(body.webhookToken).toBe('flyt-key');
        configured = true;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc' }),
        });
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          providerEnabled: true,
          credentialStatus: configured ? 'configured' : 'missing',
        }),
      });
    });
    await openAs(page, 'Manager');

    await expect(page.getByLabel('Authorization-Schlüssel')).toBeEnabled({ timeout: 20_000 });
    await page.getByLabel('Authorization-Schlüssel').fill('jet-key');
    await page.getByLabel('HMAC-Geheimnis').fill('hmac-secret');
    await page.getByLabel('X-Flyt-API-Key').fill('flyt-key');
    await page.getByLabel('Marken-ID').fill('brand-1');
    await page.getByRole('button', { name: 'Lieferando speichern' }).click();

    const section = page.locator('section').filter({ has: page.getByRole('heading', { name: 'Lieferando-Zugang' }) });
    await expect(section.getByText('Zugangsdaten hinterlegt', { exact: true })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByLabel('HMAC-Geheimnis')).toHaveValue('');
    await expect(page.getByLabel('X-Flyt-API-Key')).toHaveValue('');
    await expect(page.getByText('hmac-secret')).toHaveCount(0);
    await expect(page.getByText('flyt-key')).toHaveCount(0);
  });
});
