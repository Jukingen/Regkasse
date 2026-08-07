#!/usr/bin/env node
/**
 * Validates Windows .bat / .ps1 pairing under scripts/ (recursive by category).
 *
 * Rules:
 * 1. Every scripts/ (recursive) .ps1 must have a sibling .bat, unless listed in BAT_OPTIONAL_PS1.
 * 2. Every scripts/ (recursive) .bat must have a sibling .ps1 of the same basename, unless listed in PS1_OPTIONAL_BAT.
 * 3. Repo root must have zero .bat files (entry points live under scripts/category/).
 *
 * Allowlist entries may be basename ("smoke-test.bat") or scripts-relative path ("docker/host/up.bat").
 *
 * Usage:
 *   node scripts/verify-bat-ps1-pairing.mjs
 *   node scripts/verify-bat-ps1-pairing.mjs --json
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(__dirname, '..');
const scriptsDir = path.join(root, 'scripts');

/** .ps1 files that intentionally have no .bat (libraries / dot-sourced). */
const BAT_OPTIONAL_PS1 = new Set([
  'dev-mail-config.ps1',
  'dev/dev-mail-config.ps1',
  'GameMode.ps1',
  'legacy/GameMode.ps1',
  'WorkMode.ps1',
  'legacy/WorkMode.ps1',
]);

/**
 * .bat files under scripts/ that intentionally have no same-name .ps1
 * (aliases, helpers, host/legacy UI, or wrappers that call a different script / node).
 */
const PS1_OPTIONAL_BAT = new Set([
  // lib helpers
  '_common.bat',
  'lib/_common.bat',
  'run-with-log.bat',
  'lib/run-with-log.bat',
  // aliases / bat-only helpers
  'smoke-test.bat',
  'test/smoke-test.bat',
  'clean-backend.bat',
  'dev/clean-backend.bat',
  'generate-dep-export.bat',
  'rksv/generate-dep-export.bat',
  'fix-antd.bat',
  'dev/fix-antd.bat',
  'dev-mail.bat',
  'dev/dev-mail.bat',
  'dev-mail-test.bat',
  'dev/dev-mail-test.bat',
  'test-mode-scripts.bat',
  'test/test-mode-scripts.bat',
  // category entry points (no sibling .ps1)
  'start.bat',
  'dev/start.bat',
  'start-dev.bat',
  'dev/start-dev.bat',
  'start-backend.bat',
  'dev/start-backend.bat',
  'start-admin.bat',
  'dev/start-admin.bat',
  'start-pos.bat',
  'dev/start-pos.bat',
  'start-sites.bat',
  'dev/start-sites.bat',
  'test-all.bat',
  'test/test-all.bat',
  // DANGER bat-only (no same-name .ps1) — destructive ops
  'clean-all.DANGER.bat',
  'dev/clean-all.DANGER.bat',
  'deploy.DANGER.bat',
  'ops/deploy.DANGER.bat',
  'rollback.DANGER.bat',
  'ops/rollback.DANGER.bat',
  'dev-purge-tenant.DANGER.bat',
  'dev/dev-purge-tenant.DANGER.bat',
  'clean.DANGER.bat',
  'docker/host/clean.DANGER.bat',
  // docker/host chooser bats
  'docker/host/up.bat',
  'docker/host/down.bat',
  'docker/host/status.bat',
  'docker/host/logs.bat',
  'docker/host/up-backend.bat',
  'docker/host/up-admin.bat',
  'docker/host/up-pos.bat',
  'docker/host/_require-docker.bat',
  '_require-docker.bat',
  // legacy host starters
  'legacy/_repo.bat',
  'legacy/start-all.bat',
  'legacy/start-backend.bat',
  'legacy/start-frontend.bat',
  'legacy/start-frontend-admin.bat',
  'legacy/start-redis.bat',
  'legacy/kill-ports.bat',
  'legacy/open-tabs.bat',
]);

const jsonMode = process.argv.includes('--json');

const EXCLUDE_DIR_RE =
  /[\\/](node_modules|\.git|bin|obj|publish|\.next|dist|coverage|_test)([\\/]|$)/i;

function isExcluded(fullPath) {
  return EXCLUDE_DIR_RE.test(fullPath.replace(/\//g, '\\'));
}

function walkFiles(dir, ext, out = []) {
  if (!fs.existsSync(dir)) return out;
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, ent.name);
    if (ent.isDirectory()) {
      if (isExcluded(full)) continue;
      walkFiles(full, ext, out);
      continue;
    }
    if (!ent.isFile()) continue;
    if (isExcluded(full)) continue;
    if (ent.name.toLowerCase().endsWith(ext)) out.push(full);
  }
  return out;
}

function toScriptsRel(fullPath) {
  return path.relative(scriptsDir, fullPath).split(path.sep).join('/');
}

