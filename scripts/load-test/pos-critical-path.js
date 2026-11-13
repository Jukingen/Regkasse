// POS critical path: login, add 5 products, cash payment, close the cash register.
// One k6 VU is one cashier. Default is 50 VUs, one iteration each.
//
// Run from the repo root. See scripts/load-test/README.md.
// This file talks to a real API and creates fiscal receipts. It refuses non-local hosts
// unless LOAD_TEST_ALLOW_REMOTE=1.

import http from 'k6/http';
import { check } from 'k6';
import { Trend } from 'k6/metrics';

const baseUrl = (__ENV.BASE_URL || 'http://localhost:5184').replace(/\/$/, '');
const vuCount = positiveInt(__ENV.VUS, 50);
const productCount = positiveInt(__ENV.PRODUCT_COUNT, 5);
const p95BudgetMs = positiveInt(__ENV.P95_MS, 3000);
const maxErrorRate = __ENV.MAX_ERROR_RATE || '0.01';
const customerId = __ENV.CUSTOMER_ID || '00000000-0000-0000-0000-000000000001';
const tableBase = positiveInt(__ENV.TABLE_BASE, 9000);
const cashiersPath = __ENV.CASHIERS_FILE || 'cashiers.example.json';

assertLocalOrExplicit(baseUrl);
if (Number.isNaN(Number(maxErrorRate)) || Number(maxErrorRate) < 0 || Number(maxErrorRate) >= 1) {
  throw new Error('MAX_ERROR_RATE must be a number between 0 and 1.');
}

const cashiers = JSON.parse(open(cashiersPath));
if (!Array.isArray(cashiers) || cashiers.length < vuCount) {
  throw new Error(
    `CASHIERS_FILE must list at least ${vuCount} cashiers (found ${Array.isArray(cashiers) ? cashiers.length : 0}).`,
  );
}

const registerIds = new Set();
for (let i = 0; i < vuCount; i += 1) {
  const row = cashiers[i];
  if (!row || !row.loginIdentifier || !row.password || !row.cashRegisterId) {
    throw new Error(`Cashier row ${i + 1} needs loginIdentifier, password, and cashRegisterId.`);
  }
  if (registerIds.has(row.cashRegisterId)) {
    throw new Error(`cashRegisterId ${row.cashRegisterId} is used more than once. Each cashier needs their own register.`);
  }
  registerIds.add(row.cashRegisterId);
}

const productIds = (__ENV.PRODUCT_IDS || '')
  .split(',')
  .map((id) => id.trim())
  .filter(Boolean);
if (productIds.length > 0 && productIds.length < productCount) {
  throw new Error(`PRODUCT_IDS has ${productIds.length} ids; PRODUCT_COUNT is ${productCount}.`);
}

const isLocal = isLocalHost(hostOf(baseUrl));
const tenantSlug = __ENV.TENANT_SLUG || (isLocal ? 'dev' : '');

const saleMs = new Trend('pos_sale_ms', true);

