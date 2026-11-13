#!/usr/bin/env node
/**
 * Manual Storecove TEST smoke for the Peppol canary (Paket 22-b-4).
 * Default is dry-run. It prints the five-step plan and does not call Storecove.
 * Real HTTP requires --confirm, PEPPOL_SMOKE_ALLOW=1, and Peppol__Storecove__ApiKey.
 * Never prints the API key, the UBL document, or a response body.
 * This script does not claim Peppol or EN 16931 compliance.
 */

const DEFAULT_BASE = 'https://api.storecove.com/api/v2';

function readEnv() {
  return {
    apiKey: process.env.Peppol__Storecove__ApiKey ?? '',
    allow: process.env.PEPPOL_SMOKE_ALLOW ?? '',
    baseUrl: (process.env.Peppol__Storecove__BaseUrl || DEFAULT_BASE).replace(/\/+$/, ''),
    legalEntityId: process.env.PEPPOL_SMOKE_LEGAL_ENTITY_ID ?? '',
    scheme: process.env.PEPPOL_SMOKE_SCHEME ?? '',
    identifier: process.env.PEPPOL_SMOKE_VALUE ?? '',
  };
}

function printPlan(baseUrl) {
  console.log('dry-run: no HTTP');
  console.log('1. Canary gate: one tenant, Provider=storecove, Environment=TEST');
  console.log(`2. POST ${baseUrl}/document_submissions`);
  console.log('3. Store guid as provider_message_id (einvoice_submissions status=Sent)');
  console.log(`4. GET ${baseUrl}/document_submissions/{guid}`);
  console.log('5. state=DELIVERED sets einvoice_submissions status=Ack');
}

function isToken(value) {
  return typeof value === 'string' && value.length > 0 && value.length <= 80 && !/[\s<]/.test(value);
}

function providerCode(payload) {
  if (!payload || typeof payload !== 'object') return '';
  if (isToken(payload.code)) return payload.code;
  if (payload.error && isToken(payload.error.code)) return payload.error.code;
  if (isToken(payload.error)) return payload.error;
  return '';
}

function failHttp(status, payload) {
  console.error(`http_status=${status}`);
  console.error(`provider_code=${providerCode(payload)}`);
  process.exit(1);
}

async function request(method, url, body, apiKey) {
  const headers = { Accept: 'application/json', Authorization: `Bearer ${apiKey}` };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
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
  if (!response.ok) failHttp(response.status, payload);
  return payload && typeof payload === 'object' ? payload : {};
}

function submissionBody(env) {
  const ubl = '<Invoice/>';
  return {
    legalEntityId: env.legalEntityId,
    idempotencyGuid: crypto.randomUUID(),
    routing: {
      eIdentifiers: [{ scheme: env.scheme, id: env.identifier }],
    },
    document: {
      documentType: 'invoice',
      rawDocumentData: {
        document: Buffer.from(ubl, 'utf8').toString('base64'),
        parse: false,
      },
    },
  };
}

async function runLifecycle(env) {
  const created = await request(
    'POST',
    `${env.baseUrl}/document_submissions`,
    submissionBody(env),
    env.apiKey,
  );
  const guid = typeof created.guid === 'string' ? created.guid : '';
  if (!isToken(guid)) failHttp(200, null);

  const status = await request(
    'GET',
    `${env.baseUrl}/document_submissions/${encodeURIComponent(guid)}`,
    undefined,
    env.apiKey,
  );
  const state = typeof status.state === 'string' ? status.state : '';
  const echoed = typeof status.guid === 'string' ? status.guid : '';
  const submissionStatus = state === 'DELIVERED' && echoed === guid ? 'Ack' : 'Sent';
  console.log(`provider_message_id=${guid}`);
  console.log(`einvoice_submissions_status=${submissionStatus}`);
}

function main() {
  const argv = process.argv.slice(2);
  const confirm = argv.includes('--confirm');
  const dryRun = argv.includes('--dry-run') || !confirm;
  const env = readEnv();
  if (dryRun) {
    printPlan(env.baseUrl);
    process.exit(0);
  }

  if (!env.apiKey) {
    console.error('missing Peppol__Storecove__ApiKey');
    process.exit(1);
  }
  if (env.allow !== '1') {
    console.error('set PEPPOL_SMOKE_ALLOW=1 to run');
    process.exit(1);
  }
  if (!env.legalEntityId || !env.scheme || !env.identifier) {
    console.error('missing PEPPOL_SMOKE_LEGAL_ENTITY_ID, PEPPOL_SMOKE_SCHEME, PEPPOL_SMOKE_VALUE');
    process.exit(1);
  }
  return runLifecycle(env);
}

main().catch((error) => {
  const message = error instanceof Error ? error.message : 'request failed';
  if (!/key|bearer|authorization|invoice|ubl/i.test(message)) {
    console.error(message);
  } else {
    console.error('request failed');
  }
  process.exit(1);
});
