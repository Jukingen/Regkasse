#!/usr/bin/env node
/**
 * Compares backend/swagger.json against a baseline (backend/swagger-old.json)
 * for backward-incompatible OpenAPI changes.
 *
 * Baseline example (repo root):
 *   git show origin/main:backend/swagger.json > backend/swagger-old.json
 *
 * Run: node scripts/validate-api-contract.mjs
 */
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');
const swaggerOldPath = join(root, 'backend', 'swagger-old.json');
const swaggerNewPath = join(root, 'backend', 'swagger.json');

if (!existsSync(swaggerOldPath)) {
  console.error(`Missing baseline OpenAPI: ${swaggerOldPath}`);
  console.error(
    'Create it from the base branch, e.g.: git show origin/main:backend/swagger.json > backend/swagger-old.json'
  );
  process.exit(1);
}
if (!existsSync(swaggerNewPath)) {
  console.error(`Missing OpenAPI: ${swaggerNewPath}`);
  process.exit(1);
}

const swaggerOld = JSON.parse(readFileSync(swaggerOldPath, 'utf8'));
const swaggerNew = JSON.parse(readFileSync(swaggerNewPath, 'utf8'));

const breakingChanges = [];

const oldPaths = swaggerOld.paths && typeof swaggerOld.paths === 'object' ? swaggerOld.paths : {};
const newPaths = swaggerNew.paths && typeof swaggerNew.paths === 'object' ? swaggerNew.paths : {};

// Check for removed endpoints
Object.keys(oldPaths).forEach((path) => {
  if (!newPaths[path]) {
    breakingChanges.push(`REMOVED: ${path}`);
  }
});

// Check for removed required fields
Object.keys(newPaths).forEach((path) => {
  const methods = ['post', 'put', 'patch'];
  methods.forEach((method) => {
    const oldBody = oldPaths[path]?.[method]?.requestBody;
    const newBody = newPaths[path]?.[method]?.requestBody;

    if (oldBody && newBody) {
      const oldRequired = oldBody.content?.['application/json']?.schema?.required || [];
      const newRequired = newBody.content?.['application/json']?.schema?.required || [];

      const removedRequired = oldRequired.filter((f) => !newRequired.includes(f));
      if (removedRequired.length) {
        breakingChanges.push(
          `REMOVED REQUIRED FIELD in ${method.toUpperCase()} ${path}: ${removedRequired.join(', ')}`
        );
      }
    }
  });
});

if (breakingChanges.length) {
  console.error('BREAKING API CHANGES DETECTED:');
  breakingChanges.forEach((c) => console.error(`  - ${c}`));
  process.exit(1);
}

console.log('✅ API contract is backward compatible');