export const options = {
  scenarios: {
    cashiers: {
      executor: 'per-vu-iterations',
      vus: vuCount,
      iterations: 1,
      maxDuration: __ENV.MAX_DURATION || '10m',
    },
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(95)', 'p(99)', 'max'],
  thresholds: {
    http_req_failed: [`rate<${maxErrorRate}`],
    checks: [`rate>${(1 - Number(maxErrorRate)).toFixed(4)}`],
    pos_sale_ms: [`p(95)<${p95BudgetMs}`],
  },
};

export default function () {
  const cashier = cashiers[__VU - 1];
  const started = Date.now();
  let succeeded = false;
  try {
    runCashier(cashier);
    succeeded = true;
  } finally {
    if (succeeded) {
      saleMs.add(Date.now() - started);
    }
  }
}

export function handleSummary(data) {
  const duration = data.metrics.http_req_duration?.values ?? {};
  const sale = data.metrics.pos_sale_ms?.values ?? {};
  const errorRate = data.metrics.http_req_failed?.values?.rate ?? 0;
  const checkRate = data.metrics.checks?.values?.rate ?? 0;
  const lines = [
    'POS critical path',
    `vus=${vuCount}`,
    `requests=${data.metrics.http_reqs?.values?.count ?? 0}`,
    `error_rate=${(errorRate * 100).toFixed(2)}%`,
    `check_pass_rate=${(checkRate * 100).toFixed(2)}%`,
    `http_p95_ms=${formatMs(duration['p(95)'])}`,
    `sale_p95_ms=${formatMs(sale['p(95)'])}`,
    `sale_samples=${sale.count ?? 0}`,
  ];

  for (const key of Object.keys(data.metrics)) {
    if (!key.startsWith('http_req_duration{')) continue;
    const values = data.metrics[key].values;
    lines.push(`${key} p95_ms=${formatMs(values['p(95)'])}`);
  }

  lines.push('');
  return { stdout: `${lines.join('\n')}\n` };
}

function runCashier(cashier) {
  const login = expectJson(
    http.post(
      `${baseUrl}/api/Auth/login`,
      JSON.stringify({
        loginIdentifier: cashier.loginIdentifier,
        password: cashier.password,
        clientApp: 'pos',
      }),
      requestParams(null, 'login'),
    ),
    'login',
    [200],
  );
  if (!login.token) {
    throw new Error('login did not return a token (second factor or locked account).');
  }

  const headers = requestParams(login.token, 'shift_current');
  const current = expectJson(http.get(`${baseUrl}/api/pos/shift/current`, headers), 'shift_current', [200]);

  let cashRegisterId = cashier.cashRegisterId;
  if (!current.hasActiveShift) {
    expectJson(
      http.post(
        `${baseUrl}/api/pos/shift/start`,
        JSON.stringify({ cashRegisterId, startBalance: 0 }),
        requestParams(login.token, 'shift_start'),
      ),
      'shift_start',
      [200],
    );
  } else if (current.shift && current.shift.cashRegisterId) {
    cashRegisterId = current.shift.cashRegisterId;
  }

  const ids = productIds.length > 0 ? productIds.slice(0, productCount) : loadProductIds(login.token);
  const tableNumber = tableBase + __VU;
  let cart = null;
  for (let i = 0; i < productCount; i += 1) {
    const added = expectJson(
      http.post(
        `${baseUrl}/api/pos/cart/add-item`,
        JSON.stringify({
          productId: ids[i],
          quantity: 1,
          tableNumber,
          cashRegisterId,
        }),
        requestParams(login.token, 'add_product'),
      ),
      'add_product',
      [200],
    );
    cart = added.cart;
  }

  if (!cart || !Array.isArray(cart.items) || cart.items.length === 0) {
    throw new Error('cart is empty after adding products.');
  }

  const totalAmount = Number(cart.grandTotalGross);
  if (!Number.isFinite(totalAmount) || totalAmount < 0.01) {
    throw new Error('cart grandTotalGross is missing or below the payment minimum.');
  }

  expectJson(
    http.post(
      `${baseUrl}/api/pos/payment`,
      JSON.stringify({
        customerId,
        items: cart.items.map((item) => ({
          productId: item.productId,
          quantity: item.quantity,
          taxType: taxTypeName(item.taxType),
        })),
        payment: { method: 'cash', tseRequired: true, amount: totalAmount },
        tableNumber,
        totalAmount,
        cashRegisterId,
        cashGiven: totalAmount,
        idempotencyKey: `k6-${__VU}-${Date.now()}`,
      }),
      requestParams(login.token, 'pay'),
    ),
    'pay',
    [201],
  );

  expectJson(
    http.post(
      `${baseUrl}/api/pos/shift/end`,
      JSON.stringify({
        endBalance: totalAmount,
        notes: 'k6 pos critical path',
      }),
      requestParams(login.token, 'close_register'),
    ),
    'close_register',
    [200],
  );
}

function loadProductIds(token) {
  const body = expectJson(
    http.get(`${baseUrl}/api/pos?pageNumber=1&pageSize=100`, requestParams(token, 'catalog')),
    'catalog',
    [200],
  );
  const items = (body.data && body.data.items) || body.items || [];
  const ids = [];
  for (const product of items) {
    if (!product || !product.id) continue;
    if (product.hasVariants || product.isSellableAddOn || product.imeiTracked) continue;
    if (Number(product.price) < 0.01) continue;
    ids.push(product.id);
    if (ids.length === productCount) break;
  }
  if (ids.length < productCount) {
    throw new Error(
      `Need ${productCount} simple catalog products. Found ${ids.length}. Set PRODUCT_IDS to override.`,
    );
  }
  return ids;
}

function requestParams(token, step) {
  const headers = { Accept: 'application/json', 'Content-Type': 'application/json' };
  if (token) headers.Authorization = `Bearer ${token}`;
  if (tenantSlug) headers['X-Tenant-Id'] = tenantSlug;
  return { headers, tags: { step }, timeout: '60s' };
}

function expectJson(res, step, allowed) {
  const ok = allowed.indexOf(res.status) !== -1;
  check(res, { [`${step} status`]: () => ok });
  if (!ok) {
    throw new Error(`${step} HTTP ${res.status}: ${String(res.body || '').slice(0, 400)}`);
  }
  if (!res.body) return {};
  return res.json();
}

function taxTypeName(value) {
  const n = Number(value);
  if (n === 2) return 'reduced';
  if (n === 3) return 'special';
  if (n === 4) return 'zerorate';
  if (typeof value === 'string' && value.trim()) return value.trim().toLowerCase();
  return 'standard';
}

function positiveInt(raw, fallback) {
  const n = Number(raw || fallback);
  if (!Number.isInteger(n) || n < 1) {
    throw new Error(`Expected a positive integer, received ${raw}.`);
  }
  return n;
}

function assertLocalOrExplicit(url) {
  if (!/^https?:\/\/[^/]+$/i.test(url)) {
    throw new Error('BASE_URL must be an origin such as http://localhost:5184, with no path.');
  }
  if (!isLocalHost(hostOf(url)) && __ENV.LOAD_TEST_ALLOW_REMOTE !== '1') {
    throw new Error(
      `Refusing ${url}. This scenario creates receipts and closes cash registers. Set LOAD_TEST_ALLOW_REMOTE=1 only for a lab you own.`,
    );
  }
}

function hostOf(url) {
  const authority = (url.split('://')[1] || '').split('/')[0];
  if (authority.startsWith('[')) {
    const end = authority.indexOf(']');
    return end > 1 ? authority.slice(1, end).toLowerCase() : '';
  }
  return authority.split(':')[0].toLowerCase();
}

function isLocalHost(host) {
  return host === 'localhost' || host === '127.0.0.1' || host === '::1';
}

function formatMs(value) {
  return Number(value || 0).toFixed(1);
}
