#!/usr/bin/env node
/**
 * Parallel `npm run dev -w <pkg>` for long-running servers.
 * Native `npm run dev --workspaces` is sequential and blocks on the first server.
 *
 * Default (RAM-safe): backend + admin only.
 * Full stack: `npm run dev:all` or `node scripts/dev-workspaces.mjs --all`
 *
 * Usage:
 *   node scripts/dev-workspaces.mjs
 *   node scripts/dev-workspaces.mjs --all
 *   node scripts/dev-workspaces.mjs --with=pos,sites
 *   node scripts/dev-workspaces.mjs --only=backend,admin,pos
 *   node scripts/dev-workspaces.mjs --skip-cleanup
 *
 * Env:
 *   REGKASSE_DEV_APPS=backend,admin,pos   # overrides default selection
 */

import { spawn, spawnSync } from 'node:child_process';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '..');

const ALL_WORKSPACES = [
  ['backend', '@regkasse/backend'],
  ['admin', 'registrierkasse-admin'],
  ['pos', 'cash-register'],
  ['sites', 'regkasse-sites'],
];

const DEFAULT_LABELS = ['backend', 'admin'];

const children = [];
let shuttingDown = false;

function parseArgs(argv) {
  const opts = {
    all: false,
    skipCleanup: false,
    with: [],
    only: null,
  };

  for (const arg of argv) {
    if (arg === '--all') {
      opts.all = true;
      continue;
    }
    if (arg === '--skip-cleanup') {
      opts.skipCleanup = true;
      continue;
    }
    if (arg.startsWith('--with=')) {
      opts.with.push(
        ...arg
          .slice('--with='.length)
          .split(',')
          .map((s) => s.trim().toLowerCase())
          .filter(Boolean),
      );
      continue;
    }
    if (arg.startsWith('--only=')) {
      opts.only = arg
        .slice('--only='.length)
        .split(',')
        .map((s) => s.trim().toLowerCase())
        .filter(Boolean);
      continue;
    }
    if (arg === '--help' || arg === '-h') {
      printHelp();
      process.exit(0);
    }
    console.error(`[dev] unknown argument: ${arg}`);
    printHelp();
    process.exit(1);
  }

  return opts;
}

function printHelp() {
  console.log(`Regkasse parallel dev launcher

Default: backend + admin (RAM-safe)
Full:    --all   (backend + admin + pos + sites)

  --with=pos,sites     add apps to the default set
  --only=backend,admin explicit set (ignores default/--all/--with)
  --skip-cleanup       do not kill orphan Next/Expo workers first
  --help

Env REGKASSE_DEV_APPS=backend,admin,pos overrides selection.
npm scripts: npm run dev | npm run dev:all | npm run dev:cleanup`);
}

function resolveSelection(opts) {
  const fromEnv = process.env.REGKASSE_DEV_APPS;
  if (fromEnv && fromEnv.trim()) {
    return fromEnv
      .split(',')
      .map((s) => s.trim().toLowerCase())
      .filter(Boolean);
  }

  if (opts.only) {
    return opts.only;
  }

  if (opts.all) {
    return ALL_WORKSPACES.map(([label]) => label);
  }

  const labels = [...DEFAULT_LABELS];
  for (const extra of opts.with) {
    if (!labels.includes(extra)) {
      labels.push(extra);
    }
  }
  return labels;
}

function selectWorkspaces(labels) {
  const known = new Map(ALL_WORKSPACES);
  const selected = [];
  for (const label of labels) {
    const name = known.get(label);
    if (!name) {
      console.error(
        `[dev] unknown app "${label}". Known: ${ALL_WORKSPACES.map(([l]) => l).join(', ')}`,
      );
      process.exit(1);
    }
    selected.push([label, name]);
  }
  return selected;
}

function runCleanup() {
  const script = path.join(repoRoot, 'scripts', 'cleanup-dev-orphans.mjs');
  console.log('[dev] cleaning orphan Next/Expo node workers…');
  const result = spawnSync(process.execPath, [script], {
    cwd: repoRoot,
    stdio: 'inherit',
    env: process.env,
  });
  if (result.status && result.status !== 0) {
    console.warn(`[dev] cleanup exited with code ${result.status} (continuing)`);
  }
}

function killProcessTree(pid) {
  if (!pid) return;
  if (process.platform === 'win32') {
    spawnSync('taskkill', ['/F', '/T', '/PID', String(pid)], {
      stdio: 'ignore',
      windowsHide: true,
    });
    return;
  }
  try {
    process.kill(-pid, 'SIGTERM');
  } catch {
    try {
      process.kill(pid, 'SIGTERM');
    } catch {
      /* already gone */
    }
  }
}

function shutdown(code = 0) {
  if (shuttingDown) return;
  shuttingDown = true;
  console.log('\n[dev] shutting down workspace servers…');
  for (const child of children) {
    if (!child.killed && child.pid) {
      killProcessTree(child.pid);
    }
  }
  // Second pass: orphans left by Next/Expo after parent npm died
  runCleanup();
  process.exit(code);
}

process.on('SIGINT', () => shutdown(0));
process.on('SIGTERM', () => shutdown(0));

const opts = parseArgs(process.argv.slice(2));
const labels = resolveSelection(opts);
const workspaces = selectWorkspaces(labels);

if (!opts.skipCleanup) {
  runCleanup();
}

console.log(
  `[dev] starting (RAM-safe default is backend+admin): ${workspaces
    .map(([label]) => label)
    .join(', ')}`,
);
if (!opts.all && !opts.only && !process.env.REGKASSE_DEV_APPS) {
  console.log('[dev] tip: full stack → npm run dev:all   |   add POS → npm run dev -- --with=pos');
}

for (const [label, name] of workspaces) {
  const child = spawn(`npm run dev -w ${name}`, {
    stdio: 'inherit',
    shell: true,
    env: process.env,
    cwd: repoRoot,
    // On Unix, new process group so we can signal the whole tree
    detached: process.platform !== 'win32',
  });
  child.on('exit', (code, signal) => {
    if (shuttingDown) return;
    if (signal) {
      console.error(`[${label}] exited via ${signal}`);
      shutdown(1);
      return;
    }
    if (code && code !== 0) {
      console.error(`[${label}] exited with code ${code}`);
      shutdown(code);
    }
  });
  children.push(child);
}
