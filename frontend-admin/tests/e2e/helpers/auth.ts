import type { Page } from '@playwright/test';

import { installAdminApiMocks } from './apiMocks';
import { e2eCredentials, isLiveE2E } from './env';
import { makeE2eJwt } from './jwt';

const ACCESS_COOKIE = 'rk_admin_access_token';
const API_ACCESS_COOKIE = 'access_token';
const EDGE_COOKIE = 'rk_admin_edge_session';

export async function preparePage(page: Page): Promise<void> {
  if (!isLiveE2E()) {
    await installAdminApiMocks(page);
  }
}

/** Inject session cookies so Edge proxy and client AuthGate both see a session. */
export async function injectAdminSession(page: Page, token = makeE2eJwt()): Promise<string> {
  await page.context().addCookies([
    {
      name: ACCESS_COOKIE,
      value: encodeURIComponent(token),
      domain: '127.0.0.1',
      path: '/',
      sameSite: 'Lax',
      httpOnly: false,
    },
    {
      name: API_ACCESS_COOKIE,
      value: token,
      domain: '127.0.0.1',
      path: '/',
      sameSite: 'Lax',
      httpOnly: true,
    },
    {
      name: EDGE_COOKIE,
      value: '1',
      domain: '127.0.0.1',
      path: '/',
      sameSite: 'Lax',
      httpOnly: false,
    },
  ]);

  await page.addInitScript(() => {
    window.localStorage.removeItem('rk_admin_access_token');
    window.localStorage.removeItem('rk_admin_refresh_token');
  });

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

  await page.waitForFunction(
    () => document.cookie.split(';').some((part) => part.trim() === 'rk_admin_edge_session=1'),
    { timeout: 15_000 }
  );

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
