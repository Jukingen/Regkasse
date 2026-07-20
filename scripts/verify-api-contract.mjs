#!/usr/bin/env node
/**
 * Lightweight API contract smoke check against committed backend/swagger.json.
 * Run from repository root: node scripts/verify-api-contract.mjs
 *
 * For the fuller critical-path + schema suite, use:
 *   node scripts/validate-critical-openapi-paths.mjs
 */
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');
const swaggerPath = join(root, 'backend', 'swagger.json');

/** @type {{ path: string, methods: string[] }[]} */
const ENDPOINTS = [
  { path: '/api/Auth/login', methods: ['post'] },
  { path: '/api/admin/tenants', methods: ['get'] },
  { path: '/api/rksv/status', methods: ['get'] },
  { path: '/api/license/status', methods: ['get'] },
];

/**
 * @param {unknown} doc
 * @returns {{ passed: number, failed: number, errors: string[] }}
 */
function verifyContract(doc) {
  /** @type {string[]} */
  const errors = [];
  let passed = 0;
  let failed = 0;

  if (!doc || typeof doc !== 'object') {
    return { passed: 0, failed: 1, errors: ['OpenAPI document must be a JSON object'] };
  }

  const o = /** @type {Record<string, unknown>} */ (doc);
  const openapi = o.openapi;
  if (typeof openapi !== 'string' || !openapi.startsWith('3.')) {
    errors.push(`Expected openapi 3.x, got: ${String(openapi)}`);
    failed++;
  } else {
    passed++;
    console.log(`OK: openapi ${openapi}`);
  }

  const paths = o.paths;
  if (!paths || typeof paths !== 'object') {
    errors.push('Missing or invalid "paths"');
    return { passed, failed: failed + 1, errors };
  }

  const pathMap = /** @type {Record<string, unknown>} */ (paths);

  for (const { path, methods } of ENDPOINTS) {
    const entry = pathMap[path];
    if (!entry || typeof entry !== 'object') {
      console.log(`FAIL: ${path} — not found`);
      errors.push(`Missing path: ${path}`);
      failed++;
      continue;
    }

    const ops = /** @type {Record<string, unknown>} */ (entry);
    const missingMethods = methods.filter((m) => !(m in ops));
    if (missingMethods.length > 0) {
      const detail = missingMethods.map((m) => m.toUpperCase()).join(', ');
      console.log(`FAIL: ${path} — missing method(s): ${detail}`);
      errors.push(`Path ${path} missing HTTP method(s): ${detail}`);
      failed++;
      continue;
    }

    console.log(`OK: ${path} [${methods.map((m) => m.toUpperCase()).join(', ')}]`);
    passed++;
  }

  return { passed, failed, errors };
}

function main() {
  console.log('Verifying API contract…');

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

  const { passed, failed, errors } = verifyContract(doc);

  console.log(`\nSummary: ${passed} passed, ${failed} failed`);

  if (failed > 0) {
    for (const err of errors) {
      console.error(`  - ${err}`);
    }
    process.exit(1);
  }
}

main();
