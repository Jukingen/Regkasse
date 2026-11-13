#!/usr/bin/env node
/**
 * Manual production cutover pre-flight. Not a CI job.
 *
 * Dry run (no host; expect exit 1 until env and bases are set):
 *   node scripts/pre-flight-cutover.mjs
 *   node scripts/pre-flight-cutover.mjs --help
 *
 * Against a target host shell that already has Production env:
 *   node scripts/pre-flight-cutover.mjs \
 *     --api-base https://api.regkasse.at \
 *     --admin-base https://admin.regkasse.at \
 *     --pos-base https://pos.regkasse.at
 *
 * Does not print secret values. Runbook: docs/PRODUCTION_CUTOVER.md
 */
import { spawn } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const results = [];

function parseArgs(argv) {
  const out = { help: false, apiBase: '', adminBase: '', posBase: '' };
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === '--help' || arg === '-h') out.help = true;
    else if (arg === '--api-base') out.apiBase = argv[++i] ?? '';
    else if (arg === '--admin-base') out.adminBase = argv[++i] ?? '';
    else if (arg === '--pos-base') out.posBase = argv[++i] ?? '';
    else {
      console.error(`Unknown argument: ${arg}`);
      console.error('Run: node scripts/pre-flight-cutover.mjs --help');
      process.exit(1);
    }
  }
  return out;
}

function printHelp() {
  console.log(`pre-flight-cutover.mjs — manual production cutover checks

Usage:
  node scripts/pre-flight-cutover.mjs --help
  node scripts/pre-flight-cutover.mjs --api-base URL --admin-base URL --pos-base URL

Flags:
  --api-base     API origin, no trailing path (https://api.regkasse.at)
  --admin-base   FA origin (https://admin.regkasse.at)
  --pos-base     POS origin (https://pos.regkasse.at)

Checks (PASS/FAIL each; exit 1 if any FAIL):
  ConnectionStrings__DefaultConnection
  JwtSettings__SecretKey
  Sentry__Dsn
  SLACK_WEBHOOK_URL
  License__OfflineVerificationPublicKeyPem or LicenseSettings__PublicKeyPath
  NEXT_PUBLIC_API_BASE_URL and NEXT_PUBLIC_RKSV_ENVIRONMENT (FA build)
  EXPO_PUBLIC_API_BASE_URL (POS build)
  GET {api}/api/health/live and GET {api}/api/health/ready return 200
  GET {admin-base} and GET {pos-base} return 200 or a redirect
  dotnet ef migrations list pendingCount=0

Manual dry run:
  From the repo root, with no Production env exported, run the script with no
  flags. Every missing variable and every missing base prints FAIL and the
  process exits 1. That confirms the checker itself. Then export the target
  host environment and pass the three bases before D-1.
`);
}

function record(name, ok, detail) {
  results.push({ name, ok, detail });
  const label = ok ? 'PASS' : 'FAIL';
  const extra = detail ? ` — ${detail}` : '';
  console.log(`${label}  ${name}${extra}`);
}

function envSet(name) {
  const value = process.env[name];
  return typeof value === 'string' && value.trim().length > 0;
}

function checkEnv(name) {
  record(name, envSet(name), envSet(name) ? 'set' : 'missing or empty');
}

function trimBase(url) {
  return url.replace(/\/+$/, '');
}

async function checkHttp(name, url, allowed) {
  if (!url) {
    record(name, false, 'base URL not provided');
    return;
  }
  try {
    const response = await fetch(url, {
      redirect: 'manual',
      signal: AbortSignal.timeout(15000),
    });
    record(name, allowed.includes(response.status), `HTTP ${response.status}`);
  } catch (error) {
    const message = error instanceof Error ? error.message : 'request failed';
    record(name, false, message);
  }
}

function runEfMigrationsList() {
  return new Promise((resolve) => {
    const child = spawn(
      'dotnet',
      ['ef', 'migrations', 'list', '--project', 'backend'],
      { cwd: repoRoot, windowsHide: true },
    );
    let stdout = '';
    let stderr = '';
    child.stdout.on('data', (chunk) => {
      stdout += chunk.toString();
    });
    child.stderr.on('data', (chunk) => {
      stderr += chunk.toString();
    });
    child.on('error', (error) => {
      resolve({ ok: false, pendingCount: -1, detail: error.message });
    });
    child.on('close', (code) => {
      if (code !== 0) {
        const detail = (stderr || stdout).trim().split(/\r?\n/).slice(-3).join(' ');
        resolve({
          ok: false,
          pendingCount: -1,
          detail: detail || `dotnet ef exited ${code}`,
        });
        return;
      }
      const pendingCount = stdout.split(/\r?\n/).filter((line) => /\(Pending\)/.test(line)).length;
      resolve({ ok: pendingCount === 0, pendingCount, detail: `pendingCount=${pendingCount}` });
    });
  });
}

const args = parseArgs(process.argv.slice(2));
if (args.help) {
  printHelp();
  process.exit(0);
}

console.log('Regkasse production cutover pre-flight');

checkEnv('ConnectionStrings__DefaultConnection');
checkEnv('JwtSettings__SecretKey');
checkEnv('Sentry__Dsn');
checkEnv('SLACK_WEBHOOK_URL');

const licenseInline = envSet('License__OfflineVerificationPublicKeyPem');
const licensePath = envSet('LicenseSettings__PublicKeyPath');
record(
  'License offline verification key',
  licenseInline || licensePath,
  licenseInline || licensePath
    ? 'Pem or LicenseSettings__PublicKeyPath set'
    : 'set License__OfflineVerificationPublicKeyPem or LicenseSettings__PublicKeyPath',
);

checkEnv('NEXT_PUBLIC_API_BASE_URL');
checkEnv('NEXT_PUBLIC_RKSV_ENVIRONMENT');
checkEnv('EXPO_PUBLIC_API_BASE_URL');

const apiBase = args.apiBase ? trimBase(args.apiBase) : '';
const adminBase = args.adminBase ? trimBase(args.adminBase) : '';
const posBase = args.posBase ? trimBase(args.posBase) : '';

await checkHttp('GET /api/health/live', apiBase ? `${apiBase}/api/health/live` : '', [200]);
await checkHttp('GET /api/health/ready', apiBase ? `${apiBase}/api/health/ready` : '', [200]);
await checkHttp('FA base', adminBase, [200, 301, 302, 307, 308]);
await checkHttp('POS base', posBase, [200, 301, 302, 307, 308]);

if (!envSet('ConnectionStrings__DefaultConnection')) {
  record(
    'dotnet ef migrations list',
    false,
    'pendingCount unknown; ConnectionStrings__DefaultConnection missing',
  );
} else {
  const migrations = await runEfMigrationsList();
  record('dotnet ef migrations list', migrations.ok, migrations.detail);
}

const failed = results.some((item) => !item.ok);
console.log(failed ? 'pre-flight: FAIL' : 'pre-flight: PASS');
process.exit(failed ? 1 : 0);
