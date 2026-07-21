import type { Page } from '@playwright/test';

import { installAdminApiMocks } from './apiMocks';
import { e2eCredentials, isLiveE2E } from './env';
import { makeE2eJwt } from './jwt';

const ACCESS_COOKIE = 'rk_admin_access_token';

export async function preparePage(page: Page): Promise<void> {
  if (!isLiveE2E()) {
    await installAdminApiMocks(page);
  }
}

/** Inject JWT cookie + localStorage so Edge proxy and client AuthGate both see a session. */
export async function injectAdminSession(page: Page, token = makeE2eJwt()): Promise<string> {
  const baseURL = page.context().browser()?.contexts()[0] ? undefined : undefined;
  void baseURL;

  await page.context().addCookies([
    {
      name: ACCESS_COOKIE,
      value: encodeURIComponent(token),
      domain: '127.0.0.1',
      path: '/',
      sameSite: 'Lax',
      httpOnly: false,
    },
  ]);

  await page.addInitScript(
    ({ accessToken, refreshToken }) => {
      window.localStorage.setItem('rk_admin_access_token', accessToken);
      window.localStorage.setItem('rk_admin_refresh_token', refreshToken);
      // Avoid stale CreateTenantWizard drafts poisoning slug validation in E2E.
      window.localStorage.removeItem('fa:draft:create-tenant-wizard:v1');
    },
    { accessToken: token, refreshToken: 'e2e-refresh' }
  );

  return token;
}

export async function loginAsAdmin(
  page: Page,
  overrides?: Partial<ReturnType<typeof e2eCredentials>> & { expectSuccess?: boolean }
) {
  const { expectSuccess = true, ...credentialOverrides } = overrides ?? {};
  const credentials = { ...e2eCredentials(), ...credentialOverrides };

  await page.goto('/login');
  await page.getByLabel(/E-Mail oder Benutzername/i).fill(credentials.loginIdentifier);
  await page.getByLabel(/^Passwort$/i).fill(credentials.password);

  if (!expectSuccess) {
    await page.getByRole('button', { name: /^Anmelden$/i }).click();
    return;
  }

  const loginResponsePromise = page.waitForResponse(
    (response) =>
      response.url().includes('/api/Auth/login') && response.request().method() === 'POST'
  );

  await page.getByRole('button', { name: /^Anmelden$/i }).click();
  const loginResponse = await loginResponsePromise;
  if (loginResponse.status() >= 400) {
    throw new Error(`Login failed with HTTP ${loginResponse.status()}`);
  }

  await page.waitForFunction(() => Boolean(window.localStorage.getItem('rk_admin_access_token')), {
    timeout: 15_000,
  });

  const token = await page.evaluate(() => window.localStorage.getItem('rk_admin_access_token'));
  if (!token) {
    throw new Error('Login succeeded but access token was not persisted');
  }

  await page.context().addCookies([
    {
      name: ACCESS_COOKIE,
      value: encodeURIComponent(token),
      domain: '127.0.0.1',
      path: '/',
      sameSite: 'Lax',
      httpOnly: false,
    },
  ]);

  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' });
}

export async function expectAuthenticatedShell(page: Page) {
  await page.waitForURL((url) => !url.pathname.startsWith('/login'), { timeout: 30_000 });
  await page.locator('#main-content').waitFor({ state: 'visible', timeout: 30_000 });
}

/** Authenticated setup without UI login (faster for post-login flows). */
export async function openAuthenticated(page: Page, path = '/dashboard') {
  await preparePage(page);
  await injectAdminSession(page);
  await page.goto(path, { waitUntil: 'domcontentloaded' });
  await expectAuthenticatedShell(page);
}
