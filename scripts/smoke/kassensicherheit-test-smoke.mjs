#!/usr/bin/env node
/**
 * Staging-only fiskaly SIGN DE TEST smoke.
 * Default is dry-run. Real HTTP requires --confirm and KASSENSICHERHEIT_SMOKE_ALLOW=1.
 * Never prints API key, secret, PIN, bearer, or raw response bodies.
 */

const SIGN_DE_BASE = 'https://kassensichv-middleware.fiskaly.com/api/v2';
const DSFINVK_BASE = 'https://dsfinvk.fiskaly.com/api/v1';

const KEY_NAMES = 'KassenSicherheit__ApiKey, __ApiSecret, __AdminPin';

function readEnv() {
  return {
    apiKey: process.env.KassenSicherheit__ApiKey ?? '',
    apiSecret: process.env.KassenSicherheit__ApiSecret ?? '',
    adminPin: process.env.KassenSicherheit__AdminPin ?? '',
    allow: process.env.KASSENSICHERHEIT_SMOKE_ALLOW ?? '',
  };
}

function printPlan() {
  console.log('dry-run: no HTTP');
  console.log(`a. POST ${SIGN_DE_BASE}/auth`);
  console.log(`b. POST ${SIGN_DE_BASE}/tss`);
  console.log('c. PATCH /tss/{id} state=INITIALIZED');
  console.log('d. POST /tss/{id}/client');
  console.log('e. POST /tss/{id}/tx state=ACTIVE');
  console.log('f. PATCH /tss/{id}/tx/{txId} state=FINISHED');
  console.log(`g. PUT ${DSFINVK_BASE}/exports/{exportId} format=tar`);
}

function providerCode(payload) {
  if (!payload || typeof payload !== 'object') return '';
  if (typeof payload.code === 'string') return payload.code;
  if (payload.error && typeof payload.error.code === 'string') return payload.error.code;
  return '';
}

async function request(method, url, body, token) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = `Bearer ${token}`;
  const response = await fetch(url, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  let payload = null;
  if (text) {
    try {
      payload = JSON.parse(text);
    } catch {
      payload = null;
    }
  }
  if (!response.ok) {
    const code = providerCode(payload);
    console.error(`http_status=${response.status}`);
    console.error(`provider_code=${code}`);
    process.exit(1);
  }
  return payload && typeof payload === 'object' ? payload : {};
}

async function auth(env) {
  const payload = await request('POST', `${SIGN_DE_BASE}/auth`, {
    api_key: env.apiKey,
    api_secret: env.apiSecret,
  });
  const token = typeof payload.access_token === 'string' ? payload.access_token : '';
  if (!token) {
    console.error('http_status=200');
    console.error('provider_code=');
    process.exit(1);
  }
  return token;
}

async function createTss(token) {
  const payload = await request('POST', `${SIGN_DE_BASE}/tss`, { description: 'regkasse-staging-smoke' }, token);
  const id = payload._id || payload.tss_id || payload.id;
  if (typeof id !== 'string' || !id) {
    console.error('http_status=200');
    console.error('provider_code=');
    process.exit(1);
  }
  return id;
}

async function initializeTss(token, id, adminPin) {
  await request(
    'PATCH',
    `${SIGN_DE_BASE}/tss/${encodeURIComponent(id)}`,
    { state: 'INITIALIZED', admin_pin: adminPin },
    token,
  );
}

async function createClient(token, tssId) {
  const clientId = crypto.randomUUID();
  const serialNumber = crypto.randomUUID();
  await request(
    'POST',
    `${SIGN_DE_BASE}/tss/${encodeURIComponent(tssId)}/client`,
    { serial_number: serialNumber, client_id: clientId },
    token,
  );
  return clientId;
}

async function startTx(token, tssId, clientId) {
  const payload = await request(
    'POST',
    `${SIGN_DE_BASE}/tss/${encodeURIComponent(tssId)}/tx`,
    { state: 'ACTIVE', client_id: clientId },
    token,
  );
  const txId = payload._id || payload.tx_id || payload.id;
  if (typeof txId !== 'string' || !txId) {
    console.error('http_status=200');
    console.error('provider_code=');
    process.exit(1);
  }
  return txId;
}

async function finishTx(token, tssId, txId, clientId) {
  await request(
    'PATCH',
    `${SIGN_DE_BASE}/tss/${encodeURIComponent(tssId)}/tx/${encodeURIComponent(txId)}`,
    { state: 'FINISHED', client_id: clientId },
    token,
  );
}

async function exportDsfinvk(token) {
  const exportId = crypto.randomUUID();
  const endDate = Math.floor(Date.now() / 1000);
  const startDate = endDate - 86400;
  const payload = await request(
    'PUT',
    `${DSFINVK_BASE}/exports/${encodeURIComponent(exportId)}`,
    { start_date: startDate, end_date: endDate, format: 'tar' },
    token,
  );
  return typeof payload.state === 'string' ? payload.state : '';
}

async function runLifecycle(env) {
  const token = await auth(env);
  const tssId = await createTss(token);
  await initializeTss(token, tssId, env.adminPin);
  const clientId = await createClient(token, tssId);
  const txId = await startTx(token, tssId, clientId);
  await finishTx(token, tssId, txId, clientId);
  const exportState = await exportDsfinvk(token);
  console.log(`tss_id=${tssId}`);
  console.log(`client_id=${clientId}`);
  console.log(`tx_id=${txId}`);
  console.log(`export_state=${exportState}`);
}

function main() {
  const argv = process.argv.slice(2);
  const confirm = argv.includes('--confirm');
  const dryRun = argv.includes('--dry-run') || !confirm;
  if (dryRun) {
    printPlan();
    process.exit(0);
  }

  const env = readEnv();
  if (!env.apiKey || !env.apiSecret || !env.adminPin) {
    console.error(`missing KassenSicherheit credentials (${KEY_NAMES})`);
    process.exit(1);
  }
  if (env.allow !== '1') {
    console.error('set KASSENSICHERHEIT_SMOKE_ALLOW=1 to run');
    process.exit(1);
  }
  return runLifecycle(env);
}

main().catch((error) => {
  const message = error instanceof Error ? error.message : 'request failed';
  if (!/key|secret|pin|bearer|token/i.test(message)) {
    console.error(message);
  } else {
    console.error('request failed');
  }
  process.exit(1);
});
