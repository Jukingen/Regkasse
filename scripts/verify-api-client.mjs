#!/usr/bin/env node
/**
 * Verifies Orval-generated admin client matches committed backend/swagger.json.
 * Run from repository root: node scripts/verify-api-client.mjs
 */
import { execSync } from 'node:child_process';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');
const swaggerPath = join(root, 'backend', 'swagger.json');
const generatedGlob = 'frontend-admin/src/api/generated';

function main() {
  if (!existsSync(swaggerPath)) {
    console.error(`Missing ${swaggerPath}`);
    process.exit(1);
  }

  console.log('Validating critical OpenAPI paths and schemas…');
  execSync('node scripts/validate-critical-openapi-paths.mjs', {
    cwd: root,
    stdio: 'inherit',
  });

  try {
    JSON.parse(readFileSync(swaggerPath, 'utf8'));
  } catch (e) {
    console.error('backend/swagger.json is not valid JSON:', e.message);
    process.exit(1);
  }

  console.log('Running Orval (npm run generate:api)…');
  execSync('npm run generate:api', {
    cwd: join(root, 'frontend-admin'),
    stdio: 'inherit',
    env: { ...process.env, CI: 'true' },
  });

  try {
    execSync(`git diff --exit-code -- ${generatedGlob}/`, {
      cwd: root,
      stdio: 'pipe',
    });
  } catch {
    console.error('\n::error::Orval output is out of sync with committed OpenAPI.');
    console.error('Fix: cd frontend-admin && npm run generate:api && git add src/api/generated && git commit\n');
    try {
      execSync(`git diff --stat -- ${generatedGlob}/`, { cwd: root, stdio: 'inherit' });
    } catch {
      /* ignore */
    }
    process.exit(1);
  }

  console.log('OK: generated client matches backend/swagger.json.');
}

main();
