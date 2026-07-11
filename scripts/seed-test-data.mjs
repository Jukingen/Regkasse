#!/usr/bin/env node
/**
 * Development seed via HTTP API (full stack). Run from repo root:
 *   npm run seed:test-data
 *
 * Env:
 *   SEED_API_URL          default http://localhost:5184
 *   SEED_TENANT_SLUG      optional X-Tenant-Id (Development only), e.g. dev
 *   SEED_ADMIN_EMAIL      default admin@admin.com
 *   SEED_ADMIN_PASSWORD   default Admin123!
 *   SEED_PREFIX           default seed (resource name prefix)
 *   SEED_PRODUCT_COUNT    default 50
 *   SEED_REGISTER_COUNT   default 3
 *   SEED_USER_COUNT       default 5 (2× Cashier, 1× Manager, 1× Accountant, 1× Cashier)
 *   SEED_SALES_COUNT      default 100
 *   SEED_HISTORICAL_MONTHS default 3 (RKSV month markers)
 *   SEED_SKIP_SALES       set 1 to skip payment loop
 *   SEED_DRY_RUN          set 1 to log only
 *
 * Limits (API):
 *   - Normal sales always get server CreatedAt (today). Use Nullbeleg for past month timestamps.
 *   - Monatsbeleg POST only accepts the current Vienna calendar month.
 */

const GUEST_CUSTOMER_ID = '00000000-0000-0000-0000-000000000001';
const STEUERNUMMER = 'ATU12345678';

const config = {
  baseUrl: (process.env.SEED_API_URL || 'http://localhost:5184').replace(/\/$/, ''),
  tenantSlug: (process.env.SEED_TENANT_SLUG || '').trim(),
  adminEmail: process.env.SEED_ADMIN_EMAIL || 'admin@admin.com',
  adminPassword: process.env.SEED_ADMIN_PASSWORD || 'Admin123!',
  prefix: (process.env.SEED_PREFIX || 'seed').trim(),
  productCount: clampInt(process.env.SEED_PRODUCT_COUNT, 50, 1, 500),
  registerCount: clampInt(process.env.SEED_REGISTER_COUNT, 3, 1, 20),
  userCount: clampInt(process.env.SEED_USER_COUNT, 5, 0, 50),
  salesCount: clampInt(process.env.SEED_SALES_COUNT, 100, 0, 5000),
  historicalMonths: clampInt(process.env.SEED_HISTORICAL_MONTHS, 3, 1, 24),
  skipSales: process.env.SEED_SKIP_SALES === '1',
  dryRun: process.env.SEED_DRY_RUN === '1',
};

const PAYMENT_METHODS = ['cash', 'card'];
const TAX_TYPE_INT_TO_PAYMENT = {
  1: 'standard',
  2: 'reduced',
  3: 'special',
  4: 'zerorate',
};
const TAX_TYPE_INT_TO_RKSV = {
  1: 'Standard',
  2: 'Reduced',
  3: 'Special',
  4: 'Standard',
};

/** @type {{ token: string, userId: string, tenantSlug?: string }} */
let session = { token: '', userId: '', tenantSlug: '' };

const stats = {
  productsCreated: 0,
  registersCreated: 0,
  usersCreated: 0,
  salesCreated: 0,
  startbelegeCreated: 0,
  registersOpened: 0,
  monatsbelegeCreated: 0,
  nullbelegeCreated: 0,
  skipped: 0,
  errors: 0,
};

function clampInt(raw, defaultVal, min, max) {
  const n = Number.parseInt(String(raw ?? ''), 10);
  if (!Number.isFinite(n)) return defaultVal;
  return Math.min(max, Math.max(min, n));
}

function log(msg) {
  console.log(msg);
}

function warn(msg) {
  console.warn(`WARN ${msg}`);
}

function fail(msg) {
  console.error(`ERROR ${msg}`);
  process.exitCode = 1;
  throw new Error(msg);
}

function unwrapData(body) {
  if (body && typeof body === 'object' && 'data' in body && body.data !== undefined) {
    return body.data;
  }
  return body;
}

