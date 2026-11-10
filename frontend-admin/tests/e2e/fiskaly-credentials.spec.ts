import { expect, test, type Page } from '@playwright/test';

import { injectAdminSession, preparePage } from './helpers/auth';
import { isLiveE2E } from './helpers/env';
import { makeE2eJwt } from './helpers/jwt';

const CREDENTIALS_PATH = '/api/admin/fiskaly/credentials';
const SECRET = 'super-secret-value';
const API_KEY = 'test_key_abcd';
const SCU_ID = 'e741519c-4367-4d72-abcf-1b1430d59ec7';

async function openManagerSettings(page: Page) {
  if (!isLiveE2E()) {
    await page.route('**/api/**', async (route) => {
      const request = route.request();
      const path = new URL(request.url()).pathname;
      const method = request.method();

      if (path === '/api/Auth/me' && method === 'GET') {
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
            permissions: ['settings.view', 'cash_register.manage'],
            isActive: true,
            mustChangePasswordOnNextLogin: false,
            tenantId: '22222222-2222-4222-8222-222222222222',
            tenantSlug: 'dev',
            tenantDisplayName: 'Dev Tenant',
            appContext: 'admin',
          }),
        });
        return;
      }

      if (path === '/api/company/settings' && method === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            companyName: 'Test Café',
            companyAddress: 'Wien',
            companyTaxNumber: 'ATU12345678',
            isActive: true,
          }),
        });
        return;
      }

      if (path.startsWith('/api/admin/fiskaly/settings') && method === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            enabled: true,
            configEnabled: true,
            environment: 'TEST',
            isConfigured: false,
            apiBaseUrl: 'https://rksv.fiskaly.com/api/v1',
            source: 'config',
          }),
        });
        return;
      }

      await route.fallback();
    });
    await injectAdminSession(page, makeE2eJwt({ role: 'Manager', sub: 'e2e-manager' }));
  }
  await page.goto('/settings', { waitUntil: 'domcontentloaded' });
}

test.describe('Fiskaly tenant credentials', () => {
  test('tenant admin saves credentials and reload hides the secret', async ({ page }) => {
    test.skip(isLiveE2E(), 'Mocked credential round-trip');
    await preparePage(page);
    let configured = false;
    await page.route(`**${CREDENTIALS_PATH}**`, async (route) => {
      if (route.request().method() === 'PUT') {
        const body = route.request().postDataJSON() as {
          apiKey?: string;
          apiSecret?: string;
          scuId?: string;
          environment?: string;
        };
        expect(body.apiKey).toBe(API_KEY);
        expect(body.apiSecret).toBe(SECRET);
        expect(body.scuId).toBe(SCU_ID);
        expect(body.environment).toBe('TEST');
        configured = true;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            configured: true,
            isActive: true,
            apiKeyHint: 'abcd',
            scuId: SCU_ID,
            environment: 'TEST',
          }),
        });
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(
          configured
            ? {
                configured: true,
                isActive: true,
                apiKeyHint: 'abcd',
                scuId: SCU_ID,
                environment: 'TEST',
              }
            : {
                configured: false,
                isActive: false,
                apiKeyHint: null,
                scuId: null,
                environment: null,
              }
        ),
      });
    });
    await openManagerSettings(page);

    await expect(page.getByLabel('API-Schlüssel')).toBeVisible({ timeout: 20_000 });
    await page.getByLabel('API-Schlüssel').fill(API_KEY);
    await page.getByLabel('API-Geheimnis').fill(SECRET);
    await page.getByLabel('SCU-ID').fill(SCU_ID);
    await page.getByRole('button', { name: 'Fiskaly speichern' }).click();

    await expect(page.getByText('Hinterlegt (····abcd)')).toBeVisible({ timeout: 15_000 });
    await expect(page.getByLabel('API-Geheimnis')).toHaveValue('');
    await expect(page.getByText(SECRET)).toHaveCount(0);

    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(page.getByText('Hinterlegt (····abcd)')).toBeVisible({ timeout: 20_000 });
    await expect(page.getByLabel('API-Geheimnis')).toHaveValue('');
    await expect(page.getByText(SECRET)).toHaveCount(0);
    await expect(page.getByLabel('SCU-ID')).toHaveValue(SCU_ID);
  });
});
