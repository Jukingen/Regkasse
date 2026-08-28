#!/usr/bin/env node
/**
 * Pre-commit / CI secret scanner.
 *
 *   node scripts/git-hooks/scan-secrets.mjs              # staged files (default)
 *   node scripts/git-hooks/scan-secrets.mjs --tracked    # all git-tracked files
 *   node scripts/git-hooks/scan-secrets.mjs --files a b
 *
 * Skip: SKIP_SECRET_SCAN=1
 *
 * Does not print secret values — only file path, rule id, and a short hint.
 */
import { execSync } from 'node:child_process';
import { readFileSync, statSync } from 'node:fs';
import { basename, extname } from 'node:path';
import { pathToFileURL } from 'node:url';

const MAX_BYTES = 1_500_000;

/** Documented Development seed passwords (UserSeedData / DemoTenantAdminSeed). */
const ALLOWED_SEED_PASSWORDS = new Set(['Admin123!', 'DemoTenant1!']);

const PLACEHOLDER_RE =
  /^(YOUR_[A-Z0-9_]+_HERE|CHANGE_ME.*|PLACEHOLDER.*|<.*>|\.{3,}|…+|\*{3,}|x{3,}|TODO|FIXME|secret|password|empty|null|none|n\/a|-)$/i;

const SKIP_CONTENT_EXT = new Set([
  '.webp',
  '.png',
  '.jpg',
  '.jpeg',
  '.gif',
  '.ico',
  '.woff',
  '.woff2',
  '.ttf',
  '.eot',
  '.dll',
  '.pdb',
  '.exe',
  '.zip',
  '.gz',
  '.jar',
  '.pdf',
  '.mp4',
  '.wasm',
  '.lock',
]);

const SKIP_CONTENT_BASENAMES = new Set(['swagger.json', 'package-lock.json', 'yarn.lock', 'pnpm-lock.yaml']);

/** @typedef {{ id: string; hint: string }} Finding */

export function isExamplePath(posixPath) {
  const base = basename(posixPath);
  return /\.example(\.|$)/i.test(base) || base.endsWith('.example');
}

/**
 * Filenames that must never be committed (templates use *.example).
 * @param {string} posixPath
 */
export function isBlockedPath(posixPath) {
  const normalized = posixPath.replace(/\\/g, '/');
  const base = basename(normalized);
  const lower = base.toLowerCase();

  if (isExamplePath(normalized)) return false;

  const blockedNames = new Set([
    'appsettings.json',
    'appsettings.development.json',
    'appsettings.staging.json',
    'appsettings.production.json',
    'credentials.json',
    'user-secrets.json',
    '.user-secrets.json',
    'secrets.json',
    '.env',
    '.env.local',
    '.env.development',
    '.env.production',
    '.env.staging',
    '.env.test',
  ]);
  if (blockedNames.has(lower)) return true;

  if (/\.(pem|key|pfx|p12|crt|cer|der|p7b|p7c|snk|ppk|kdbx|secret)$/i.test(lower)) {
    return true;
  }

  if (/^id_(rsa|dsa|ecdsa|ed25519)$/i.test(lower)) return true;

  return false;
}

function shouldSkipContent(posixPath) {
  const normalized = posixPath.replace(/\\/g, '/');
  const ext = extname(normalized).toLowerCase();
  const base = basename(normalized).toLowerCase();
  if (SKIP_CONTENT_EXT.has(ext)) return true;
  if (SKIP_CONTENT_BASENAMES.has(base)) return true;
  // Own fixtures intentionally contain PEM / key-shaped samples for unit tests.
  if (normalized.endsWith('scripts/git-hooks/scan-secrets.test.mjs')) return true;
  if (normalized.includes('/locales/')) return true;
  if (normalized.includes('/i18n/locales/')) return true;
  if (normalized.includes('node_modules/')) return true;
  return false;
}

