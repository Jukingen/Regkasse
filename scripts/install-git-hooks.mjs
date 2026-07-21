#!/usr/bin/env node
/**
 * Points this repo's git hooksPath at scripts/git-hooks (local only; not committed config).
 *
 *   node scripts/install-git-hooks.mjs
 *   # or: npm run install:git-hooks
 */
import { execSync } from 'node:child_process';
import { chmodSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');
const hooksRel = 'scripts/git-hooks';
const preCommit = join(root, hooksRel, 'pre-commit');
const preCommitJs = join(root, hooksRel, 'pre-commit.mjs');

if (!existsSync(preCommit) || !existsSync(preCommitJs)) {
  console.error(`Missing ${hooksRel}/pre-commit or pre-commit.mjs`);
  process.exit(1);
}

try {
  chmodSync(preCommit, 0o755);
} catch {
  /* Windows may ignore mode bits */
}

execSync(`git config core.hooksPath ${hooksRel}`, { cwd: root, stdio: 'inherit' });
console.log(`OK: git core.hooksPath → ${hooksRel}`);
console.log(
  'OpenAPI/Orval commits will run verify-api-client when swagger/generated paths are staged.',
);
