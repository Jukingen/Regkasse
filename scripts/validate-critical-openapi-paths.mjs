#!/usr/bin/env node
/**
 * Validates backend/swagger.json: OpenAPI 3.x shape, critical Admin/POS routes, and required component schemas.
 * Run from repository root: node scripts/validate-critical-openapi-paths.mjs
 *
 * Regenerate the committed contract (no manual JSON edits): node scripts/generate-backend-openapi.mjs
 */
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');
const swaggerPath = join(root, 'backend', 'swagger.json');

/** @type {{ path: string, methods: string[] }[]} */
const CRITICAL_PATHS = [
  { path: '/api/pos/payment', methods: ['post'] },
  { path: '/api/pos/payment/methods', methods: ['get'] },
  { path: '/api/pos/payment/{id}', methods: ['get'] },
  { path: '/api/pos/cart/current', methods: ['get'] },
  { path: '/api/admin/payments', methods: ['get'] },
  { path: '/api/admin/payments/{id}', methods: ['get'] },
  { path: '/api/admin/backup/status/latest', methods: ['get'] },
  { path: '/api/admin/restore-verification/runs/latest', methods: ['get'] },
  { path: '/api/admin/restore-verification/readiness', methods: ['get'] },
  { path: '/api/rksv/monatsbeleg/status-overview', methods: ['get'] },
  { path: '/api/rksv/reminder/status-overview', methods: ['get'] },
];

const REQUIRED_SCHEMAS = [
  'CreatePaymentRequest',
  'PaymentListResponse',
  'PaymentMethod',
  'BackupLatestStatusResponseDto',
  'RestoreVerificationRunResponseDto',
  'RestoreVerificationReadinessResponseDto',
  'MonatsbelegRegisterStatusItemDto',
  'RksvReminderRegisterStatusItemDto',
];

/**
 * @param {unknown} doc
 */
function validateCriticalOpenApiDocument(doc) {
  if (!doc || typeof doc !== 'object') {
    throw new Error('OpenAPI document must be a JSON object');
  }
  const o = /** @type {Record<string, unknown>} */ (doc);
  const openapi = o.openapi;
  if (typeof openapi !== 'string' || !openapi.startsWith('3.')) {
    throw new Error(`Expected openapi 3.x, got: ${String(openapi)}`);
  }
  const paths = o.paths;
  if (!paths || typeof paths !== 'object') {
    throw new Error('Missing or invalid "paths"');
  }
  const pathMap = /** @type {Record<string, unknown>} */ (paths);

  for (const { path, methods } of CRITICAL_PATHS) {
    const entry = pathMap[path];
    if (!entry || typeof entry !== 'object') {
      throw new Error(`Missing critical path: ${path}`);
    }
    const ops = /** @type {Record<string, unknown>} */ (entry);
    for (const m of methods) {
      if (!(m in ops)) {
        throw new Error(`Path ${path} missing HTTP method: ${m.toUpperCase()}`);
      }
    }
  }

  const components = o.components;
  if (!components || typeof components !== 'object') {
    throw new Error('Missing or invalid "components"');
  }
  const schemas = /** @type {Record<string, unknown>} */ (components).schemas;
  if (!schemas || typeof schemas !== 'object') {
    throw new Error('Missing components.schemas');
  }
  const schemaMap = /** @type {Record<string, unknown>} */ (schemas);
  for (const name of REQUIRED_SCHEMAS) {
    if (!(name in schemaMap)) {
      throw new Error(`Missing required schema: components.schemas.${name}`);
    }
  }
}

function main() {
  if (!existsSync(swaggerPath)) {
    console.error(`Missing ${swaggerPath}`);
    process.exit(1);
  }
  let doc;
  try {
    doc = JSON.parse(readFileSync(swaggerPath, 'utf8'));
  } catch (e) {
    console.error('backend/swagger.json is not valid JSON:', /** @type {Error} */ (e).message);
    process.exit(1);
  }
  try {
    validateCriticalOpenApiDocument(doc);
  } catch (e) {
    console.error('OpenAPI contract validation failed:', /** @type {Error} */ (e).message);
    process.exit(1);
  }
  console.log('OK: critical OpenAPI paths and payment-related schemas are present.');
}

main();
