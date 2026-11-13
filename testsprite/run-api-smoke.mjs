#!/usr/bin/env node
/**
 * Live API smoke for TestSprite contract suites (no proprietary CLI).
 * Requires a running Development API (default http://localhost:5184).
 *
 * Usage:
 *   node testsprite/run-api-smoke.mjs
 *   TESTSPRITE_API_URL=http://localhost:5184 TESTSPRITE_LOGIN=admin@admin.com TESTSPRITE_PASSWORD=Admin123! node testsprite/run-api-smoke.mjs
 *   npm run testsprite:smoke
 *
 * Skips auth-dependent cases when login fails (exit 1). Health always runs.
 */
import { readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');
const configPath = join(root, 'testsprite.config.json');

const config = existsSync(configPath)
  ? JSON.parse(readFileSync(configPath, 'utf8'))
  : {};

const baseUrl = (
  process.env.TESTSPRITE_API_URL ||
  config.baseUrl?.api ||
  'http://localhost:5184'
).replace(/\/$/, '');

const loginId =
  process.env.TESTSPRITE_LOGIN ||
  config.auth?.devAdmin?.loginIdentifier ||
  'admin@admin.com';
const password =
  process.env.TESTSPRITE_PASSWORD ||
  config.auth?.devAdmin?.password ||
  'Admin123!';
const tenantSlug =
  process.env.TESTSPRITE_TENANT ||
  config.testData?.tenantSlug ||
  'dev';

/** @type {{ name: string, ok: boolean, detail?: string }[]} */
const results = [];

function record(name, ok, detail) {
  results.push({ name, ok, detail });
  const icon = ok ? 'PASS' : 'FAIL';
  console.log(`${icon}: ${name}${detail ? ` — ${detail}` : ''}`);
}

async function api(method, path, { token, body, headersExtra, expectStatuses } = {}) {
  const url = `${baseUrl}${path.startsWith('/') ? path : `/${path}`}`;
  const headers = {
    Accept: 'application/json',
    'Content-Type': 'application/json',
    ...(tenantSlug ? { 'X-Tenant-Id': tenantSlug } : {}),
    ...headersExtra,
  };
  if (token) headers.Authorization = `Bearer ${token}`;

  let res;
  try {
    res = await fetch(url, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch (err) {
    throw new Error(`fetch failed: ${err instanceof Error ? err.message : err}`);
  }

  const text = await res.text();
  let parsed = null;
  if (text) {
    try {
      parsed = JSON.parse(text);
    } catch {
      parsed = text;
    }
  }

  const allowed = expectStatuses ?? [200];
  if (!allowed.includes(res.status)) {
    const snippet =
      typeof parsed === 'object' && parsed !== null
        ? JSON.stringify(parsed).slice(0, 240)
        : String(parsed).slice(0, 240);
    throw new Error(`${method} ${path} → ${res.status}: ${snippet}`);
  }
  return { status: res.status, body: parsed };
}

async function main() {
  console.log(`TestSprite API smoke → ${baseUrl} (tenant header: ${tenantSlug || 'none'})`);

  // --- Health (no auth) ---
  try {
    const { status, body } = await api('GET', '/api/health', { expectStatuses: [200, 503] });
    const st = body && typeof body === 'object' ? body.status : null;
    record(
      'GET /api/health',
      status === 200 && typeof st === 'string',
      `HTTP ${status}, status=${st}`,
    );
  } catch (err) {
    record('GET /api/health', false, err instanceof Error ? err.message : String(err));
    console.error('\nAPI unreachable. Start backend first, then re-run.');
    process.exit(1);
  }

  // --- Auth ---
  let token = '';
  try {
    const { body } = await api('POST', '/api/Auth/login', {
      body: { loginIdentifier: loginId, password, clientApp: 'admin' },
      expectStatuses: [200, 400, 401, 403],
    });
    token = body && typeof body === 'object' && body.token ? String(body.token) : '';
    record('POST /api/Auth/login (loginIdentifier)', Boolean(token), token ? 'token received' : 'no token');
  } catch (err) {
    record('POST /api/Auth/login (loginIdentifier)', false, err instanceof Error ? err.message : String(err));
  }

  try {
    const { status } = await api('POST', '/api/Auth/login', {
      body: { loginIdentifier: 'invalid@example.com', password: 'wrong', clientApp: 'admin' },
      expectStatuses: [400, 401],
    });
    record('POST /api/Auth/login invalid credentials → 401/400', status === 401 || status === 400, `HTTP ${status}`);
  } catch (err) {
    record('POST /api/Auth/login invalid credentials', false, err instanceof Error ? err.message : String(err));
  }

  if (!token) {
    console.error('\nLogin failed — remaining auth-gated smoke skipped.');
    failExit();
  }

  try {
    const { body } = await api('GET', '/api/Auth/me', { token, expectStatuses: [200] });
    record('GET /api/Auth/me', body && typeof body === 'object', 'ok');
  } catch (err) {
    record('GET /api/Auth/me', false, err instanceof Error ? err.message : String(err));
  }

  // --- Tenant (Dev header) ---
  try {
    const { body } = await api('GET', '/api/tenants/switcher', {
      expectStatuses: [200],
    });
    const ok = Array.isArray(body) || (body && typeof body === 'object');
    record('GET /api/tenants/switcher (Dev)', ok, Array.isArray(body) ? `len=${body.length}` : 'object');
  } catch (err) {
    record('GET /api/tenants/switcher', false, err instanceof Error ? err.message : String(err));
  }

  // --- Cross-tenant 404 ---
  try {
    const fakeId = '00000000-0000-4000-8000-000000000099';
    const { status } = await api('GET', `/api/admin/payments/${fakeId}`, {
      token,
      expectStatuses: [404, 400, 401, 403],
    });
    record(
      'GET /api/admin/payments/{missing} not 200 (prefer 404)',
      status === 404 || status === 400,
      `HTTP ${status}`,
    );
  } catch (err) {
    record('cross-tenant/missing payment', false, err instanceof Error ? err.message : String(err));
  }

  // --- Backup (Super Admin / settings.view) ---
  try {
    await api('GET', '/api/admin/backup/status/latest', { token, expectStatuses: [200] });
    record('GET /api/admin/backup/status/latest', true);
  } catch (err) {
    record('GET /api/admin/backup/status/latest', false, err instanceof Error ? err.message : String(err));
  }

  try {
    await api('GET', '/api/admin/backup/settings', { token, expectStatuses: [200] });
    record('GET /api/admin/backup/settings', true);
  } catch (err) {
    record('GET /api/admin/backup/settings', false, err instanceof Error ? err.message : String(err));
  }

  try {
    await api('GET', '/api/admin/backup/compliance-status', { token, expectStatuses: [200] });
    record('GET /api/admin/backup/compliance-status', true);
  } catch (err) {
    record('GET /api/admin/backup/compliance-status', false, err instanceof Error ? err.message : String(err));
  }

  try {
    await api('GET', '/api/admin/backup/storage-costs', { token, expectStatuses: [200] });
    record('GET /api/admin/backup/storage-costs', true);
  } catch (err) {
    record('GET /api/admin/backup/storage-costs', false, err instanceof Error ? err.message : String(err));
  }

  // --- Offline orders list (admin) ---
  try {
    await api('GET', '/api/admin/offline-orders?page=1&pageSize=5', {
      token,
      expectStatuses: [200],
    });
    record('GET /api/admin/offline-orders', true);
  } catch (err) {
    record('GET /api/admin/offline-orders', false, err instanceof Error ? err.message : String(err));
  }

  // --- Sites / working hours (public; no auth) ---
  try {
    const { status } = await api('GET', `/api/sites/${tenantSlug}/status`, {
      expectStatuses: [200, 404],
    });
    record(
      `GET /api/sites/{slug}/status`,
      status === 200 || status === 404,
      `HTTP ${status} (404 ok if slug missing)`,
    );
  } catch (err) {
    record('GET /api/sites/.../status', false, err instanceof Error ? err.message : String(err));
  }

  failExit();
}

function failExit() {
  const failed = results.filter((r) => !r.ok);
  console.log(`\nSummary: ${results.length - failed.length}/${results.length} passed`);
  process.exit(failed.length ? 1 : 0);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
