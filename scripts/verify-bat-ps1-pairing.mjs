#!/usr/bin/env node
/**
 * Validates Windows .bat / .ps1 pairing under scripts/ (and root convenience .bat files).
 *
 * Rules:
 * 1. Every scripts/*.ps1 must have a sibling .bat, unless listed in BAT_OPTIONAL_PS1.
 * 2. Every scripts/*.bat must have a sibling .ps1 of the same basename, unless listed in PS1_OPTIONAL_BAT.
 * 3. Root *.bat files are expected to be npm/docker helpers (no sibling .ps1); listed in ROOT_BAT_ONLY.
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
]);

/**
 * .bat files under scripts/ that intentionally have no same-name .ps1
 * (aliases, helpers, or wrappers that call a different script / node).
 */
const PS1_OPTIONAL_BAT = new Set([
  '_common.bat',
  'run-with-log.bat',
  'smoke-test.bat', // lightweight curl smoke (comprehensive → run-comprehensive-smoke.ps1)
  'clean-backend.bat', // → clean-backend-build.ps1
  'generate-dep-export.bat', // → generate-dep-export-fixtures.ps1
  'dev-purge-tenant.bat', // → dev-purge-tenant-catalog.ps1
  'fix-antd.bat', // → fix-antd-deprecations.mjs
  'dev-mail.bat', // env + dev-mail-test.bat
  'dev-mail-test.bat', // → test-forgot-username-email.ps1
  'test-mode-scripts.bat', // Legacy/Docker mode structural smoke (no .ps1 pair)
]);

/** Root-level convenience .bat files (npm / docker / deploy) — no .ps1 pair required. */
const ROOT_BAT_ONLY = new Set([
  'start.bat', // mode chooser → scripts/legacy vs scripts/docker
  'start-dev.bat',
  'start-backend.bat',
  'start-admin.bat',
  'start-pos.bat',
  'start-sites.bat',
  'test-all.bat',
  'clean-all.bat',
  'docker-up.bat',
  'docker-up-prod.bat',
  'docker-down.bat',
  'docker-down-prod.bat',
  'docker-clean.bat',
  'docker-status.bat',
  'docker-logs.bat',
  'docker-build-prod.bat',
  'docker-logs-prod.bat',
  'docker-push-prod.bat',
  'deploy.bat',
  'deploy-docker.bat',
  'rollback.bat',
]);

const jsonMode = process.argv.includes('--json');

function listExt(dir, ext) {
  if (!fs.existsSync(dir)) return [];
  return fs
    .readdirSync(dir, { withFileTypes: true })
    .filter((e) => e.isFile() && e.name.toLowerCase().endsWith(ext))
    .map((e) => e.name)
    .sort();
}

const ps1Files = listExt(scriptsDir, '.ps1');
const batFiles = listExt(scriptsDir, '.bat');
const rootBats = listExt(root, '.bat');

const missingBatForPs1 = [];
const missingPs1ForBat = [];
const unexpectedRootBat = [];

for (const name of ps1Files) {
  if (BAT_OPTIONAL_PS1.has(name)) continue;
  const bat = name.replace(/\.ps1$/i, '.bat');
  if (!fs.existsSync(path.join(scriptsDir, bat))) {
    missingBatForPs1.push({ ps1: name, expectedBat: bat });
  }
}

for (const name of batFiles) {
  if (PS1_OPTIONAL_BAT.has(name)) continue;
  const ps1 = name.replace(/\.bat$/i, '.ps1');
  if (!fs.existsSync(path.join(scriptsDir, ps1))) {
    missingPs1ForBat.push({ bat: name, expectedPs1: ps1 });
  }
}

for (const name of rootBats) {
  if (!ROOT_BAT_ONLY.has(name)) {
    unexpectedRootBat.push({
      bat: name,
      hint: 'Add to ROOT_BAT_ONLY in verify-bat-ps1-pairing.mjs or move under scripts/',
    });
  }
}

// Orphan allowlist entries (typos) — warn but do not fail CI hard for extras
const warnUnused = [];
for (const name of BAT_OPTIONAL_PS1) {
  if (!ps1Files.includes(name)) warnUnused.push(`BAT_OPTIONAL_PS1 unused: ${name}`);
}
for (const name of PS1_OPTIONAL_BAT) {
  if (!batFiles.includes(name)) warnUnused.push(`PS1_OPTIONAL_BAT unused: ${name}`);
}
for (const name of ROOT_BAT_ONLY) {
  if (!rootBats.includes(name)) warnUnused.push(`ROOT_BAT_ONLY unused: ${name}`);
}

const ok =
  missingBatForPs1.length === 0 &&
  missingPs1ForBat.length === 0 &&
  unexpectedRootBat.length === 0;

const report = {
  ok,
  scriptsDir: path.relative(root, scriptsDir).replace(/\\/g, '/'),
  counts: {
    ps1: ps1Files.length,
    scriptsBat: batFiles.length,
    rootBat: rootBats.length,
  },
  missingBatForPs1,
  missingPs1ForBat,
  unexpectedRootBat,
  allowlists: {
    BAT_OPTIONAL_PS1: [...BAT_OPTIONAL_PS1],
    PS1_OPTIONAL_BAT: [...PS1_OPTIONAL_BAT],
    ROOT_BAT_ONLY: [...ROOT_BAT_ONLY],
  },
  warnings: warnUnused,
};

if (jsonMode) {
  console.log(JSON.stringify(report, null, 2));
} else {
  console.log('verify-bat-ps1-pairing');
  console.log(`  scripts/*.ps1: ${ps1Files.length}`);
  console.log(`  scripts/*.bat: ${batFiles.length}`);
  console.log(`  root/*.bat:    ${rootBats.length}`);
  if (missingBatForPs1.length) {
    console.log('\n[FAIL] .ps1 without sibling .bat:');
    for (const x of missingBatForPs1) {
      console.log(`  - ${x.ps1}  (expected ${x.expectedBat})`);
      console.log('    Fix: scripts\\create-bat-wrappers.bat  OR add to BAT_OPTIONAL_PS1');
    }
  }
  if (missingPs1ForBat.length) {
    console.log('\n[FAIL] scripts/*.bat without same-name .ps1:');
    for (const x of missingPs1ForBat) {
      console.log(`  - ${x.bat}  (expected ${x.expectedPs1})`);
      console.log('    Fix: add matching .ps1  OR add basename to PS1_OPTIONAL_BAT');
    }
  }
  if (unexpectedRootBat.length) {
    console.log('\n[FAIL] unexpected root .bat (not in ROOT_BAT_ONLY):');
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