function productLabel(i) {
  return `${config.prefix} Produkt ${String(i).padStart(3, '0')}`;
}

function registerLabel(i) {
  return `${config.prefix} Kasse ${i}`;
}

function buildUserSpecs() {
  const roles = ['Manager', 'Cashier', 'Cashier', 'Accountant', 'Cashier'];
  const specs = [];
  for (let i = 0; i < config.userCount; i++) {
    const role = roles[i % roles.length];
    const n = i + 1;
    const slug = role.toLowerCase();
    specs.push({
      userName: `${config.prefix}.${slug}${n}`,
      email: `${config.prefix}.${slug}${n}@seed.local`,
      firstName: 'Seed',
      lastName: `${role} ${n}`,
      employeeNumber: `EMP-${config.prefix.toUpperCase()}-${String(n).padStart(3, '0')}`,
      role,
      password: 'SeedTest1!',
      taxNumber: `ATU${String(10_000_000 + n).slice(-8)}`,
    });
  }
  return specs;
}

async function api(method, path, { body, token, expectStatuses, headersExtra } = {}) {
  const url = `${config.baseUrl}${path.startsWith('/') ? path : `/${path}`}`;
  const headers = {
    Accept: 'application/json',
    'Content-Type': 'application/json',
    ...headersExtra,
  };
  if (token) headers.Authorization = `Bearer ${token}`;
  if (config.tenantSlug) headers['X-Tenant-Id'] = config.tenantSlug;

  if (config.dryRun && method !== 'GET') {
    log(`[dry-run] ${method} ${path}`);
    return { status: 200, body: {}, dryRun: true };
  }

  const res = await fetch(url, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  const text = await res.text();
  /** @type {unknown} */
  let parsed = null;
  if (text) {
    try {
      parsed = JSON.parse(text);
    } catch {
      parsed = text;
    }
  }

  const allowed = expectStatuses ?? [200, 201, 204];
  if (!allowed.includes(res.status)) {
    const detail =
      typeof parsed === 'object' && parsed !== null
        ? JSON.stringify(parsed).slice(0, 800)
        : String(parsed).slice(0, 800);
    fail(`${method} ${path} → ${res.status}: ${detail}`);
  }

  return { status: res.status, body: parsed };
}

async function login() {
  const { body } = await api('POST', '/api/Auth/login', {
    body: {
      email: config.adminEmail,
      password: config.adminPassword,
      clientApp: 'admin',
    },
    expectStatuses: [200, 400, 401, 403],
  });

  if (!body || typeof body !== 'object' || !('token' in body) || !body.token) {
    fail(
      `Login failed for ${config.adminEmail}. Check credentials and that the API is running at ${config.baseUrl}.`,
    );
  }

  session.token = body.token;
  session.userId = body.user?.id ?? '';
  session.tenantSlug = body.user?.tenantSlug ?? '';
  log(`Logged in as ${config.adminEmail} (tenant: ${session.tenantSlug || 'n/a'})`);
  if (config.tenantSlug && session.tenantSlug && config.tenantSlug !== session.tenantSlug) {
    warn(
      `SEED_TENANT_SLUG=${config.tenantSlug} differs from JWT tenant slug ${session.tenantSlug}. Data is scoped by JWT after login.`,
    );
  }
}

async function ensureHealth() {
  const { status } = await api('GET', '/api/health', { expectStatuses: [200, 503] });
  if (status !== 200) fail(`API health check failed (${status}). Start backend first.`);
  log(`API OK: ${config.baseUrl}`);
}

async function ensureCategory() {
  const name = `${config.prefix} Katalog`;
  const { body: listRaw } = await api('GET', '/api/admin/categories', { token: session.token });
  const list = Array.isArray(listRaw) ? listRaw : [];
  const existing = list.find((c) => c?.name === name);
  if (existing?.id) {
    log(`Category exists: ${name}`);
    return existing.id;
  }

  const { body: created } = await api('POST', '/api/admin/categories', {
    token: session.token,
    body: {
      name,
      description: 'Auto-created by seed:test-data',
      color: '#4a90d9',
      sortOrder: 0,
      vatRate: 20,
    },
    expectStatuses: [201, 400],
  });
  const id = created?.id;
  if (!id) fail('Category create did not return id');
  log(`Category created: ${name}`);
  return id;
}

async function listSeedProducts() {
  const items = [];
  let page = 1;
  const pageSize = 100;
  for (;;) {
    const { body } = await api(
      'GET',
      `/api/admin/products?pageNumber=${page}&pageSize=${pageSize}&isActive=all`,
      { token: session.token },
    );
    const data = unwrapData(body);
    const pageItems = data?.items ?? [];
    for (const p of pageItems) {
      if (typeof p?.name === 'string' && p.name.startsWith(`${config.prefix} Produkt`)) {
        items.push(p);
      }
    }
    const totalPages = data?.pagination?.totalPages ?? 1;
    if (page >= totalPages || pageItems.length === 0) break;
    page += 1;
  }
  return items;
}

async function ensureProducts(categoryId) {
  const existing = await listSeedProducts();
  const existingByIndex = new Map();
  for (const p of existing) {
    const m = /^.+ Produkt (\d+)$/.exec(p.name);
    if (m) existingByIndex.set(Number.parseInt(m[1], 10), p);
  }

  const created = [...existing];
  for (let i = 1; i <= config.productCount; i++) {
    if (existingByIndex.has(i)) continue;

    const taxType = [1, 1, 1, 2, 3][i % 5];
    const taxRate = taxType === 1 ? 20 : taxType === 2 ? 10 : taxType === 3 ? 13 : 20;
    const price = round2(1.5 + (i * 0.37) % 48 + randomJitter(0, 2));

    const payload = {
      name: productLabel(i),
      description: `Seed product ${i}`,
      price,
      taxType,
      taxRate,
      categoryId,
      category: `${config.prefix} Katalog`,
      stockQuantity: 500,
      minStockLevel: 5,
      unit: 'Stk',
      cost: round2(price * 0.55),
      barcode: `${config.prefix}-${String(i).padStart(6, '0')}`.slice(0, 20),
      isFiscalCompliant: true,
      isTaxable: true,
      rksvProductType: TAX_TYPE_INT_TO_RKSV[taxType] ?? 'Standard',
    };

    const { body } = await api('POST', '/api/admin/products', {
      token: session.token,
      body: payload,
      expectStatuses: [200, 201, 400],
    });
    const data = unwrapData(body);
    if (data?.id) {
      created.push(data);
      stats.productsCreated += 1;
    }
  }

  const all = await listSeedProducts();
  log(`Products ready: ${all.length} (created ${stats.productsCreated} this run)`);
  return all.slice(0, config.productCount);
}

async function listRegisters() {
  const { body } = await api('GET', '/api/CashRegister', { token: session.token });
  const registers = body?.registers;
  return Array.isArray(registers) ? registers : [];
}

async function ensureRegisters() {
  const all = await listRegisters();
  const seedRegs = all.filter((r) =>
    typeof r?.location === 'string' ? r.location.startsWith(`${config.prefix} Kasse`) : false,
  );

  const byIndex = new Map();
  for (const r of seedRegs) {
    const m = /^.+ Kasse (\d+)$/.exec(r.location);
    if (m) byIndex.set(Number.parseInt(m[1], 10), r);
  }

  const result = [...seedRegs];
  for (let i = 1; i <= config.registerCount; i++) {
    if (byIndex.has(i)) continue;
    const { body } = await api('POST', '/api/CashRegister', {
      token: session.token,
      body: {
        location: registerLabel(i),
        startingBalance: 200,
      },
      expectStatuses: [201, 400, 500],
    });
    const reg = body?.register;
    if (reg?.id) {
      result.push(reg);
      stats.registersCreated += 1;
    }
  }

  const refreshed = await listRegisters();
  const ordered = [];
  for (let i = 1; i <= config.registerCount; i++) {
    const reg = refreshed.find((r) => r.location === registerLabel(i));
    if (reg) ordered.push(reg);
  }

  log(`Cash registers ready: ${ordered.length} (created ${stats.registersCreated} this run)`);
  return ordered;
}

async function ensureUsers() {
  if (config.userCount <= 0) return [];

  const { body: usersRaw } = await api('GET', '/api/admin/users', {
    token: session.token,
    expectStatuses: [200, 403],
  });
  if (!Array.isArray(usersRaw)) {
    warn('Cannot list users (missing user.manage?). Skipping user seed.');
    return [];
  }

  const existingNames = new Set(usersRaw.map((u) => u.userName));
  const created = [];
  for (const spec of buildUserSpecs()) {
    if (existingNames.has(spec.userName)) {
      stats.skipped += 1;
      continue;
    }
    const { status } = await api('POST', '/api/admin/users', {
      token: session.token,
      body: spec,
      expectStatuses: [201, 400, 409],
    });
    if (status === 201) {
      created.push(spec);
      stats.usersCreated += 1;
      log(`User created: ${spec.userName} (${spec.role})`);
    }
  }
  log(`Users: ${created.length} created this run`);
  return created;
}

async function ensureStartbeleg(registerId) {
  const { status } = await api('POST', '/api/rksv/special-receipts/startbeleg', {
    token: session.token,
    body: {
      cashRegisterId: registerId,
      reason: `${config.prefix} seed startbeleg`,
      correlationId: `${config.prefix}-start`,
    },
    expectStatuses: [200, 400, 409],
  });
  if (status === 200) stats.startbelegeCreated += 1;
}

async function openRegister(registerId) {
  const { status } = await api('POST', `/api/CashRegister/${registerId}/open`, {
    token: session.token,
    body: { openingBalance: 100 },
    expectStatuses: [200, 400, 409],
  });
  if (status === 200) stats.registersOpened += 1;
}

async function prepareRegisters(registers) {
  for (const reg of registers) {
    if (!reg?.id) continue;
    await ensureStartbeleg(reg.id);
    await openRegister(reg.id);
  }
}

function round2(n) {
  return Math.round(n * 100) / 100;
}

function randomJitter(min, max) {
  return min + Math.random() * (max - min);
}

function pickRandom(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

function getViennaYearMonth(date = new Date()) {
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Europe/Vienna',
    year: 'numeric',
    month: 'numeric',
  }).formatToParts(date);
  const year = Number(parts.find((p) => p.type === 'year')?.value);
  const month = Number(parts.find((p) => p.type === 'month')?.value);
  return { year, month };
}

