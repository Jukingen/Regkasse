#!/usr/bin/env node
/**
 * Optional git pre-commit gate for OpenAPI / Orval drift.
 * Invoked by scripts/git-hooks/pre-commit after install-git-hooks.mjs.
 *
 * Only runs the full Orval verify when staged paths touch the contract surface:
 *   - backend/swagger.json
 *   - frontend-admin/orval.config.ts
 *   - frontend-admin/scripts/orval-strip-legacy-paths.cjs
 *   - frontend-admin/src/api/generated/**
 */
import { execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '../..');

const TRIGGER_PREFIXES = [
  'backend/swagger.json',
  'frontend-admin/orval.config.ts',
  'frontend-admin/scripts/orval-strip-legacy-paths.cjs',
  'frontend-admin/src/api/generated/',
];

function stagedFiles() {
  const out = execSync('git diff --cached --name-only --diff-filter=ACMR', {
    cwd: root,
    encoding: 'utf8',
  });
  return out
    .split(/\r?\n/)
    .map((line) => line.trim().replaceAll('\\', '/'))
    .filter(Boolean);
}

function shouldVerify(files) {
  return files.some((file) =>
    TRIGGER_PREFIXES.some((prefix) => file === prefix || file.startsWith(prefix)),
  );
}

function main() {
  const files = stagedFiles();
  if (!shouldVerify(files)) {
    process.exit(0);
  }

  console.log('pre-commit: OpenAPI / Orval paths staged — running verify-api-client…');
  try {
    execSync('node scripts/verify-api-client.mjs', {
      cwd: root,
      stdio: 'inherit',
      env: { ...process.env, CI: 'true' },
    });
  } catch {
    console.error('\npre-commit blocked: API client is out of sync with backend/swagger.json.');
    console.error('Regenerate and re-stage before committing (see verify-api-client output).');
    process.exit(1);
  }
}

main();
