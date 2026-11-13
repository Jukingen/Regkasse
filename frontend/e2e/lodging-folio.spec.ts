import { readFileSync } from 'node:fs';
import path from 'node:path';

import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

import { login, openLogin } from './helpers/login';

type TestUser = { username: string; password: string };

const users = JSON.parse(
  readFileSync(path.join(__dirname, 'fixtures', 'test-users.json'), 'utf8')
) as Record<string, TestUser>;

const API = process.env.E2E_API_BASE_URL ?? 'http://localhost:5184/api';

type RoomRow = { id: string; status: string; occupied: boolean };

/** A previous check-out leaves the room in Cleaning. Cashiers cannot create rooms, so the test frees that room. */
async function ensureFreeRoom(request: APIRequestContext, account: TestUser): Promise<void> {
  const login = await request.post(`${API}/Auth/login`, {
    headers: { 'X-Tenant-Id': 'dev-beherbergung' },
    data: { loginIdentifier: account.username, password: account.password, clientApp: 'pos' },
  });
  expect(login.ok(), 'POS login for room setup failed').toBeTruthy();
  const token = ((await login.json()) as { token?: string }).token;
  if (!token) throw new Error('POS login did not return a token');
  const headers = { Authorization: `Bearer ${token}`, 'X-Tenant-Id': 'dev-beherbergung' };
  const listed = await request.get(`${API}/pos/rooms`, { headers });
  expect(listed.ok()).toBeTruthy();
  const rooms = (await listed.json()) as RoomRow[];
  if (rooms.some((room) => room.status === 'Available' && !room.occupied)) return;
  const cleaning = rooms.find((room) => room.status === 'Cleaning' && !room.occupied);
  if (!cleaning) {
    throw new Error('Kein freies Zimmer. Ein Kassierer kann keine Zimmer anlegen.');
  }
  const patched = await request.patch(`${API}/pos/rooms/${cleaning.id}`, {
    headers,
    data: { status: 'Available' },
  });
  expect(patched.ok(), 'Cleaning room could not be set to Available').toBeTruthy();
}

async function openRooms(page: Page): Promise<void> {
  const room = page.getByRole('button', { name: /Zimmer \d+, / }).first();
  if (!(await room.isVisible().catch(() => false))) {
    await page.getByText('Zimmer', { exact: true }).first().click();
  }
  await expect(room).toBeVisible({ timeout: 20_000 });
}

test.describe.configure({ timeout: 180_000 });

test('dev-beherbergung check-in, charge, and check-out', async ({ page, request }) => {
  const account = users['dev-beherbergung'];
  if (!account?.username || !account.password) {
    throw new Error('Missing fixture credentials for dev-beherbergung');
  }

  await ensureFreeRoom(request, account);
  const guestName = `E2E Gast ${Date.now()}`;
  await openLogin(page, 'dev-beherbergung');
  await login(page, account.username, account.password);
  await openRooms(page);

  await page.getByRole('button', { name: 'Check-in', exact: true }).click();
  await page.getByRole('textbox', { name: 'Name' }).fill(guestName);
  await page.getByRole('button', { name: 'Neuen Gast anlegen' }).click();
  await expect(page.getByText(`Gast: ${guestName}`)).toBeVisible({ timeout: 20_000 });

  const room = page.getByRole('button', { name: /Zimmer .+ wählen/ }).first();
  await expect(room, 'Kein freies Zimmer für den Check-in.').toBeVisible();
  const roomLabel = (await room.getAttribute('aria-label')) ?? '';
  const roomNumber = /Zimmer (.+) wählen/.exec(roomLabel)?.[1] ?? '';
  await room.click();
  await page.getByRole('button', { name: 'Check-in speichern' }).click();

  await expect(page.getByText(new RegExp(`${guestName} ·`))).toBeVisible({ timeout: 20_000 });

  await page.getByRole('textbox', { name: 'Beschreibung' }).fill('Minibar');
  await page.getByRole('textbox', { name: 'Betrag' }).fill('4.50');
  await page.getByRole('button', { name: 'Buchung speichern' }).click();
  await expect(page.getByText('Minibar · 4.50')).toBeVisible({ timeout: 20_000 });
  await expect(page.getByText(new RegExp(`${guestName} · 4\\.50`))).toBeVisible();

  await page.getByRole('button', { name: 'Konto anzeigen' }).click();
  await expect(page.getByLabel(/Minibar · 4\.50/)).toBeVisible({ timeout: 20_000 });
  await page.getByRole('button', { name: 'Check-out' }).click();
  await expect(page.getByText(/Summe 4\.50/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Zur Kasse' })).toBeVisible();
  await page.getByRole('button', { name: 'Check-out bestätigen' }).click();

  await expect(page.getByRole('button', { name: 'Check-in', exact: true })).toBeVisible({ timeout: 20_000 });
  if (roomNumber) {
    await expect(page.getByRole('button', { name: `Zimmer ${roomNumber}, Reinigung` })).toBeVisible();
  }
});