function addMonths(year, month, delta) {
  let y = year;
  let m = month + delta;
  while (m < 1) {
    m += 12;
    y -= 1;
  }
  while (m > 12) {
    m -= 12;
    y += 1;
  }
  return { year: y, month: m };
}

async function createSale(product, registerId, index) {
  const taxType = product.taxType ?? 1;
  const paymentTax = TAX_TYPE_INT_TO_PAYMENT[taxType] ?? 'standard';
  const qty = 1 + (index % 3);
  const unitPrice = Number(product.price);
  const totalAmount = round2(unitPrice * qty);
  const method = PAYMENT_METHODS[index % PAYMENT_METHODS.length];

  const payload = {
    customerId: GUEST_CUSTOMER_ID,
    items: [
      {
        productId: product.id,
        quantity: qty,
        taxType: paymentTax,
      },
    ],
    payment: {
      method,
      tseRequired: true,
    },
    tableNumber: 1 + (index % 12),
    totalAmount,
    steuernummer: STEUERNUMMER,
    cashRegisterId: registerId,
    notes: `${config.prefix} sale #${index}`,
    idempotencyKey: `${config.prefix}-sale-${new Date().toISOString().slice(0, 10)}-${index}`,
  };

  const { status, body } = await api('POST', '/api/pos/payment', {
    token: session.token,
    body: payload,
    expectStatuses: [201, 202, 400, 403, 409],
    headersExtra: { 'X-Regkasse-Payment-Contract': 'v2' },
  });

  if (status === 201 || status === 202) {
    stats.salesCreated += 1;
    return body;
  }
  stats.errors += 1;
  warn(`Sale ${index} failed (${status})`);
  return null;
}

