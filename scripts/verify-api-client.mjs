#!/usr/bin/env node
/**
 * Verifies Orval-generated admin client matches committed backend/swagger.json.
 *
 * Run from repository root:
 *   node scripts/verify-api-client.mjs
 *   node scripts/verify-api-client.mjs --openapi-only   # critical paths + config only (no Orval)
 *
 * Frontend-admin convenience:
 *   cd frontend-admin && npm run verify:api-client
 *
 * CI: .github/workflows/api-client-alignment.yml (verify)
 * CI auto-commit: .github/workflows/api-client-auto-generate.yml
 * Pre-commit: Husky → scripts/git-hooks/pre-commit.mjs (npm run prepare)
 */
import { execSync } from 'node:child_process';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, relative } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');
const swaggerPath = join(root, 'backend', 'swagger.json');
const orvalConfigPath = join(root, 'frontend-admin', 'orval.config.ts');
const generatedRel = 'frontend-admin/src/api/generated';
const expectedOrvalTarget = '../backend/swagger.json';

if (process.argv.includes('--help') || process.argv.includes('-h')) {
  console.log(`Usage: node scripts/verify-api-client.mjs [--openapi-only]

Verifies Orval-generated admin client matches committed backend/swagger.json.
  --openapi-only   Critical paths + orval config only (no Orval regenerate)
`);
  process.exit(0);
}

const openapiOnly = process.argv.includes('--openapi-only');

function run(cmd, opts = {}) {
  return execSync(cmd, {
    cwd: root,
    encoding: 'utf8',
    stdio: opts.stdio ?? 'pipe',
    env: { ...process.env, ...opts.env },
    ...opts,
  });
}

function assertOrvalConfigPointsAtSwagger() {
  if (!existsSync(orvalConfigPath)) {
    console.error(`Missing ${orvalConfigPath}`);
    process.exit(1);
  }
  const source = readFileSync(orvalConfigPath, 'utf8');
  // Accept single or double quotes around the swagger path.
  const hit =
    source.includes(`target: '${expectedOrvalTarget}'`) ||
    source.includes(`target: "${expectedOrvalTarget}"`);
  if (!hit) {
    console.error(
      `frontend-admin/orval.config.ts must set input.target to '${expectedOrvalTarget}'.`,
    );
    console.error(`Open the file and fix the Orval input before regenerating the client.`);
    process.exit(1);
  }
}

/**
 * Fail on modified, deleted, or untracked files under the generated tree.
 * Plain `git diff` alone misses brand-new Orval files (untracked).
 */
function assertGeneratedTreeClean() {
  const porcelain = run(`git status --porcelain -- ${generatedRel}/`, {
    stdio: 'pipe',
  }).trim();

  if (!porcelain) {
    // Also catch content drift on tracked files when status is clean but
    // index/worktree comparison is needed after regenerate in the same process.
    try {
      run(
        `git -c core.safecrlf=false diff --exit-code --ignore-cr-at-eol -- ${generatedRel}/`,
        { stdio: 'pipe' },
      );
    } catch {
      printDriftHelp();
      try {
        run(`git diff --stat --ignore-cr-at-eol -- ${generatedRel}/`, { stdio: 'inherit' });
      } catch {
        /* ignore */
      }
      process.exit(1);
    }
    return;
  }

  printDriftHelp();
  console.error('\nWorking tree changes under generated client:');
  console.error(porcelain);
  try {
    run(`git diff --stat --ignore-cr-at-eol -- ${generatedRel}/`, { stdio: 'inherit' });
  } catch {
    /* ignore */
  }
  process.exit(1);
}

function printDriftHelp() {
  console.error('\n::error::Orval output is out of sync with committed OpenAPI.');
  console.error('Fix (repo root):');
  console.error('  1. node scripts/generate-backend-openapi.mjs   # if API routes/DTOs changed');
  console.error('  2. cd frontend-admin && npm run generate:api');
  console.error('  3. git add backend/swagger.json frontend-admin/src/api/generated');
  console.error('  4. node scripts/verify-api-client.mjs\n');
}

function main() {
  if (!existsSync(swaggerPath)) {
    console.error(`Missing ${swaggerPath}`);
    console.error('Generate it with: node scripts/generate-backend-openapi.mjs');
    process.exit(1);
  }

  assertOrvalConfigPointsAtSwagger();
  console.log(`OK: orval input → ${expectedOrvalTarget}`);

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

  if (openapiOnly) {
    console.log('OK: --openapi-only (skipped Orval regenerate / drift check).');
    return;
  }

  console.log('Running Orval (npm run generate:api)…');
  const adminPkg = join(root, 'frontend-admin');
  if (!existsSync(join(adminPkg, 'node_modules', 'orval'))) {
    console.error(`Missing frontend-admin/node_modules/orval.`);
    console.error('Install first: npm ci --prefix frontend-admin  (or: npm install -w registrierkasse-admin)');
    process.exit(1);
  }
  try {
    execSync('npm run generate:api', {
      cwd: adminPkg,
      stdio: 'inherit',
      env: { ...process.env, CI: 'true' },
    });
  } catch {
    console.error('\n::error::npm run generate:api failed.');
    process.exit(1);
  }

  assertGeneratedTreeClean();
  console.log(
    `OK: generated client matches ${relative(root, swaggerPath).replaceAll('\\', '/')}.`,
  );
}

main();
