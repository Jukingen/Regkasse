import { expect, type Page } from '@playwright/test';

const LOGIN_FIELD = /E-MAIL ODER BENUTZERNAME/i;
const PASSWORD_FIELD = /^PASSWORT$/i;

/** Opens /login for one Development tenant. Credentials stay in the fixture file. */
export async function openLogin(page: Page, slug: string): Promise<void> {
  page.on('dialog', (dialog) => {
    void dialog.accept();
  });
  await page.goto('/login');
  await page.evaluate((tenant) => {
    localStorage.setItem('dev_tenant_id', tenant);
    localStorage.setItem('tenant_slug', tenant);
    localStorage.setItem('pos_onboarding_seen_v1', '1');
  }, slug);
  await page.reload();
  await page.getByRole('textbox', { name: LOGIN_FIELD }).waitFor({ timeout: 90_000 });
  await page.evaluate((tenant) => {
    localStorage.setItem('dev_tenant_id', tenant);
    localStorage.setItem('tenant_slug', tenant);
    localStorage.setItem('pos_onboarding_seen_v1', '1');
  }, slug);
}

/** Signs in on the current login screen and waits until the POS shell is up. */
export async function login(page: Page, username: string, password: string): Promise<void> {
  const userField = page.getByRole('textbox', { name: LOGIN_FIELD });
  const passField = page.getByRole('textbox', { name: PASSWORD_FIELD });
  await userField.click();
  await userField.fill(username);
  await passField.click();
  await passField.fill(password);
  await expect(userField).toHaveValue(username);
  await expect(passField).toHaveValue(password);
  await page.getByRole('button', { name: 'Anmelden' }).click();

  const skip = page.getByRole('button', { name: 'Überspringen' });
  const done = page.getByRole('button', { name: 'Loslegen' });
  const kassa = page.getByText('Kassa', { exact: true }).first();
  const picker = page.getByText('Kasse wählen', { exact: true });
  const loginError = page.getByText(
    /Benutzername oder Passwort|Anmeldung fehlgeschlagen|Mandantenlizenz|keine Berechtigung/i
  );

  await expect
    .poll(
      async () => {
        if (await loginError.isVisible().catch(() => false)) return 'error';
        if (await skip.isVisible().catch(() => false)) return 'onboarding';
        if (await done.isVisible().catch(() => false)) return 'onboarding-last';
        if (await picker.isVisible().catch(() => false)) return 'picker';
        if (await page.getByRole('tab', { name: /Kassa/ }).first().isVisible().catch(() => false))
          return 'shell';
        if (isCashRegisterPath(page.url()) && (await kassa.isVisible().catch(() => false))) return 'shell';
        return 'pending';
      },
      { timeout: 60_000, intervals: [500, 1000, 2000] }
    )
    .not.toBe('pending');

  if (await loginError.isVisible().catch(() => false)) {
    throw new Error(`Login failed for ${username}: ${(await loginError.first().textContent()) ?? ''}`);
  }

  if (await skip.isVisible().catch(() => false)) {
    await skip.click();
  } else if (await done.isVisible().catch(() => false)) {
    await done.click();
  }

  if (page.url().includes('cash-register-select') || (await picker.isVisible().catch(() => false))) {
    await enterShellFromRegisterPicker(page);
  }

  await expect(page.getByRole('tab', { name: /Kassa/ }).first()).toBeVisible({ timeout: 30_000 });
}

/**
 * Persists a register, then opens the shell.
 * Monatsbeleg can reject the shift and keep the picker on screen. The default
 * register is already saved, so a reload reads it and enters the tabs.
 */
async function enterShellFromRegisterPicker(page: Page): Promise<void> {
  await selectListedRegister(page);
  const kassa = page.getByText('Kassa', { exact: true }).first();
  const entered = await kassa
    .waitFor({ state: 'visible', timeout: 8_000 })
    .then(() => true)
    .catch(() => false);
  if (entered) return;
  await page.reload();
}

/** Prefer KASSE-001. Demo tenant `dev` also lists other registers. */
async function selectListedRegister(page: Page): Promise<void> {
  const preferred = page.getByRole('button', { name: /KASSE-001/i });
  if ((await preferred.count()) > 0) {
    await preferred.first().click();
    return;
  }
  await page.getByRole('button', { name: /Auswählen/ }).first().click();
}

function isCashRegisterPath(url: string): boolean {
  return /\/cash-register(?:\/)?(?:\?|#|$)/.test(url);
}

/** Expo's dev error toast covers the Kassa tab. Remove it so the tab receives the click. */
async function hideDevErrorToast(page: Page): Promise<void> {
  const dismiss = page.getByRole('button', { name: 'Dismiss error' });
  if (await dismiss.isVisible().catch(() => false)) {
    await dismiss.click();
  }
  await page.evaluate(() => {
    document.getElementById('error-toast')?.remove();
  });
}

/** Profile layouts may open appointments or rooms. The sale screen is still Kassa. */
export async function openCashRegister(page: Page): Promise<void> {
  if (page.url().includes('cash-register-select')) {
    await enterShellFromRegisterPicker(page);
  }

  // Appointment and room profiles leave Kassa once, about 700ms after the shell mounts.
  await page.waitForTimeout(800);
  await hideDevErrorToast(page);

  const tab = page.getByRole('tab', { name: /Kassa/ }).filter({ visible: true }).first();
  await expect(async () => {
    await hideDevErrorToast(page);
    await tab.click({ timeout: 2_000 });
    await expect(tab).toHaveAttribute('aria-selected', 'true');
  }).toPass({ timeout: 20_000 });
  await expect(page).toHaveURL(/\/cash-register/);
}