async function runSales(products, registers) {
  if (config.skipSales || config.salesCount <= 0) {
    log('Skipping sales (SEED_SKIP_SALES or SEED_SALES_COUNT=0)');
    return;
  }
  if (products.length === 0 || registers.length === 0) {
    warn('No products or registers — cannot create sales.');
    return;
  }

  const primaryRegister = registers[0].id;
  log(`Creating ${config.salesCount} sales on register ${primaryRegister}…`);

  for (let i = 1; i <= config.salesCount; i++) {
    const product = products[(i - 1) % products.length];
    await createSale(product, primaryRegister, i);
    if (i % 25 === 0) log(`  …${i}/${config.salesCount} sales`);
  }
  log(`Sales done: ${stats.salesCreated} succeeded`);
}

async function seedMonthlyRksv(registers) {
  if (registers.length === 0) return;
  const registerId = registers[0].id;
  const { year: currentYear, month: currentMonth } = getViennaYearMonth();

  for (let offset = 0; offset < config.historicalMonths; offset++) {
    const { year, month } = addMonths(currentYear, currentMonth, -offset);
    const isCurrentMonth = offset === 0;

    if (isCurrentMonth) {
      const { status } = await api('POST', '/api/rksv/special-receipts/monatsbeleg', {
        token: session.token,
        body: {
          cashRegisterId: registerId,
          year,
          month,
          reason: `${config.prefix} seed monatsbeleg`,
        },
        expectStatuses: [200, 400, 409],
      });
      if (status === 200) {
        stats.monatsbelegeCreated += 1;
        log(`Monatsbeleg ${year}-${String(month).padStart(2, '0')} created`);
      } else if (status === 409) {
        log(`Monatsbeleg ${year}-${String(month).padStart(2, '0')} already exists`);
      }
    } else {
      const { status } = await api('POST', '/api/rksv/special-receipts/nullbeleg', {
        token: session.token,
        body: {
          cashRegisterId: registerId,
          year,
          month,
          reason: `${config.prefix} seed nullbeleg (historical month marker)`,
        },
        expectStatuses: [200, 400, 409],
      });
      if (status === 200) {
        stats.nullbelegeCreated += 1;
        log(`Nullbeleg ${year}-${String(month).padStart(2, '0')} created (backdated month-end)`);
      } else if (status === 409) {
        log(`Nullbeleg ${year}-${String(month).padStart(2, '0')} already exists`);
      }
    }
  }

  if (config.historicalMonths > 1) {
    warn(
      'Past months use Nullbeleg (API allows month-end timestamps). Monatsbeleg API only accepts the current Vienna month.',
    );
  }
}

