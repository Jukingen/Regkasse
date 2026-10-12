import { readFileSync } from 'node:fs';
import path from 'node:path';

import { expect, test, type Page } from '@playwright/test';

import { login, openCashRegister, openLogin } from './helpers/login';

type TestUser = { username: string; password: string };

const users = JSON.parse(
  readFileSync(path.join(__dirname, 'fixtures', 'test-users.json'), 'utf8')
) as Record<string, TestUser>;

type ProfileCheck = {
  slug: string;
  assertUi: (page: Page) => Promise<void>;
};

async function visible(page: Page, text: string): Promise<void> {
  const locator = page.getByText(text, { exact: true }).first();
  await locator.scrollIntoViewIfNeeded();
  await expect(locator).toBeVisible();
}

async function absent(page: Page, text: string): Promise<void> {
  await expect(page.getByText(text, { exact: true })).toHaveCount(0);
}

const profiles: ProfileCheck[] = [
  {
    slug: 'dev',
    assertUi: async (page) => {
      await visible(page, 'Küche');
      await absent(page, 'Tisch wählen');
    },
  },
  {
    slug: 'dev-gastronomy-tables',
    assertUi: async (page) => {
      await visible(page, 'Küche');
      await visible(page, 'Tisch wählen');
    },
  },
  {
    slug: 'dev-vet',
    assertUi: async (page) => {
      await visible(page, 'Tierakte');
    },
  },
  {
    slug: 'dev-doctor',
    assertUi: async (page) => {
      await visible(page, 'Patientenakte');
    },
  },
  {
    slug: 'dev-hair-salon',
    assertUi: async (page) => {
      await visible(page, 'Termine');
    },
  },
  {
    slug: 'dev-taxi',
    assertUi: async (page) => {
      await visible(page, 'Taxi-Fahrt');
    },
  },
  {
    slug: 'dev-handy-shop',
    assertUi: async (page) => {
      await visible(page, 'IMEI / Seriennummer');
    },
  },
  {
    slug: 'dev-ticket-sales',
    assertUi: async (page) => {
      await visible(page, 'Ticket erzeugen');
    },
  },
  {
    slug: 'dev-mobile-services',
    assertUi: async (page) => {
      await visible(page, 'Adresse und Einsatzort');
    },
  },
  {
    slug: 'dev-beherbergung',
    assertUi: async (page) => {
      await visible(page, 'Zimmer');
    },
  },
];

async function assertSharedFiscalActions(page: Page): Promise<void> {
  await page.goto('/settings');
  await expect(page.getByText('Einstellungen', { exact: true }).first()).toBeVisible();

  const receipts = page.getByText('Belegliste öffnen', { exact: true });
  await receipts.scrollIntoViewIfNeeded();
  await expect(receipts).toBeVisible();

  // The Tagesabschluss button renders only while a shift is open. Startbeleg /
  // Monatsbeleg keep the shift closed, and the Schicht section still exposes it.
  const shift = page.getByText('Schicht', { exact: true }).first();
  await shift.scrollIntoViewIfNeeded();
  await expect(shift).toBeVisible();
  await expect(page.getByText(/Tagesabschluss bleibt manuell/)).toBeVisible();
  const closing = page.getByRole('button', { name: 'Tagesabschluss' });
  if ((await closing.count()) > 0) {
    await expect(closing).toBeVisible();
  }
}

for (const profile of profiles) {
  test(profile.slug, async ({ page }, testInfo) => {
    const account = users[profile.slug];
    if (!account?.username || !account.password) {
      throw new Error(`Missing fixture credentials for ${profile.slug}`);
    }

    try {
      await openLogin(page, profile.slug);
      await login(page, account.username, account.password);
      await openCashRegister(page);
      await profile.assertUi(page);
      await assertSharedFiscalActions(page);
    } catch (error) {
      await page
        .screenshot({
          path: path.join('e2e', 'screenshots', `${profile.slug}.png`),
          fullPage: true,
        })
        .catch(() => undefined);
      await testInfo.attach(`${profile.slug}-url`, {
        body: page.url(),
        contentType: 'text/plain',
      });
      throw error;
    }
  });
}