function extractConnPassword(raw) {
  return String(raw ?? '')
    .trim()
    .split(/[\s"'\\,#]/)[0];
}

function isPlaceholderValue(value) {
  const v = String(value ?? '')
    .trim()
    .replace(/^["'`]+/, '')
    .replace(/["'`]+$/, '');
  if (!v) return true;
  if (ALLOWED_SEED_PASSWORDS.has(v)) return true;
  if (PLACEHOLDER_RE.test(v)) return true;
  if (/^YOUR_[A-Z0-9_]+$/i.test(v)) return true;
  if (/^(postgres|openapi|secret|password|test|admin|changeme)$/i.test(v)) return true;
  if (/^CHANGE_ME/i.test(v)) return true;
  if (/^\$\{[A-Za-z0-9_]+(?::-[^}]*)?\}$/.test(v)) return true;
  if (/^\$[A-Za-z0-9_]+$/.test(v)) return true;
  if (/^\$\{\{/.test(v)) return true;
  if (v.length <= 2) return true;
  return false;
}

/**
 * @param {string} text
 * @param {string} [posixPath]
 * @returns {Finding[]}
 */
export function scanText(text, posixPath = '') {
  /** @type {Finding[]} */
  const findings = [];
  const add = (id, hint) => findings.push({ id, hint });

  if (/-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----/.test(text)) {
    add('private-key-pem', 'PEM private key block');
  }

  const connPass = [...text.matchAll(/;Password=([^;\r\n]+)/gi)];
  for (const m of connPass) {
    const value = extractConnPassword(m[1]);
    if (!isPlaceholderValue(value) && value.length >= 4) {
      add('connection-string-password', 'connection string Password=…');
      break;
    }
  }

  if (/\bsk_live_[A-Za-z0-9]{8,}/.test(text)) {
    add('stripe-live-secret', 'Stripe live secret key');
  }
  if (/\brk_live_[A-Za-z0-9]{8,}/.test(text)) {
    add('stripe-live-restricted', 'Stripe live restricted key');
  }
  const whsec = text.match(/\bwhsec_[A-Za-z0-9]{16,}/);
  if (whsec && !/whsec_(test|example|placeholder)/i.test(whsec[0])) {
    add('stripe-webhook-secret', 'Stripe webhook secret');
  }

  if (/\bAKIA[0-9A-Z]{16}\b/.test(text)) {
    add('aws-access-key', 'AWS access key id');
  }
  if (/\bghp_[A-Za-z0-9]{20,}/.test(text) || /\bgithub_pat_[A-Za-z0-9_]{20,}/.test(text)) {
    add('github-token', 'GitHub token');
  }
  if (/\bxox[baprs]-[A-Za-z0-9-]{10,}/.test(text)) {
    add('slack-token', 'Slack token');
  }
  if (/hooks\.slack\.com\/services\/[A-Z0-9]+\/[A-Z0-9]+\/[A-Za-z0-9]+/.test(text)) {
    add('slack-webhook', 'Slack incoming webhook URL');
  }

  const fiskalyKey = text.match(/"ApiKey"\s*:\s*"(test_[A-Za-z0-9_]{12,})"/);
  if (fiskalyKey && !isPlaceholderValue(fiskalyKey[1]) && !/_HERE$/i.test(fiskalyKey[1])) {
    add('fiskaly-api-key', 'Fiskaly ApiKey value (use user-secrets / env)');
  }
  const fiskalySecret = text.match(/"ApiSecret"\s*:\s*"([^"]{20,})"/);
  if (fiskalySecret && !isPlaceholderValue(fiskalySecret[1]) && !/_HERE$/i.test(fiskalySecret[1])) {
    add('fiskaly-api-secret', 'Fiskaly ApiSecret value (use user-secrets / env)');
  }

  const jwtSecret = text.match(/"SecretKey"\s*:\s*"([^"]{16,})"/);
  if (jwtSecret && !isPlaceholderValue(jwtSecret[1]) && !/_HERE$/i.test(jwtSecret[1])) {
    add('jwt-secret-key', 'JwtSettings SecretKey in file');
  }

  const jsonPasswordEligible =
    posixPath &&
    /\.(json|ya?ml|xml)$/i.test(posixPath) &&
    !posixPath.includes('/locales/') &&
    !posixPath.endsWith('package.json');
  if (jsonPasswordEligible) {
    const jsonPassword = [
      ...text.matchAll(/"(?:Password|password|SmtpPassword|finanzOnlinePassword)"\s*:\s*"([^"]+)"/g),
    ];
    for (const m of jsonPassword) {
      const value = m[1];
      if (!isPlaceholderValue(value) && value.length >= 8 && !/_HERE$/i.test(value)) {
        add('json-password', 'password field with a non-placeholder value');
        break;
      }
    }
  }

  if (posixPath && /\.npmrc$/i.test(posixPath) && /(_authToken|_password)\s*=\s*\S+/.test(text)) {
    add('npm-auth-token', '.npmrc auth token');
  }

  return findings;
}

/**
 * @param {string} posixPath
 * @param {string} [text]
 * @returns {Finding[]}
 */
export function scanFile(posixPath, text) {
  /** @type {Finding[]} */
  const findings = [];
  if (isBlockedPath(posixPath)) {
    findings.push({
      id: 'blocked-path',
      hint: 'sensitive filename (use *.example templates / user-secrets)',
    });
  }
  if (text == null || shouldSkipContent(posixPath)) return findings;
  findings.push(...scanText(text, posixPath));
  return findings;
}

function git(args, cwd) {
  return execSync(`git ${args}`, { cwd, encoding: 'utf8' }).trim();
}

function stagedFiles(cwd) {
  try {
    const out = git('diff --cached --name-only --diff-filter=ACMR', cwd);
    return out ? out.split(/\r?\n/).map((s) => s.trim()).filter(Boolean) : [];
  } catch {
    return [];
  }
}

function trackedFiles(cwd) {
  const out = git('ls-files', cwd);
  return out ? out.split(/\r?\n/).map((s) => s.trim()).filter(Boolean) : [];
}

function readIfText(absPath) {
  try {
    const st = statSync(absPath);
    if (!st.isFile() || st.size > MAX_BYTES) return null;
    const buf = readFileSync(absPath);
    if (buf.includes(0)) return null;
    return buf.toString('utf8');
  } catch {
    return null;
  }
}

/**
 * @param {string[]} files posix paths relative to repo root
 * @param {string} root
 * @returns {{ path: string, findings: Finding[] }[]}
 */
export function scanPaths(files, root) {
  /** @type {{ path: string, findings: Finding[] }[]} */
  const results = [];
  for (const rel of files) {
    const posix = rel.replace(/\\/g, '/');
    const abs = `${root.replace(/\\/g, '/')}/${posix}`;
    const text = shouldSkipContent(posix) ? null : readIfText(abs);
    const findings = scanFile(posix, text ?? undefined);
    if (findings.length) results.push({ path: posix, findings });
  }
  return results;
}

function printReport(results) {
  if (!results.length) {
    console.log('secret-scan: OK (no sensitive files or high-confidence secrets).');
    return;
  }
  console.error('secret-scan: blocked — sensitive material in the commit/index:\n');
  for (const row of results) {
    const ids = [...new Set(row.findings.map((f) => f.id))].join(', ');
    const hint = row.findings[0]?.hint ?? '';
    console.error(`  ${row.path}`);
    console.error(`    rules: ${ids}`);
    console.error(`    ${hint}\n`);
  }
  console.error('Do not commit secrets. Use dotnet user-secrets, environment variables, or a vault.');
  console.error('Templates: *.example.json / .env.example with YOUR_*_HERE placeholders.');
  console.error('Policy: SECURITY.md');
  console.error('Skip (emergency only): SKIP_SECRET_SCAN=1 git commit …');
}

function parseArgs(argv) {
  const filesIdx = argv.indexOf('--files');
  if (filesIdx >= 0) {
    return { mode: 'files', files: argv.slice(filesIdx + 1).filter((a) => !a.startsWith('--')) };
  }
  if (argv.includes('--tracked')) return { mode: 'tracked', files: [] };
  return { mode: 'staged', files: [] };
}

export function main(argv = process.argv.slice(2), cwd = process.cwd()) {
  if (process.env.SKIP_SECRET_SCAN === '1') {
    console.log('secret-scan: SKIP_SECRET_SCAN=1 — skipping.');
    return 0;
  }

  const { mode, files: explicit } = parseArgs(argv);
  let files = explicit;
  if (mode === 'staged') files = stagedFiles(cwd);
  else if (mode === 'tracked') files = trackedFiles(cwd);

  if (!files.length) {
    console.log('secret-scan: no files to scan.');
    return 0;
  }

  const results = scanPaths(files, cwd);
  printReport(results);
  return results.length ? 1 : 0;
}

function isDirectRun() {
  const entry = process.argv[1];
  if (!entry) return false;
  try {
    return import.meta.url === pathToFileURL(entry).href;
  } catch {
    return basename(entry) === 'scan-secrets.mjs';
  }
}

if (isDirectRun()) {
  process.exit(main());
}
