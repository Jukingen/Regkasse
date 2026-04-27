#!/usr/bin/env node
/**
 * Read-only beta preflight checks.
 * Run from repository root: node scripts/beta-preflight.mjs
 */

const REQUIRED_ENV = {
  backendBaseUrl: 'BACKEND_BASE_URL',
  adminApiBaseUrl: 'NEXT_PUBLIC_API_BASE_URL',
  adminRksvEnvironment: 'NEXT_PUBLIC_RKSV_ENVIRONMENT',
  posApiBaseUrl: 'EXPO_PUBLIC_API_BASE_URL',
};

let failCount = 0;
let warnCount = 0;

function pass(message) {
  console.log(`PASS ${message}`);
}

function warn(message) {
  warnCount += 1;
  console.warn(`WARN ${message}`);
}

function fail(message) {
  failCount += 1;
  console.error(`FAIL ${message}`);
}

function readEnv(name) {
  return process.env[name]?.trim() ?? '';
}

function requireEnv(name, label) {
  const value = readEnv(name);
  if (!value) {
    fail(`${label} is missing: ${name}`);
    return '';
  }

  pass(`${label} is set: ${name}`);
  return value;
}

function normalizeBackendRoot(rawUrl) {
  try {
    const url = new URL(rawUrl);
    url.pathname = url.pathname.replace(/\/+$/, '').replace(/\/api$/i, '');
    url.search = '';
    url.hash = '';
    return url.toString().replace(/\/+$/, '');
  } catch {
    fail(`Backend base URL is not a valid URL: ${REQUIRED_ENV.backendBaseUrl}`);
    return '';
  }
}

async function checkEndpoint(rootUrl, path, { optional = false } = {}) {
  const url = `${rootUrl}${path}`;

  try {
    const response = await fetch(url, { method: 'GET' });
    const body = (await response.text()).trim();

    if (response.ok) {
      pass(`${path} returned OK (${response.status}${body ? `: ${body.slice(0, 80)}` : ''})`);
      return;
    }

    if (optional && response.status === 404) {
      warn(`${path} is not available on this backend (${response.status})`);
      return;
    }

    const message = `${path} returned ${response.status}${body ? `: ${body.slice(0, 120)}` : ''}`;
    if (optional) {
      warn(message);
    } else {
      fail(message);
    }
  } catch (error) {
    const message = `${path} request failed: ${error instanceof Error ? error.message : String(error)}`;
    if (optional) {
      warn(message);
    } else {
      fail(message);
    }
  }
}

async function main() {
  console.log('Beta preflight checks');
  console.log('---------------------');

  const backendBaseUrl = requireEnv(REQUIRED_ENV.backendBaseUrl, 'Backend base URL');
  requireEnv(REQUIRED_ENV.adminApiBaseUrl, 'Admin API base URL');
  requireEnv(REQUIRED_ENV.posApiBaseUrl, 'POS API base URL');

  const rksvEnvironment = requireEnv(REQUIRED_ENV.adminRksvEnvironment, 'Admin RKSV environment');
  if (rksvEnvironment && !['TEST', 'PROD'].includes(rksvEnvironment)) {
    fail(`${REQUIRED_ENV.adminRksvEnvironment} must be TEST or PROD; got ${rksvEnvironment}`);
  } else if (rksvEnvironment) {
    pass(`${REQUIRED_ENV.adminRksvEnvironment} is valid: ${rksvEnvironment}`);
  }

  const backendRootUrl = backendBaseUrl ? normalizeBackendRoot(backendBaseUrl) : '';
  if (backendRootUrl) {
    await checkEndpoint(backendRootUrl, '/health');
    await checkEndpoint(backendRootUrl, '/health/auth-schema', { optional: true });
  }

  console.log('---------------------');
  console.log(`Summary: ${failCount === 0 ? 'PASS' : 'FAIL'} (${failCount} fail, ${warnCount} warn)`);

  if (failCount > 0) {
    process.exit(1);
  }
}

main();
