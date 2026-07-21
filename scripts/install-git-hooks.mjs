#!/usr/bin/env node
/**
 * Installs local git hooks via Husky (points core.hooksPath at .husky/).
 *
 *   npm run prepare
 *   npm run install:git-hooks
 *
 * Pre-commit → scripts/git-hooks/pre-commit.mjs
 *   (API verify + staged-package lint/typecheck; tests opt-in)
 */
import { execSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');
const huskyDir = join(root, '.husky');
const huskyPreCommit = join(huskyDir, 'pre-commit');
const logic = join(root, 'scripts', 'git-hooks', 'pre-commit.mjs');

if (process.env.CI === 'true' || process.env.HUSKY === '0') {
  console.log('Skipping git hooks install (CI or HUSKY=0).');
  process.exit(0);
}
if (!existsSync(join(root, '.git'))) {
  console.log('Skipping git hooks install (no .git directory).');
  process.exit(0);
}

if (!existsSync(logic)) {
  console.error(`Missing ${logic}`);
  process.exit(1);
}

mkdirSync(huskyDir, { recursive: true });

const hookBody = `#!/usr/bin/env sh
# Husky pre-commit — see scripts/git-hooks/pre-commit.mjs
node scripts/git-hooks/pre-commit.mjs
`;

writeFileSync(huskyPreCommit, hookBody.replace(/\r\n/g, '\n'), 'utf8');
try {
  chmodSync(huskyPreCommit, 0o755);
} catch {
  /* Windows may ignore mode bits */
}

try {
  execSync('npx husky', { cwd: root, stdio: 'inherit' });
} catch {
  console.error('husky install failed. Is husky listed in root package.json devDependencies?');
  process.exit(1);
}

console.log('OK: Husky hooks installed (.husky/pre-commit).');
console.log('Skip all: SKIP_PRECOMMIT=1 git commit ...');
console.log('Skip API: SKIP_API_CLIENT_VERIFY=1 git commit ...');
console.log('Run tests: HUSKY_RUN_TESTS=1 git commit ...');