function allowlisted(set, relPosix, basename) {
  return set.has(relPosix) || set.has(basename);
}

function listRootBats() {
  if (!fs.existsSync(root)) return [];
  return fs
    .readdirSync(root, { withFileTypes: true })
    .filter((e) => e.isFile() && e.name.toLowerCase().endsWith('.bat'))
    .map((e) => e.name)
    .sort();
}

const ps1Full = walkFiles(scriptsDir, '.ps1').sort();
const batFull = walkFiles(scriptsDir, '.bat').sort();
const rootBats = listRootBats();

const ps1Rels = ps1Full.map(toScriptsRel);
const batRels = batFull.map(toScriptsRel);

const missingBatForPs1 = [];
const missingPs1ForBat = [];
const unexpectedRootBat = [];

for (const full of ps1Full) {
  const rel = toScriptsRel(full);
  const base = path.basename(full);
  if (allowlisted(BAT_OPTIONAL_PS1, rel, base)) continue;
  const batFullPath = full.replace(/\.ps1$/i, '.bat');
  if (!fs.existsSync(batFullPath)) {
    missingBatForPs1.push({ ps1: rel, expectedBat: rel.replace(/\.ps1$/i, '.bat') });
  }
}

for (const full of batFull) {
  const rel = toScriptsRel(full);
  const base = path.basename(full);
  if (allowlisted(PS1_OPTIONAL_BAT, rel, base)) continue;
  const ps1FullPath = full.replace(/\.bat$/i, '.ps1');
  if (!fs.existsSync(ps1FullPath)) {
    missingPs1ForBat.push({ bat: rel, expectedPs1: rel.replace(/\.bat$/i, '.ps1') });
  }
}

for (const name of rootBats) {
  unexpectedRootBat.push({
    bat: name,
    hint: 'Remove root .bat — move under scripts/<category>/ (root bats are forbidden)',
  });
}

const warnUnused = [];
for (const name of BAT_OPTIONAL_PS1) {
  const hit = ps1Rels.includes(name) || ps1Rels.some((r) => path.posix.basename(r) === name);
  if (!hit) warnUnused.push(`BAT_OPTIONAL_PS1 unused: ${name}`);
}
for (const name of PS1_OPTIONAL_BAT) {
  const hit = batRels.includes(name) || batRels.some((r) => path.posix.basename(r) === name);
  if (!hit) warnUnused.push(`PS1_OPTIONAL_BAT unused: ${name}`);
}

const ok =
  missingBatForPs1.length === 0 &&
  missingPs1ForBat.length === 0 &&
  unexpectedRootBat.length === 0;

const report = {
  ok,
  scriptsDir: path.relative(root, scriptsDir).replace(/\\/g, '/'),
  counts: {
    ps1: ps1Rels.length,
    scriptsBat: batRels.length,
    rootBat: rootBats.length,
  },
  missingBatForPs1,
  missingPs1ForBat,
  unexpectedRootBat,
  allowlists: {
    BAT_OPTIONAL_PS1: [...BAT_OPTIONAL_PS1],
    PS1_OPTIONAL_BAT: [...PS1_OPTIONAL_BAT],
  },
  warnings: warnUnused,
};

if (jsonMode) {
  console.log(JSON.stringify(report, null, 2));
} else {
  console.log('verify-bat-ps1-pairing');
  console.log(`  scripts/**/*.ps1: ${ps1Rels.length}`);
  console.log(`  scripts/**/*.bat: ${batRels.length}`);
  console.log(`  root/*.bat:       ${rootBats.length} (must be 0)`);
  if (missingBatForPs1.length) {
    console.log('\n[FAIL] .ps1 without sibling .bat:');
    for (const x of missingBatForPs1) {
      console.log(`  - ${x.ps1}  (expected ${x.expectedBat})`);
      console.log('    Fix: scripts\\lib\\create-bat-wrappers.bat  OR add to BAT_OPTIONAL_PS1');
    }
  }
  if (missingPs1ForBat.length) {
    console.log('\n[FAIL] scripts/**/*.bat without same-name .ps1:');
    for (const x of missingPs1ForBat) {
      console.log(`  - ${x.bat}  (expected ${x.expectedPs1})`);
      console.log('    Fix: add matching .ps1  OR add path/basename to PS1_OPTIONAL_BAT');
    }
  }
  if (unexpectedRootBat.length) {
    console.log('\n[FAIL] root .bat files are forbidden:');
    for (const x of unexpectedRootBat) {
      console.log(`  - ${x.bat}`);
      console.log(`    ${x.hint}`);
    }
  }
  if (warnUnused.length) {
    console.log('\n[WARN] allowlist entries with no matching file:');
    for (const w of warnUnused) console.log(`  - ${w}`);
  }
  if (ok) {
    console.log('\n[OK] .bat / .ps1 pairing checks passed.');
  }
}

process.exit(ok ? 0 : 1);
