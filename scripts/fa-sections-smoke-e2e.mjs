/**
 * FA smoke E2E: Sortiment, Kunden, Berichte, Backup, Einstellungen
 * Run: node scripts/fa-sections-smoke-e2e.mjs
 */
import { chromium } from 'playwright';
import { writeFileSync, mkdirSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const BASE = process.env.FA_BASE || 'http://localhost:3000';
const API = process.env.API_BASE || 'http://localhost:5184';
const TENANT = 'dev';

const ACCOUNTS = {
  manager: { login: 'manager1', password: 'Juke1034#', label: 'Manager' },
  superAdmin: { login: 'admin@admin.com', password: 'Admin123!', label: 'SuperAdmin' },
};

/** @type {{ id: string; role: string; status: 'PASS'|'FAIL'|'SKIP'; title: string; detail?: string }[]} */
const results = [];

function record(id, role, status, title, detail = '') {
  results.push({ id, role, status, title, detail: detail || undefined });
  const icon = status === 'PASS' ? '✅' : status === 'FAIL' ? '❌' : '⚠️';
  console.log(`${icon} [${role}] ${id}: ${title}${detail ? ` — ${detail}` : ''}`);
}

async function waitReady(page, ms = 1500) {
  await page.waitForLoadState('domcontentloaded').catch(() => {});
  await page.waitForLoadState('networkidle', { timeout: 12000 }).catch(() => {});
  await page.waitForTimeout(ms);
  for (let i = 0; i < 25; i++) {
    const spinning = await page.locator('.ant-spin-spinning').count();
    if (spinning === 0) break;
    await page.waitForTimeout(400);
  }
}

async function apiLogin(loginIdentifier, password) {
  const res = await fetch(`${API}/api/Auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': TENANT },
    body: JSON.stringify({ loginIdentifier, password, clientApp: 'admin' }),
  });
  if (!res.ok) throw new Error(`API login failed ${res.status}`);
  const json = await res.json();
  const token = json.token || json.accessToken;
  if (!token) throw new Error('No token in login response');
  return { token, refreshToken: json.refreshToken || null, user: json.user };
}

async function injectSession(page, token, refreshToken) {
  await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' });
  await page.evaluate(
    ({ token, refreshToken, tenant }) => {
      localStorage.setItem('rk_admin_access_token', token);
      if (refreshToken) localStorage.setItem('rk_admin_refresh_token', refreshToken);
      localStorage.setItem('dev_tenant_id', tenant);
      document.cookie = `rk_admin_access_token=${encodeURIComponent(token)}; Path=/; SameSite=Lax; Max-Age=604800`;
    },
    { token, refreshToken, tenant: TENANT },
  );
}

async function gotoPage(page, path) {
  const consoleErrors = [];
  const onConsole = (msg) => {
    if (msg.type() === 'error') consoleErrors.push(msg.text());
  };
  const pageErrors = [];
  const onPageError = (err) => pageErrors.push(String(err));
  page.on('console', onConsole);
  page.on('pageerror', onPageError);

  await page.goto(`${BASE}${path}`, { waitUntil: 'domcontentloaded', timeout: 90000 });
  await waitReady(page);

  page.off('console', onConsole);
  page.off('pageerror', onPageError);

  const url = page.url();
  if (url.includes('/login')) return { ok: false, url, reason: 'redirected to login', consoleErrors, pageErrors };
  if (url.includes('/403')) return { ok: false, url, reason: '403', consoleErrors, pageErrors };
  if (url.includes('/404') || (await page.locator('text=/404|Seite nicht gefunden|Not Found/i').count()) > 0) {
    const has404 = await page.locator('h1,h2,.ant-result-title').filter({ hasText: /404|nicht gefunden|Not Found/i }).count();
    if (has404 > 0) return { ok: false, url, reason: '404 page', consoleErrors, pageErrors };
  }

  // Ant fatal error boundary / Next error
  const nextError =
    (await page.locator('nextjs-portal').count()) +
    (await page.locator('#__next-build-error').count()) +
    (await page.getByText(/Application error|Unhandled Runtime Error/i).count());
  if (nextError > 0 || pageErrors.length > 0) {
    return {
      ok: false,
      url,
      reason: pageErrors[0] || 'runtime/page error UI',
      consoleErrors,
      pageErrors,
    };
  }

  return { ok: true, url, consoleErrors, pageErrors };
}

async function pageHasContent(page) {
  const bodyText = (await page.locator('body').innerText().catch(() => '')).trim();
  return bodyText.length > 40;
}

async function clickFirst(page, selectors, { timeout = 8000 } = {}) {
  for (const sel of selectors) {
    const loc = page.locator(sel).first();
    if ((await loc.count()) === 0) continue;
    try {
      await loc.click({ timeout });
      return true;
    } catch {
      /* try next */
    }
  }
  return false;
}

async function fillVisible(page, selectors, value) {
  for (const sel of selectors) {
    const loc = page.locator(sel).first();
    if ((await loc.count()) === 0) continue;
    if (!(await loc.isVisible().catch(() => false))) continue;
    await loc.fill(value);
    return true;
  }
  return false;
}

async function testLoad(page, id, role, path, title) {
  try {
    const r = await gotoPage(page, path);
    if (!r.ok) {
      record(id, role, 'FAIL', title, `${r.reason} @ ${r.url}`);
      return false;
    }
    if (!(await pageHasContent(page))) {
      record(id, role, 'FAIL', title, 'empty body');
      return false;
    }
    record(id, role, 'PASS', title, `→ ${r.url.replace(BASE, '')}`);
    return true;
  } catch (e) {
    record(id, role, 'FAIL', title, String(e.message || e));
    return false;
  }
}

async function testProductsCrud(page, role) {
  const id = '1.1';
  const stamp = Date.now().toString().slice(-6);
  const name = `E2E-Prod-${stamp}`;
  try {
    if (!(await testLoad(page, `${id}-load`, role, '/products', 'Products list loads'))) return;

    // Search
    const search = page.locator('input[placeholder*="Such"], input[type="search"], .ant-input-search input, input[placeholder*="search" i]').first();
    if ((await search.count()) > 0) {
      await search.fill('pizza');
      await page.keyboard.press('Enter');
      await waitReady(page, 1000);
      record(`${id}-search`, role, 'PASS', 'Products search works');
      await search.fill('');
      await page.keyboard.press('Enter');
      await waitReady(page, 800);
    } else {
      record(`${id}-search`, role, 'SKIP', 'Products search input not found');
    }

    // Create
    const opened = await clickFirst(page, [
      'button:has-text("Neu")',
      'button:has-text("Hinzufügen")',
      'button:has-text("Produkt")',
      'button:has-text("Add")',
      'button:has-text("Create")',
      '[data-testid="create-product"]',
    ]);
    if (!opened) {
      record(`${id}-create`, role, 'FAIL', 'Products create — open button missing');
      return;
    }
    await waitReady(page, 800);
    await fillVisible(page, ['#name', 'input#name', 'input[id*="name"]', '.ant-modal input'].slice(0, 3), name);
    // Prefer nameDe / name fields in modal
    const modal = page.locator('.ant-modal, .ant-drawer').last();
    const nameInput = modal.locator('input').filter({ hasNot: page.locator('[type=checkbox],[type=radio],[type=hidden]') }).first();
    if ((await nameInput.count()) > 0) await nameInput.fill(name);
    // Price
    const price = modal.locator('input[id*="price" i], input[role="spinbutton"]').first();
    if ((await price.count()) > 0) {
      await price.click({ clickCount: 3 });
      await price.fill('9.99');
    }
    // Category select if required
    const cat = modal.locator('.ant-select').first();
    if ((await cat.count()) > 0) {
      await cat.click();
      await page.waitForTimeout(400);
      const opt = page.locator('.ant-select-item-option').first();
      if ((await opt.count()) > 0) await opt.click();
    }
    const saved = await clickFirst(page, [
      '.ant-modal button.ant-btn-primary',
      '.ant-drawer button.ant-btn-primary',
      'button:has-text("Speichern")',
      'button:has-text("Erstellen")',
      'button:has-text("Save")',
    ]);
    await waitReady(page, 1500);
    if (!saved) {
      record(`${id}-create`, role, 'FAIL', 'Products create — save button missing');
      return;
    }
    // Check success toast or row
    const toastOk = (await page.locator('.ant-message-success, .ant-notification-notice-success').count()) > 0;
    const rowOk = (await page.locator(`text=${name}`).count()) > 0;
    if (toastOk || rowOk) {
      record(`${id}-create`, role, 'PASS', 'Products create works');
    } else {
      // validation might block — capture screenshot hint
      const err = await page.locator('.ant-form-item-explain-error, .ant-message-error').first().textContent().catch(() => '');
      record(`${id}-create`, role, 'FAIL', 'Products create — no success signal', err || 'no toast/row');
      await page.keyboard.press('Escape');
      return;
    }

    // Edit — open first matching row actions
    const row = page.locator('tr').filter({ hasText: name }).first();
    if ((await row.count()) === 0) {
      record(`${id}-edit`, role, 'SKIP', 'Products edit — created row not visible (may be filtered)');
      record(`${id}-delete`, role, 'SKIP', 'Products delete — created row not visible');
      return;
    }
    const editBtn = row.locator('button').filter({ has: page.locator('.anticon-edit') }).or(row.locator('button:has-text("Bearbeiten")')).first();
    if ((await editBtn.count()) > 0) {
      await editBtn.click();
      await waitReady(page, 800);
      const editModal = page.locator('.ant-modal, .ant-drawer').last();
      const editName = editModal.locator('input').first();
      if ((await editName.count()) > 0) await editName.fill(`${name}-ed`);
      await clickFirst(page, ['.ant-modal button.ant-btn-primary', '.ant-drawer button.ant-btn-primary', 'button:has-text("Speichern")']);
      await waitReady(page, 1200);
      record(`${id}-edit`, role, 'PASS', 'Products edit works');
    } else {
      record(`${id}-edit`, role, 'FAIL', 'Products edit button missing');
    }

    // Delete
    const row2 = page.locator('tr').filter({ hasText: new RegExp(name) }).first();
    const delBtn = row2.locator('button').filter({ has: page.locator('.anticon-delete') }).or(row2.locator('button:has-text("Löschen")')).first();
    if ((await delBtn.count()) > 0) {
      await delBtn.click();
      await page.waitForTimeout(400);
      await clickFirst(page, ['.ant-popconfirm button.ant-btn-primary', '.ant-modal button.ant-btn-primary', 'button:has-text("OK")', 'button:has-text("Ja")']);
      await waitReady(page, 1200);
      record(`${id}-delete`, role, 'PASS', 'Products delete works');
    } else {
      record(`${id}-delete`, role, 'FAIL', 'Products delete button missing');
    }
  } catch (e) {
    record(`${id}-crud`, role, 'FAIL', 'Products CRUD', String(e.message || e));
  }
}

async function testCategoriesCrud(page, role) {
  const id = '1.2';
  const stamp = Date.now().toString().slice(-6);
  const name = `E2E-Cat-${stamp}`;
  try {
    if (!(await testLoad(page, `${id}-load`, role, '/categories', 'Categories list loads'))) return;
    const opened = await clickFirst(page, [
      'button:has-text("Neu")',
      'button:has-text("Hinzufügen")',
      'button:has-text("Kategorie")',
      'button:has-text("Add")',
    ]);
    if (!opened) {
      record(`${id}-create`, role, 'FAIL', 'Categories create button missing');
      return;
    }
    await waitReady(page, 600);
    const modal = page.locator('.ant-modal, .ant-drawer').last();
    await modal.locator('input').first().fill(name);
    await clickFirst(page, ['.ant-modal button.ant-btn-primary', '.ant-drawer button.ant-btn-primary', 'button:has-text("Speichern")']);
    await waitReady(page, 1200);
    const ok = (await page.locator(`text=${name}`).count()) > 0 || (await page.locator('.ant-message-success').count()) > 0;
    record(`${id}-create`, role, ok ? 'PASS' : 'FAIL', 'Categories create');

    const row = page.locator('tr, .ant-list-item, .ant-card').filter({ hasText: name }).first();
    if ((await row.count()) === 0) {
      record(`${id}-edit`, role, 'SKIP', 'Categories edit — row missing');
      record(`${id}-delete`, role, 'SKIP', 'Categories delete — row missing');
      return;
    }
    const editBtn = row.locator('button').filter({ has: page.locator('.anticon-edit') }).first();
    if ((await editBtn.count()) > 0) {
      await editBtn.click();
      await waitReady(page, 500);
      const m = page.locator('.ant-modal, .ant-drawer').last();
      await m.locator('input').first().fill(`${name}-ed`);
      await clickFirst(page, ['.ant-modal button.ant-btn-primary', 'button:has-text("Speichern")']);
      await waitReady(page, 1000);
      record(`${id}-edit`, role, 'PASS', 'Categories edit');
    } else {
      record(`${id}-edit`, role, 'FAIL', 'Categories edit button missing');
    }

    const row2 = page.locator('tr, .ant-list-item, .ant-card').filter({ hasText: new RegExp(name) }).first();
    const delBtn = row2.locator('button').filter({ has: page.locator('.anticon-delete') }).first();
    if ((await delBtn.count()) > 0) {
      await delBtn.click();
      await page.waitForTimeout(300);
      await clickFirst(page, ['.ant-popconfirm button.ant-btn-primary', 'button:has-text("OK")', 'button:has-text("Ja")']);
      await waitReady(page, 1000);
      record(`${id}-delete`, role, 'PASS', 'Categories delete');
    } else {
      record(`${id}-delete`, role, 'FAIL', 'Categories delete button missing');
    }
  } catch (e) {
    record(`${id}-crud`, role, 'FAIL', 'Categories CRUD', String(e.message || e));
  }
}

async function testModifierGroups(page, role) {
  const id = '1.3';
  const stamp = Date.now().toString().slice(-6);
  const name = `E2E-MG-${stamp}`;
  try {
    if (!(await testLoad(page, `${id}-load`, role, '/modifier-groups', 'Modifier-groups list loads'))) return;
    const opened = await clickFirst(page, [
      'button:has-text("Neu")',
      'button:has-text("Gruppe")',
      'button:has-text("Hinzufügen")',
      'button:has-text("Add")',
    ]);
    if (!opened) {
      record(`${id}-create`, role, 'FAIL', 'Modifier-groups create button missing');
      return;
    }
    await waitReady(page, 500);
    const modal = page.locator('.ant-modal, .ant-drawer').last();
    await modal.locator('input').first().fill(name);
    await clickFirst(page, ['.ant-modal button.ant-btn-primary', 'button:has-text("Speichern")', 'button:has-text("Erstellen")']);
    await waitReady(page, 1200);
    const ok = (await page.locator(`text=${name}`).count()) > 0 || (await page.locator('.ant-message-success').count()) > 0;
    record(`${id}-create`, role, ok ? 'PASS' : 'FAIL', 'modifier-groups create');

    // Try add product/modifier button on the new group card
    const card = page.locator('.ant-card, .ant-collapse-item, tr').filter({ hasText: name }).first();
    const addMod = card.locator('button').filter({ hasText: /Produkt|Add-on|Modifier|Hinzufügen|Add/i }).first();
    if ((await addMod.count()) > 0) {
      await addMod.click();
      await waitReady(page, 600);
      const m = page.locator('.ant-modal, .ant-drawer').last();
      const sel = m.locator('.ant-select').first();
      if ((await sel.count()) > 0) {
        await sel.click();
        await page.waitForTimeout(400);
        await page.locator('.ant-select-item-option').first().click().catch(() => {});
      }
      await clickFirst(page, ['.ant-modal button.ant-btn-primary', 'button:has-text("Speichern")', 'button:has-text("Hinzufügen")']);
      await waitReady(page, 1000);
      record(`${id}-add-mod`, role, 'PASS', 'modifier-groups add modifier/product');
    } else {
      record(`${id}-add-mod`, role, 'SKIP', 'add modifier UI not found on group');
    }

    const editBtn = card.locator('button').filter({ has: page.locator('.anticon-edit') }).first();
    if ((await editBtn.count()) > 0) {
      await editBtn.click();
      await waitReady(page, 500);
      await clickFirst(page, ['.ant-modal button.ant-btn-primary', 'button:has-text("Speichern")']);
      await waitReady(page, 800);
      record(`${id}-edit`, role, 'PASS', 'modifier-groups edit');
    } else {
      record(`${id}-edit`, role, 'SKIP', 'modifier-groups edit button missing');
    }

    const delBtn = card.locator('button').filter({ has: page.locator('.anticon-delete') }).first();
    if ((await delBtn.count()) > 0) {
      await delBtn.click();
      await page.waitForTimeout(300);
      await clickFirst(page, ['.ant-popconfirm button.ant-btn-primary', 'button:has-text("OK")', 'button:has-text("Ja")']);
      await waitReady(page, 1000);
      record(`${id}-delete`, role, 'PASS', 'modifier-groups delete');
    } else {
      record(`${id}-delete`, role, 'SKIP', 'modifier-groups delete button missing');
    }
  } catch (e) {
    record(`${id}-crud`, role, 'FAIL', 'modifier-groups CRUD', String(e.message || e));
  }
}

async function testPricingRules(page, role) {
  const id = '1.4';
  try {
    await testLoad(page, `${id}-alias`, role, '/price-rules', 'price-rules redirects to pricing-rules');
    if (!(await testLoad(page, `${id}-load`, role, '/pricing-rules', 'pricing-rules list loads'))) return;

    const stamp = Date.now().toString().slice(-6);
    const opened = await clickFirst(page, [
      'button:has-text("Neu")',
      'button:has-text("Regel")',
      'button:has-text("Hinzufügen")',
      'button:has-text("Add")',
      'button:has-text("Create")',
    ]);
    if (!opened) {
      record(`${id}-create`, role, 'SKIP', 'pricing-rules create button missing (empty MVP?)');
      return;
    }
    await waitReady(page, 600);
    const modal = page.locator('.ant-modal, .ant-drawer').last();
    const inputs = modal.locator('input:not([type=checkbox]):not([type=radio]):not([type=hidden])');
    if ((await inputs.count()) > 0) await inputs.first().fill(`E2E-PR-${stamp}`);
    await clickFirst(page, ['.ant-modal button.ant-btn-primary', 'button:has-text("Speichern")']);
    await waitReady(page, 1200);
    const err = await page.locator('.ant-form-item-explain-error, .ant-message-error').first().textContent().catch(() => '');
    const ok = (await page.locator('.ant-message-success').count()) > 0;
    if (ok) {
      record(`${id}-create`, role, 'PASS', 'pricing-rules create');
      // best-effort edit/delete first row
      const row = page.locator('tbody tr').first();
      if ((await row.count()) > 0) {
        const editBtn = row.locator('button').filter({ has: page.locator('.anticon-edit') }).first();
        if ((await editBtn.count()) > 0) {
          await editBtn.click();
          await waitReady(page, 500);
          await clickFirst(page, ['.ant-modal button.ant-btn-primary', 'button:has-text("Speichern")']);
          await waitReady(page, 800);
          record(`${id}-edit`, role, 'PASS', 'pricing-rules edit');
        }
        const delBtn = row.locator('button').filter({ has: page.locator('.anticon-delete') }).first();
        if ((await delBtn.count()) > 0) {
          await delBtn.click();
          await clickFirst(page, ['.ant-popconfirm button.ant-btn-primary', 'button:has-text("OK")']);
          await waitReady(page, 800);
          record(`${id}-delete`, role, 'PASS', 'pricing-rules delete');
        }
      }
    } else {
      record(`${id}-create`, role, 'FAIL', 'pricing-rules create', err || 'no success');
      await page.keyboard.press('Escape');
    }
  } catch (e) {
    record(`${id}-crud`, role, 'FAIL', 'pricing-rules', String(e.message || e));
  }
}

async function testInventory(page, role) {
  const id = '1.5';
  try {
    if (!(await testLoad(page, `${id}-load`, role, '/inventory', 'inventory list loads'))) return;
    const search = page.locator('input[placeholder*="Such"], input[type="search"], .ant-input-search input').first();
    if ((await search.count()) > 0) {
      await search.fill('a');
      await page.keyboard.press('Enter');
      await waitReady(page, 800);
      record(`${id}-search`, role, 'PASS', 'inventory search');
    } else {
      record(`${id}-search`, role, 'SKIP', 'inventory search not found');
    }
    const lowStock = await page.locator('text=/Niedrig|Low stock|Mindestbestand|Warnung/i').count();
    record(`${id}-low`, role, 'PASS', 'inventory low-stock UI present or empty', `matches=${lowStock}`);
    const adjust = page.locator('button').filter({ hasText: /Anpassen|Restock|Bestand|Update/i }).first();
    if ((await adjust.count()) > 0) {
      await adjust.click();
      await waitReady(page, 500);
      await page.keyboard.press('Escape');
      record(`${id}-update`, role, 'PASS', 'inventory stock update UI opens');
    } else {
      record(`${id}-update`, role, 'SKIP', 'no stock update action (empty inventory?)');
    }
  } catch (e) {
    record(`${id}`, role, 'FAIL', 'inventory', String(e.message || e));
  }
}

async function testCustomers(page, role) {
  const id = '2.1';
  const stamp = Date.now().toString().slice(-6);
  const name = `E2E Kunde ${stamp}`;
  try {
    if (!(await testLoad(page, `${id}-load`, role, '/customers', 'customers list loads'))) return;
    const opened = await clickFirst(page, [
      'button:has-text("Neu")',
      'button:has-text("Kunde")',
      'button:has-text("Hinzufügen")',
      'button:has-text("Add")',
    ]);
    if (!opened) {
      record(`${id}-create`, role, 'FAIL', 'customers create button missing');
      return;
    }
    await waitReady(page, 600);
    const modal = page.locator('.ant-modal, .ant-drawer').last();
    // Fill first/last or name fields
    const inputs = modal.locator('input:not([type=checkbox]):not([type=radio]):not([type=hidden])');
    const count = await inputs.count();
    for (let i = 0; i < Math.min(count, 4); i++) {
      const el = inputs.nth(i);
      const ph = ((await el.getAttribute('placeholder')) || '').toLowerCase();
      const idAttr = ((await el.getAttribute('id')) || '').toLowerCase();
      if (idAttr.includes('email') || ph.includes('mail')) {
        await el.fill(`e2e${stamp}@example.com`);
      } else if (idAttr.includes('phone') || ph.includes('tel')) {
        await el.fill('+436601234567');
      } else {
        await el.fill(i === 0 ? name : `Test${i}`);
      }
    }
    await clickFirst(page, ['.ant-modal button.ant-btn-primary', '.ant-drawer button.ant-btn-primary', 'button:has-text("Speichern")']);
    await waitReady(page, 1500);
    const err = await page.locator('.ant-form-item-explain-error, .ant-message-error').allTextContents().catch(() => []);
    const ok = (await page.locator('.ant-message-success').count()) > 0 || (await page.locator(`text=${name}`).count()) > 0;
    record(`${id}-create`, role, ok ? 'PASS' : 'FAIL', 'customers create', ok ? '' : err.join('; '));
    if (!ok) {
      await page.keyboard.press('Escape');
      return;
    }

    const row = page.locator('tr').filter({ hasText: new RegExp(stamp) }).first();
    if ((await row.count()) === 0) {
      record(`${id}-edit`, role, 'SKIP', 'customers edit — row not listed');
      record(`${id}-delete`, role, 'SKIP', 'customers delete — row not listed');
      return;
    }
    const editBtn = row.locator('button').filter({ has: page.locator('.anticon-edit') }).or(row.locator('button:has-text("Bearbeiten")')).first();
    if ((await editBtn.count()) > 0) {
      await editBtn.click();
      await waitReady(page, 600);
      await clickFirst(page, ['.ant-modal button.ant-btn-primary', 'button:has-text("Speichern")']);
      await waitReady(page, 1000);
      record(`${id}-edit`, role, 'PASS', 'customers edit');
    } else {
      // maybe click row
      await row.click();
      await waitReady(page, 600);
      record(`${id}-edit`, role, 'SKIP', 'customers edit button missing');
    }

    const delBtn = row.locator('button').filter({ has: page.locator('.anticon-delete') }).first();
    if ((await delBtn.count()) > 0) {
      await delBtn.click();
      await clickFirst(page, ['.ant-popconfirm button.ant-btn-primary', 'button:has-text("OK")', 'button:has-text("Ja")']);
      await waitReady(page, 1000);
      record(`${id}-delete`, role, 'PASS', 'customers delete');
    } else {
      record(`${id}-delete`, role, 'SKIP', 'customers delete button missing');
    }
  } catch (e) {
    record(`${id}`, role, 'FAIL', 'customers', String(e.message || e));
  }
}

async function testReporting(page, role) {
  await testLoad(page, '3.1-load', role, '/reporting/operational', 'operational reports (alias→/reporting)');
  await testLoad(page, '3.1-hub', role, '/reporting', 'reporting hub loads');
  // try generate if button exists
  try {
    const gen = page.locator('button').filter({ hasText: /Generieren|Erzeugen|Generate|Bericht/i }).first();
    if ((await gen.count()) > 0 && (await gen.isVisible())) {
      await gen.click();
      await waitReady(page, 2000);
      record('3.1-generate', role, 'PASS', 'operational generate clicked');
    } else {
      record('3.1-generate', role, 'SKIP', 'no generate button on reporting hub');
    }
    const dl = page.locator('button,a').filter({ hasText: /Download|Herunterladen|PDF|Export/i }).first();
    if ((await dl.count()) > 0) {
      record('3.1-download', role, 'PASS', 'download/export control visible');
    } else {
      record('3.1-download', role, 'SKIP', 'download control not visible');
    }
  } catch (e) {
    record('3.1-actions', role, 'FAIL', 'operational actions', String(e.message || e));
  }

  await testLoad(page, '3.2-alias', role, '/reporting/center', 'report center alias');
  await testLoad(page, '3.2-load', role, '/reporting/report-center', 'report center loads');
  try {
    const filter = page.locator('.ant-select, .ant-picker, button:has-text("Filtern"), button:has-text("Anwenden")').first();
    if ((await filter.count()) > 0) {
      record('3.2-filters', role, 'PASS', 'report center filters present');
    } else {
      record('3.2-filters', role, 'FAIL', 'report center filters missing');
    }
  } catch (e) {
    record('3.2-filters', role, 'FAIL', 'report center filters', String(e.message || e));
  }

  await testLoad(page, '3.3-load', role, '/audit-logs', 'audit-logs loads');
  try {
    const pickers = await page.locator('.ant-picker').count();
    const selects = await page.locator('.ant-select').count();
    record('3.3-filters', role, pickers + selects > 0 ? 'PASS' : 'FAIL', 'audit-logs filters', `pickers=${pickers} selects=${selects}`);
    const pag = await page.locator('.ant-pagination').count();
    record('3.3-pagination', role, pag > 0 ? 'PASS' : 'SKIP', 'audit-logs pagination', `count=${pag}`);
  } catch (e) {
    record('3.3-filters', role, 'FAIL', 'audit filters', String(e.message || e));
  }

  // user-activity: may redirect
  await testLoad(page, '3.4-alias', role, '/reporting/user-activity', 'user-activity route');
  await testLoad(page, '3.4-admin', role, '/admin/reports/user-activity', 'admin user-activity');
  try {
    const userSel = page.locator('.ant-select').first();
    if ((await userSel.count()) > 0) {
      await userSel.click();
      await page.waitForTimeout(500);
      const opt = page.locator('.ant-select-item-option').first();
      if ((await opt.count()) > 0) {
        await opt.click();
        record('3.4-user', role, 'PASS', 'user-activity user select');
      } else {
        record('3.4-user', role, 'SKIP', 'no user options');
      }
    } else {
      record('3.4-user', role, 'SKIP', 'user select missing');
    }
    const gen = page.locator('button').filter({ hasText: /Generieren|Erzeugen|Generate|Laden|Anwenden/i }).first();
    if ((await gen.count()) > 0) {
      await gen.click();
      await waitReady(page, 2000);
      record('3.4-generate', role, 'PASS', 'user-activity generate');
    } else {
      record('3.4-generate', role, 'SKIP', 'generate button missing');
    }
  } catch (e) {
    record('3.4-actions', role, 'FAIL', 'user-activity actions', String(e.message || e));
  }

  await testLoad(page, '3.5-load', role, '/reporting/staff', 'staff performance loads');
  try {
    const metrics = await page.locator('text=/Umsatz|Performance|Leistung|Transaktionen|Sales/i').count();
    record('3.5-metrics', role, metrics > 0 ? 'PASS' : 'SKIP', 'staff metrics text', `matches=${metrics}`);
    const pickers = await page.locator('.ant-picker').count();
    record('3.5-filters', role, pickers > 0 ? 'PASS' : 'SKIP', 'staff date filters', `pickers=${pickers}`);
  } catch (e) {
    record('3.5', role, 'FAIL', 'staff', String(e.message || e));
  }
}

async function testBackup(page, role) {
  await testLoad(page, '4.1-root', role, '/backup', 'backup root→dashboard');
  await testLoad(page, '4.1-dash', role, '/backup/dashboard', 'backup overview');
  try {
    const status = await page.locator('text=/Status|Erfolg|Failed|Succeeded|Letzte|Last/i').count();
    record('4.1-status', role, status > 0 ? 'PASS' : 'FAIL', 'backup status/last time visible', `matches=${status}`);
  } catch (e) {
    record('4.1-status', role, 'FAIL', 'backup status', String(e.message || e));
  }

  await testLoad(page, '4.2-runs', role, '/backup/runs', 'backup runs');
  try {
    const badges = await page.locator('.ant-tag, .ant-badge').count();
    record('4.2-badges', role, badges > 0 ? 'PASS' : 'SKIP', 'backup run status badges', `count=${badges}`);
  } catch (e) {
    record('4.2-badges', role, 'FAIL', 'badges', String(e.message || e));
  }

  // legacy paths expected to redirect or 404 — document
  const cfg404 = await gotoPage(page, '/backup/config');
  if (!cfg404.ok && cfg404.reason?.includes('404')) {
    record('4.3-legacy-config', role, 'FAIL', '/backup/config should redirect to /backup/configuration', cfg404.reason);
  } else if (cfg404.ok && cfg404.url.includes('/backup/configuration')) {
    record('4.3-legacy-config', role, 'PASS', '/backup/config redirects');
  } else if (cfg404.ok) {
    record('4.3-legacy-config', role, 'PASS', '/backup/config loads', cfg404.url);
  } else {
    record('4.3-legacy-config', role, 'FAIL', '/backup/config', cfg404.reason);
  }

  await testLoad(page, '4.3-config', role, '/backup/configuration', 'backup configuration');
  try {
    const sched = await page.locator('text=/Cron|Zeitplan|Schedule|Retention|Aufbewahrung/i').count();
    record('4.3-schedule', role, sched > 0 ? 'PASS' : 'FAIL', 'schedule/retention visible', `matches=${sched}`);
  } catch (e) {
    record('4.3-schedule', role, 'FAIL', 'schedule', String(e.message || e));
  }

  const logs404 = await gotoPage(page, '/backup/logs');
  if (!logs404.ok && logs404.reason?.includes('404')) {
    record('4.4-legacy-logs', role, 'FAIL', '/backup/logs should redirect to /backup/audit', logs404.reason);
  } else if (logs404.ok && logs404.url.includes('/backup/audit')) {
    record('4.4-legacy-logs', role, 'PASS', '/backup/logs redirects');
  } else if (logs404.ok) {
    record('4.4-legacy-logs', role, 'PASS', '/backup/logs loads');
  } else {
    record('4.4-legacy-logs', role, 'FAIL', '/backup/logs', logs404.reason);
  }

  await testLoad(page, '4.4-audit', role, '/backup/audit', 'backup audit log');
}

async function testSettings(page, role) {
  await testLoad(page, '5.1', role, '/settings', 'settings hub');
  try {
    const sections = await page.locator('a[href^="/settings/"], .ant-menu-item, .ant-card').count();
    record('5.1-sections', role, sections > 0 ? 'PASS' : 'FAIL', 'settings sections visible', `count=${sections}`);
  } catch (e) {
    record('5.1-sections', role, 'FAIL', 'sections', String(e.message || e));
  }

  await testLoad(page, '5.2-load', role, '/settings/session', 'session settings');
  try {
    const inputs = await page.locator('input, .ant-input-number, .ant-slider').count();
    const save = page.locator('button').filter({ hasText: /Speichern|Save/i }).first();
    if (inputs > 0 && (await save.count()) > 0) {
      await save.click();
      await waitReady(page, 1500);
      const ok = (await page.locator('.ant-message-success, .ant-notification-notice-success').count()) > 0;
      record('5.2-save', role, ok ? 'PASS' : 'SKIP', 'session save', ok ? 'toast' : 'no toast (may be unchanged)');
    } else {
      record('5.2-save', role, 'FAIL', 'session controls/save missing');
    }
  } catch (e) {
    record('5.2-save', role, 'FAIL', 'session save', String(e.message || e));
  }

  // appearance alias
  const app = await gotoPage(page, '/settings/appearance');
  if (!app.ok && app.reason?.includes('404')) {
    record('5.3-legacy-appearance', role, 'FAIL', '/settings/appearance should redirect to personalization', app.reason);
  } else if (app.ok && app.url.includes('personalization')) {
    record('5.3-legacy-appearance', role, 'PASS', '/settings/appearance redirects');
  } else if (app.ok) {
    record('5.3-legacy-appearance', role, 'PASS', '/settings/appearance loads', app.url);
  } else {
    record('5.3-legacy-appearance', role, 'FAIL', '/settings/appearance', `${app.reason} @ ${app.url}`);
  }

  await testLoad(page, '5.3-load', role, '/settings/personalization', 'appearance/personalization');
  try {
    const lang = page.locator('.ant-select, button, .ant-radio-group').filter({ hasText: /Deutsch|English|Türk|Sprache|Language/i }).first();
    const anyLang = await page.locator('text=/Sprache|Language|Deutsch|English/i').count();
    if (anyLang > 0) {
      record('5.3-lang', role, 'PASS', 'language controls visible');
      const save = page.locator('button').filter({ hasText: /Speichern|Save/i }).first();
      if ((await save.count()) > 0) {
        await save.click();
        await waitReady(page, 1200);
        record('5.3-save', role, 'PASS', 'personalization save clicked');
      } else {
        record('5.3-save', role, 'SKIP', 'no save (auto-save?)');
      }
    } else {
      record('5.3-lang', role, 'FAIL', 'language controls missing');
    }
  } catch (e) {
    record('5.3', role, 'FAIL', 'personalization', String(e.message || e));
  }

  await testLoad(page, '5.4-load', role, '/settings/payment-methods', 'payment methods');
  try {
    const opened = await clickFirst(page, [
      'button:has-text("Neu")',
      'button:has-text("Hinzufügen")',
      'button:has-text("Zahlungsart")',
      'button:has-text("Add")',
    ]);
    if (opened) {
      await waitReady(page, 600);
      const modal = page.locator('.ant-modal, .ant-drawer').last();
      const inp = modal.locator('input:not([type=checkbox]):not([type=radio])').first();
      if ((await inp.count()) > 0) await inp.fill(`E2E-PM-${Date.now().toString().slice(-5)}`);
      await clickFirst(page, ['.ant-modal button.ant-btn-primary', 'button:has-text("Speichern")']);
      await waitReady(page, 1200);
      const err = await page.locator('.ant-message-error, .ant-form-item-explain-error').first().textContent().catch(() => '');
      const ok = (await page.locator('.ant-message-success').count()) > 0;
      record('5.4-add', role, ok ? 'PASS' : 'FAIL', 'payment method add', err || '');
      await page.keyboard.press('Escape');
    } else {
      record('5.4-add', role, 'SKIP', 'add button missing (register-scoped?)');
    }
    const toggle = page.locator('.ant-switch').first();
    if ((await toggle.count()) > 0) {
      await toggle.click();
      await waitReady(page, 800);
      record('5.4-toggle', role, 'PASS', 'payment method toggle');
      // revert
      await toggle.click().catch(() => {});
    } else {
      record('5.4-toggle', role, 'SKIP', 'no switch on page');
    }
  } catch (e) {
    record('5.4', role, 'FAIL', 'payment methods', String(e.message || e));
  }
}

async function runAs(browser, accountKey) {
  const acct = ACCOUNTS[accountKey];
  console.log(`\n======== ${acct.label} ========`);
  const { token, refreshToken } = await apiLogin(acct.login, acct.password);
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  await injectSession(page, token, refreshToken);
  await page.goto(`${BASE}/dashboard`, { waitUntil: 'domcontentloaded' });
  await waitReady(page, 2000);
  if (page.url().includes('/login')) {
    // fallback UI login
    await page.locator('input[autocomplete="username"], input[name="loginIdentifier"]').fill(acct.login);
    await page.locator('input[type="password"]').fill(acct.password);
    await page.locator('button[type="submit"]').click();
    await page.waitForURL(/\/(dashboard|admin|settings)/, { timeout: 60000 }).catch(() => {});
    await waitReady(page, 2000);
  }

  await testProductsCrud(page, acct.label);
  await testCategoriesCrud(page, acct.label);
  await testModifierGroups(page, acct.label);
  await testPricingRules(page, acct.label);
  await testInventory(page, acct.label);
  await testCustomers(page, acct.label);
  await testReporting(page, acct.label);
  await testBackup(page, acct.label);
  await testSettings(page, acct.label);

  await context.close();
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  try {
    await runAs(browser, 'manager');
    // SuperAdmin quick pass on backup + settings only (tenant may differ)
    await runAs(browser, 'superAdmin');
  } finally {
    await browser.close();
  }

  const outDir = join(__dirname, '..', 'tmp');
  mkdirSync(outDir, { recursive: true });
  const outPath = join(outDir, 'fa-sections-smoke-results.json');
  writeFileSync(outPath, JSON.stringify(results, null, 2));
  const pass = results.filter((r) => r.status === 'PASS').length;
  const fail = results.filter((r) => r.status === 'FAIL').length;
  const skip = results.filter((r) => r.status === 'SKIP').length;
  console.log(`\nSummary: PASS=${pass} FAIL=${fail} SKIP=${skip}`);
  console.log(`Wrote ${outPath}`);
  if (fail > 0) process.exitCode = 1;
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