function printSummary() {
  log('');
  log('── Seed summary ──');
  log(`  Products created:     ${stats.productsCreated}`);
  log(`  Registers created:    ${stats.registersCreated}`);
  log(`  Registers opened:     ${stats.registersOpened}`);
  log(`  Startbelege created:  ${stats.startbelegeCreated}`);
  log(`  Users created:        ${stats.usersCreated}`);
  log(`  Sales created:        ${stats.salesCreated}`);
  log(`  Monatsbelege created: ${stats.monatsbelegeCreated}`);
  log(`  Nullbelege created:   ${stats.nullbelegeCreated}`);
  log(`  Skipped (existing):   ${stats.skipped}`);
  log(`  Errors (sales):       ${stats.errors}`);
  log('');
  log('Note: payment CreatedAt is always server time (today). Nullbeleg covers past months in reports.');
}

async function main() {
  log(`Regkasse test-data seed → ${config.baseUrl}`);
  if (config.dryRun) warn('SEED_DRY_RUN=1 — mutating requests are not sent');

  await ensureHealth();
  await login();

  const categoryId = await ensureCategory();
  const products = await ensureProducts(categoryId);
  const registers = await ensureRegisters();
  await ensureUsers();
  await prepareRegisters(registers);
  await runSales(products, registers);
  await seedMonthlyRksv(registers);

  printSummary();
  if (stats.errors > 0) process.exitCode = 1;
}

main().catch((err) => {
  if (!process.exitCode) process.exitCode = 1;
  console.error(err instanceof Error ? err.message : err);
});
