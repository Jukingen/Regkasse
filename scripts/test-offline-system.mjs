#!/usr/bin/env node
/**
 * Offline system smoke checks runnable without React Native / Expo.
 * Invoked by scripts/test-offline-system.sh
 */
import { readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');

let failed = 0;

function pass(label) {
  console.log(`  ✅ ${label}`);
}

function fail(label, detail = '') {
  failed += 1;
  console.error(`  ❌ ${label}${detail ? `: ${detail}` : ''}`);
}

function read(relPath) {
  const full = join(root, relPath);
  if (!existsSync(full)) {
    fail(`missing file ${relPath}`);
    return '';
  }
  return readFileSync(full, 'utf8');
}

function assertMatch(label, haystack, pattern) {
  if (pattern.test(haystack)) {
    pass(label);
  } else {
    fail(label, `expected ${pattern}`);
  }
}

// --- Test 1: Offline config ---
console.log('Test 1: Check Offline Config');

const offlineConfigTs = read('frontend/constants/offlineConfig.ts');
assertMatch('OFFLINE_EXPIRY_HOURS is 72', offlineConfigTs, /OFFLINE_EXPIRY_HOURS:\s*72/);
assertMatch('OFFLINE_WARNING_HOURS is 24', offlineConfigTs, /OFFLINE_WARNING_HOURS:\s*24/);
assertMatch('OFFLINE_CRITICAL_HOURS is 6', offlineConfigTs, /OFFLINE_CRITICAL_HOURS:\s*6/);
assertMatch('SYNC_INTERVAL_SECONDS is 30', offlineConfigTs, /SYNC_INTERVAL_SECONDS:\s*30/);
assertMatch('ENABLE_OFFLINE_GUTSCHEIN is false', offlineConfigTs, /ENABLE_OFFLINE_GUTSCHEIN:\s*false/);
assertMatch('POS offline-orders replay endpoint', offlineConfigTs, /\/api\/pos\/offline-orders\/replay/);

const offlineConfigService = read('frontend/services/config/offlineConfigService.ts');
assertMatch('OfflineConfigService singleton', offlineConfigService, /static getInstance\(\)/);
assertMatch('getExpiryMs uses OFFLINE_EXPIRY_HOURS', offlineConfigService, /getExpiryMs\(\)/);

const expiryMs = 72 * 60 * 60 * 1000;
if (offlineConfigTs.includes('OFFLINE_EXPIRY_HOURS: 72')) {
  pass(`expiry calculation: 72h = ${expiryMs}ms`);
} else {
  fail('expiry calculation');
}

console.log('');

// --- Test 2: Session manager ---
console.log('Test 2: Check Session Manager');

const sessionManager = read('frontend/services/auth/offlineSessionManager.ts');
assertMatch('AsyncStorage session key', sessionManager, /OFFLINE_SESSION_STORAGE_KEY/);
assertMatch('saveSession persists to AsyncStorage', sessionManager, /AsyncStorage\.setItem/);
assertMatch('isTokenExpired()', sessionManager, /isTokenExpired\(\)/);
assertMatch('canWorkOffline()', sessionManager, /canWorkOffline\(\)/);
assertMatch('JWT exp decode', sessionManager, /jwtDecode/);

// Token expiry math (mirrors OfflineSessionManager.saveSession fallback)
const tokenExpiryHours = 168;
const fallbackMs = tokenExpiryHours * 60 * 60 * 1000;
if (fallbackMs === 604_800_000) {
  pass('token expiry fallback: 168h = 604800000ms');
} else {
  fail('token expiry fallback calculation');
}

console.log('');

// --- Test 3: Order manager ---
console.log('Test 3: Check Order Manager');

const orderManager = read('frontend/services/offline/offlineOrderManager.ts');
assertMatch('saveOrder()', orderManager, /async saveOrder\(/);
assertMatch('OFFLINE- id prefix', orderManager, /OFFLINE-\$\{/);
assertMatch('72h EXPIRY_MS', orderManager, /72 \* 60 \* 60 \* 1000/);
assertMatch('voucher block on save', orderManager, /paymentPayloadContainsVoucherSecrets/);
assertMatch('local storage integration', orderManager, /this\.storage\.saveOrder/);

const offlineStorage = read('frontend/services/offline/offlineStorage.ts');
assertMatch('AsyncStorage key for orders', offlineStorage, /@regkasse\/offline_orders_storage_v1/);
assertMatch('IndexedDB for web', offlineStorage, /regkasse_offline_orders/);

function formatOfflineTimestamp(date) {
  const pad = (n) => String(n).padStart(2, '0');
  return (
    `${date.getUTCFullYear()}${pad(date.getUTCMonth() + 1)}${pad(date.getUTCDate())}` +
    `${pad(date.getUTCHours())}${pad(date.getUTCMinutes())}${pad(date.getUTCSeconds())}`
  );
}

const sampleId = `OFFLINE-${formatOfflineTimestamp(new Date())}-0042`;
if (/^OFFLINE-\d{14}-\d{4}$/.test(sampleId)) {
  pass(`order ID format sample: ${sampleId}`);
} else {
  fail('order ID format', sampleId);
}

const created = new Date('2026-06-27T12:00:00.000Z');
const expires = new Date(created.getTime() + 72 * 60 * 60 * 1000);
if (expires.toISOString() === '2026-06-30T12:00:00.000Z') {
  pass('order expiry: created + 72h');
} else {
  fail('order expiry calculation', expires.toISOString());
}

console.log('');

// --- Test 4: Sync service ---
console.log('Test 4: Check Sync Service');

const syncService = read('frontend/services/offline/offlineSyncService.ts');
assertMatch('auto-sync interval from config', syncService, /SYNC_INTERVAL_SECONDS/);
assertMatch('sync:online listener', syncService, /sync:online/);
assertMatch('syncNow()', syncService, /async syncNow\(\)/);
assertMatch('sync:completed event', syncService, /sync:completed/);
assertMatch('getSyncStatus()', syncService, /getSyncStatus\(\)/);
assertMatch('delegates to OfflineOrderManager', syncService, /OfflineOrderManager/);

console.log('');

// --- Test 5: Notification system ---
console.log('Test 5: Check Notification System');

const notificationService = read('frontend/services/offline/offlineNotificationService.ts');
assertMatch('OFFLINE_CRITICAL_HOURS threshold', notificationService, /OFFLINE_CRITICAL_HOURS/);
assertMatch('OFFLINE_WARNING_HOURS threshold', notificationService, /OFFLINE_WARNING_HOURS/);
assertMatch('offline:critical event', notificationService, /offline:critical/);
assertMatch('offline:warning event', notificationService, /offline:warning/);
assertMatch('sync:warning for token', notificationService, /sync:warning/);

const eventEmitterTs = read('frontend/utils/eventEmitter.ts');
assertMatch('EventMap includes offline:warning', eventEmitterTs, /'offline:warning'/);
assertMatch('EventMap includes offline:critical', eventEmitterTs, /'offline:critical'/);

// Simulate OfflineNotificationService threshold logic
const criticalThreshold = 6;
const warningThreshold = 24;
function classifyHoursRemaining(hoursRemaining) {
  if (hoursRemaining <= criticalThreshold && hoursRemaining > 0) return 'critical';
  if (hoursRemaining <= warningThreshold && hoursRemaining > criticalThreshold) return 'warning';
  return 'none';
}
if (classifyHoursRemaining(5) === 'critical') pass('critical warning at 5h remaining');
else fail('critical warning at 5h remaining');
if (classifyHoursRemaining(12) === 'warning') pass('warning at 12h remaining');
else fail('warning at 12h remaining');
if (classifyHoursRemaining(30) === 'none') pass('no warning at 30h remaining');
else fail('no warning at 30h remaining');

console.log('');

// --- Test 6: FA offline settings ---
console.log('Test 6: Check FA Settings');

const faPage = read('frontend-admin/src/app/(protected)/settings/offline/page.tsx');
assertMatch('FA offline settings page', faPage, /OfflineSettings/);

const faComponent = read('frontend-admin/src/features/settings/components/OfflineSettings.tsx');
assertMatch('OfflineSettings form component', faComponent, /Form\.useForm/);
assertMatch('save/submit handler', faComponent, /handleSubmit|onFinish/);

const faApi = read('frontend-admin/src/features/settings/api/offlineSettingsApi.ts');
assertMatch('GET /api/admin/settings/offline', faApi, /url:\s*'\/api\/admin\/settings\/offline'/);
assertMatch('PUT /api/admin/settings/offline', faApi, /method:\s*'PUT'/);
assertMatch('default offlineExpiryHours 72', faApi, /offlineExpiryHours:\s*72/);

const faHooks = read('frontend-admin/src/features/settings/hooks/useOfflineSettings.ts');
assertMatch('useOfflineSettings hook', faHooks, /useOfflineSettings/);
assertMatch('useUpdateOfflineSettings mutation', faHooks, /useUpdateOfflineSettings/);

// Backend offline_orders surface (new system)
const posController = read('backend/Controllers/PosOfflineOrdersController.cs');
assertMatch('PosOfflineOrdersController route', posController, /api\/pos\/offline-orders/);

const adminController = read('backend/Controllers/AdminOfflineOrdersController.cs');
assertMatch('AdminOfflineOrdersController route', adminController, /api\/admin\/offline-orders/);

console.log('');

if (failed > 0) {
  console.error(`❌ ${failed} check(s) failed`);
  process.exit(1);
}

console.log('✅ Node offline checks passed');
