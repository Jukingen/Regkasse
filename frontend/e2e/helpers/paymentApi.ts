import { readFileSync } from 'node:fs';
import path from 'node:path';

import { expect, type APIRequestContext } from '@playwright/test';

const users = JSON.parse(
  readFileSync(path.join(__dirname, '../fixtures/test-users.json'), 'utf8')
) as Record<string, { username: string; password: string }>;

/**
 * Seeded `dev` cashier from the POS fixture. `cashier1` exists in some local
 * databases with a different password, so the suite does not use that account.
 * `admin1` is the seeded Manager on the same tenant and shares this password.
 */
export const DEV_TENANT = 'dev';
const CASHIER = users[DEV_TENANT];
const DEMO_PASSWORD = CASHIER.password;

export const API = process.env.E2E_API_URL ?? 'http://127.0.0.1:5184';

export const CASHIER_USER = CASHIER.username;

/**
 * Development SuperAdmin from the smoke script. The seeded `admin1` password
 * is not the fixture password on this database, and a cashier cannot read
 * audit or the FinanzOnline outbox.
 */
export const SUPERADMIN_USER = 'admin@admin.com';
export const SUPERADMIN_PASSWORD = 'Admin123!';

export type SaleSession = {
  token: string;
  tenant: string;
  registerId: string;
  customerId: string;
  product: { id: string; name: string; price: number };
};

export type ApiResult = {
  status: number;
  text: string;
  json: Record<string, unknown> | null;
};

function asRecord(value: unknown): Record<string, unknown> | null {
  return value !== null && typeof value === 'object' ? (value as Record<string, unknown>) : null;
}

export function authHeaders(token: string, tenant: string): Record<string, string> {
  return {
    Authorization: `Bearer ${token}`,
    'X-Tenant-Id': tenant,
  };
}

export async function readApi(response: {
  status: () => number;
  text: () => Promise<string>;
}): Promise<ApiResult> {
  const text = await response.text();
  let json: Record<string, unknown> | null = null;
  try {
    json = asRecord(JSON.parse(text));
  } catch {
    json = null;
  }
  return { status: response.status(), text, json };
}

export function diagnosticCode(body: Record<string, unknown> | null): string {
  const details = asRecord(body?.details);
  const context = asRecord(body?.context);
  const code =
    details?.diagnosticCode ??
    details?.code ??
    context?.diagnosticCode ??
    body?.diagnosticCode ??
    body?.code;
  return typeof code === 'string' ? code : '';
}

export async function loginApi(
  request: APIRequestContext,
  username: string,
  clientApp: 'pos' | 'admin',
  tenant = DEV_TENANT,
  password = DEMO_PASSWORD
): Promise<string> {
  const response = await request.post(`${API}/api/Auth/login`, {
    headers: { 'X-Tenant-Id': tenant },
    data: {
      loginIdentifier: username,
      password,
      clientApp,
    },
  });
  const body = await readApi(response);
  expect(response.ok(), body.text).toBeTruthy();
  const token = body.json?.token;
  expect(typeof token, body.text).toBe('string');
  return token as string;
}

async function walkInCustomerId(
  request: APIRequestContext,
  headers: Record<string, string>
): Promise<string> {
  const response = await request.get(`${API}/api/Customer/walk-in`, { headers });
  const body = await readApi(response);
  expect(response.ok(), body.text).toBeTruthy();
  const data = asRecord(body.json?.data);
  const customerId = data?.customerId;
  expect(typeof customerId, body.text).toBe('string');
  return customerId as string;
}

async function openShift(
  request: APIRequestContext,
  headers: Record<string, string>
): Promise<string> {
  const response = await request.post(`${API}/api/pos/shift/auto-open`, {
    headers,
    data: {},
  });
  const body = await readApi(response);
  expect(response.ok(), body.text).toBeTruthy();
  const data = asRecord(body.json?.data);
  const registerId = data?.cashRegisterId;
  expect(typeof registerId, body.text).toBe('string');
  return registerId as string;
}

async function sellableProduct(
  request: APIRequestContext,
  headers: Record<string, string>
): Promise<SaleSession['product']> {
  const response = await request.get(`${API}/api/pos/catalog`, { headers });
  const body = await readApi(response);
  expect(response.ok(), body.text).toBeTruthy();
  const data = asRecord(body.json?.data);
  const products = Array.isArray(data?.products) ? data.products : [];
  const product = products
    .map((row) => asRecord(row))
    .find((row) => {
      const price = typeof row?.price === 'number' ? row.price : 0;
      const stock = typeof row?.stockQuantity === 'number' ? row.stockQuantity : 0;
      return row?.imeiTracked !== true && row?.isTicket !== true && price >= 2 && stock > 0;
    });
  expect(product, 'catalog has no sellable product priced at least €2').toBeTruthy();
  const price = Math.round((product?.price as number) * 100) / 100;
  return { id: product?.id as string, name: String(product?.name ?? ''), price };
}

/** Independent cashier session on the seeded `dev` tenant. Each test calls this itself. */
export async function openSaleSession(request: APIRequestContext): Promise<SaleSession> {
  const token = await loginApi(request, CASHIER_USER, 'pos');
  const headers = authHeaders(token, DEV_TENANT);
  const [customerId, registerId, product] = await Promise.all([
    walkInCustomerId(request, headers),
    openShift(request, headers),
    sellableProduct(request, headers),
  ]);
  return { token, tenant: DEV_TENANT, registerId, customerId, product };
}

export async function closeShift(
  request: APIRequestContext,
  headers: Record<string, string>
): Promise<ApiResult> {
  const response = await request.post(`${API}/api/pos/shift/end`, {
    headers,
    data: { endBalance: 0, notes: 'e2e payment sale gate' },
  });
  return readApi(response);
}

export async function reopenShift(
  request: APIRequestContext,
  headers: Record<string, string>,
  registerId: string
): Promise<void> {
  const response = await request.post(`${API}/api/pos/shift/auto-open`, {
    headers,
    data: { cashRegisterId: registerId },
  });
  const body = await readApi(response);
  expect(response.ok(), body.text).toBeTruthy();
}

export async function createPayment(
  request: APIRequestContext,
  headers: Record<string, string>,
  data: Record<string, unknown>
): Promise<ApiResult> {
  const response = await request.post(`${API}/api/pos/payment`, { headers, data });
  return readApi(response);
}

export function cashSaleBody(session: SaleSession, tableNumber: number, idempotencyKey: string) {
  return {
    customerId: session.customerId,
    cashRegisterId: session.registerId,
    tableNumber,
    totalAmount: session.product.price,
    cashGiven: session.product.price,
    idempotencyKey,
    items: [{ productId: session.product.id, quantity: 1, taxType: 'standard' }],
    payment: { method: 'cash', tseRequired: true, amount: session.product.price },
  };
}

export function receiptNumber(body: Record<string, unknown> | null): string {
  const tse = asRecord(body?.tse);
  const payment = asRecord(body?.payment);
  const value = tse?.receiptNumber ?? payment?.receiptNumber;
  return typeof value === 'string' ? value : '';
}

export function paymentIdOf(body: Record<string, unknown> | null): string {
  const payment = asRecord(body?.payment);
  const value = body?.paymentId ?? payment?.id;
  return typeof value === 'string' ? value : '';
}

export function qrPayload(body: Record<string, unknown> | null): string {
  const tse = asRecord(body?.tse);
  return typeof tse?.qrPayload === 'string' ? tse.qrPayload : '';
}
