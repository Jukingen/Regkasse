#!/usr/bin/env node
/**
 * Git pre-commit gate (Husky → `.husky/pre-commit`).
 *
 * Default (fast):
 *   1. API client verify (`--openapi-only`, or full if swagger/generated staged)
 *   2. lint / typecheck only for packages with staged files
 *   3. tests skipped (slow) unless HUSKY_RUN_TESTS=1
 *
 * Escape hatches:
 *   HUSKY=0                         — disable husky entirely
 *   SKIP_PRECOMMIT=1                — skip this whole hook
 *   SKIP_API_CLIENT_VERIFY=1        — skip OpenAPI/Orval check
 *   SKIP_PRECOMMIT_LINT=1           — skip lint
 *   SKIP_PRECOMMIT_TYPECHECK=1      — skip typecheck
 *   HUSKY_RUN_TESTS=1               — run fast contract tests for staged FE packages
 *   HUSKY_FULL_VERIFY=1             — always run full verify-api-client (Orval)
 *
 * Install: npm run prepare / npm run install:git-hooks
 */
import { execSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '../..');

function run(cmd, opts = {}) {
  execSync(cmd, {
    cwd: root,
    stdio: 'inherit',
    env: { ...process.env, ...opts.env },
    ...opts,
  });
}

function stagedFiles() {
  try {
    const out = execSync('git diff --cached --name-only --diff-filter=ACMR', {
      cwd: root,
      encoding: 'utf8',
    });
    return out
      .split(/\r?\n/)
      .map((s) => s.trim())
      .filter(Boolean);
  } catch {
    return [];
  }
}

/** @param {string[]} files */
function packagesTouched(files) {
  const set = new Set();
  for (const f of files) {
    if (f.startsWith('frontend-admin/')) set.add('admin');
    else if (f.startsWith('frontend-sites/')) set.add('sites');
    else if (f.startsWith('frontend/')) set.add('pos');
    else if (f.startsWith('backend/') || f.startsWith('tools/LicenseGenerator')) set.add('backend');
    else if (f.startsWith('localization/')) set.add('localization');
    else if (
      f.startsWith('scripts/') ||
      f === 'package.json' ||
      f === 'testsprite.config.json' ||
      f.startsWith('testsprite/')
    ) {
      set.add('root');
    }
  }
  return set;
}

function needsFullApiVerify(files) {
  if (process.env.HUSKY_FULL_VERIFY === '1') return true;
  return files.some(
    (f) =>
      f === 'backend/swagger.json' ||
      f.startsWith('frontend-admin/src/api/generated/') ||
      f === 'frontend-admin/orval.config.ts',
  );
}

function main() {
  if (process.env.SKIP_PRECOMMIT === '1' || process.env.HUSKY === '0') {
    console.log('pre-commit: SKIP_PRECOMMIT/HUSKY=0 — skipping.');
    process.exit(0);
  }

  const files = stagedFiles();
  const touched = packagesTouched(files);
  console.log(
    `pre-commit: ${files.length} staged file(s); packages: ${
      touched.size ? [...touched].join(', ') : '(none / docs-only)'
    }`,
  );

  // --- 1) API client sync ---
  if (process.env.SKIP_API_CLIENT_VERIFY !== '1') {
    const orvalReady = existsSync(join(root, 'frontend-admin', 'node_modules', 'orval'));
    if (!orvalReady) {
      console.warn(
        'pre-commit: Orval not installed in frontend-admin — skipping API client verify.',
      );
    } else {
      const full = needsFullApiVerify(files);
      const cmd = full
        ? 'node scripts/verify-api-client.mjs'
        : 'node scripts/verify-api-client.mjs --openapi-only';
      console.log(`pre-commit: ${cmd}`);
      try {
        run(cmd, { env: { CI: 'true' } });
      } catch {
        console.error('\npre-commit blocked: API client / OpenAPI check failed.');
        console.error('Fix: cd frontend-admin && npm run generate:api');
        console.error('      git add frontend-admin/src/api/generated');
        console.error('Or skip: SKIP_API_CLIENT_VERIFY=1 git commit …');
        process.exit(1);
      }
    }
  } else {
    console.log('pre-commit: SKIP_API_CLIENT_VERIFY=1');
  }

  // --- 2) Lint (package-scoped for speed) ---
  if (process.env.SKIP_PRECOMMIT_LINT !== '1' && touched.size > 0) {
    try {
      if (touched.has('admin')) {
        console.log('pre-commit: lint frontend-admin …');
        run('npm run lint -w registrierkasse-admin');
      }
      if (touched.has('pos')) {
        console.log('pre-commit: lint frontend (POS) …');
        run('npm run lint -w cash-register');
      }
      if (touched.has('sites')) {
        console.log('pre-commit: lint frontend-sites …');
        run('npm run lint -w regkasse-sites');
      }
      // Backend "lint" is a full build — only when backend sources staged
      if (touched.has('backend')) {
        console.log('pre-commit: backend build (lint) …');
        run('npm run lint -w @regkasse/backend');
      }
    } catch {
      console.error('\npre-commit blocked: lint failed.');
      console.error('Skip: SKIP_PRECOMMIT_LINT=1 git commit …');
      process.exit(1);
    }
  } else if (process.env.SKIP_PRECOMMIT_LINT === '1') {
    console.log('pre-commit: SKIP_PRECOMMIT_LINT=1');
  }

  // --- 3) Typecheck (TS packages only) ---
  if (process.env.SKIP_PRECOMMIT_TYPECHECK !== '1') {
    try {
      if (touched.has('admin')) {
        console.log('pre-commit: typecheck frontend-admin …');
        run('npm run typecheck -w registrierkasse-admin');
      }
      if (touched.has('pos')) {
        console.log('pre-commit: typecheck frontend (POS) …');
        run('npm run typecheck -w cash-register');
      }
      if (touched.has('sites')) {
        console.log('pre-commit: typecheck frontend-sites …');
        run('npm run typecheck -w regkasse-sites');
      }
    } catch {
      console.error('\npre-commit blocked: typecheck failed.');
      console.error('Skip: SKIP_PRECOMMIT_TYPECHECK=1 git commit …');
      process.exit(1);
    }
  } else {
    console.log('pre-commit: SKIP_PRECOMMIT_TYPECHECK=1');
  }

  // --- 4) Tests (opt-in; keep default pre-commit fast) ---
  if (process.env.HUSKY_RUN_TESTS === '1') {
    try {
      if (touched.has('admin')) {
        console.log('pre-commit: fast contract tests (admin) …');
        run('npm run test:contract -w registrierkasse-admin');
      }
      if (touched.has('pos')) {
        console.log('pre-commit: fast contract tests (POS) …');
        run('npm run test:contract -w cash-register');
      }
    } catch {
      console.error('\npre-commit blocked: tests failed.');
      process.exit(1);
    }
  } else {
    console.log('pre-commit: tests skipped (set HUSKY_RUN_TESTS=1 to enable fast contract tests).');
  }

  console.log('pre-commit: OK');
}

main();
