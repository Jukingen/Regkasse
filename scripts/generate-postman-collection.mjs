/**
 * Builds docs/postman/Regkasse.postman_collection.json from backend/swagger.json.
 * Every request path and method must exist in the OpenAPI document.
 */
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const spec = JSON.parse(readFileSync(path.join(root, 'backend', 'swagger.json'), 'utf8'));

const HTTP = new Set(['get', 'post', 'put', 'patch', 'delete']);

function resolveSchema(schema, depth = 0) {
  if (!schema || depth > 8) return schema ?? {};
  if (schema.$ref) {
    const name = schema.$ref.split('/').pop();
    return resolveSchema(spec.components?.schemas?.[name] ?? {}, depth + 1);
  }
  if (schema.allOf) {
    const merged = { type: 'object', properties: {}, required: [] };
    for (const part of schema.allOf) {
      const resolved = resolveSchema(part, depth + 1);
      Object.assign(merged.properties, resolved.properties ?? {});
      merged.required.push(...(resolved.required ?? []));
    }
    return merged;
  }
  return schema;
}

function exampleValue(name, schema, depth) {
  const resolved = resolveSchema(schema, depth);
  if (resolved.enum?.length) return resolved.enum[0];
  if (resolved.type === 'array') {
    return depth > 3 ? [] : [exampleValue(name, resolved.items ?? { type: 'string' }, depth + 1)];
  }
  if (resolved.type === 'object' || resolved.properties) {
    return exampleObject(resolved, depth + 1);
  }
  if (resolved.type === 'integer') return 1;
  if (resolved.type === 'number') return 1;
  if (resolved.type === 'boolean') return false;
  if (resolved.format === 'uuid') return `{{${name}}}`;
  if (resolved.format === 'date-time') return '2026-11-01T00:00:00Z';
  if (resolved.format === 'date') return '2026-11-01';
  const key = name.toLowerCase();
  if (key.includes('email')) return 'cashier@example.com';
  if (key === 'role') return 'Cashier';
  if (key.includes('password')) return '{{password}}';
  if (key === 'loginidentifier') return '{{loginIdentifier}}';
  if (key === 'clientapp') return '{{clientApp}}';
  if (key === 'method') return 'cash';
  if (key.includes('slug')) return 'wien-cafe';
  if (key === 'countrycode') return 'AT';
  if (key === 'vatregime') return 'AT_RKSV_STANDARD';
  if (key === 'taxtype') return 'standard';
  return 'sample';
}

function exampleObject(schema, depth = 0) {
  const resolved = resolveSchema(schema, depth);
  const properties = resolved.properties ?? {};
  const keys = resolved.required?.length ? resolved.required : Object.keys(properties).slice(0, 4);
  const body = {};
  for (const key of keys) {
    if (!properties[key]) continue;
    body[key] = exampleValue(key, properties[key], depth);
  }
  return body;
}

function operationOf(method, requestPath) {
  const item = spec.paths[requestPath];
  const operation = item?.[method.toLowerCase()];
  if (!item || !operation || !HTTP.has(method.toLowerCase())) {
    throw new Error(`swagger.json has no ${method} ${requestPath}`);
  }
  return { item, operation };
}

function parametersOf(pathItem, operation) {
  return [...(pathItem.parameters ?? []), ...(operation.parameters ?? [])].map((parameter) => {
    if (parameter.$ref) {
      const name = parameter.$ref.split('/').pop();
      return spec.components?.parameters?.[name] ?? parameter;
    }
    return parameter;
  });
}

function queryParameters(pathItem, operation, enabled) {
  return parametersOf(pathItem, operation)
    .filter((parameter) => parameter.in === 'query')
    .map((parameter) => {
      const value = enabled?.[parameter.name];
      return {
        key: parameter.name,
        value: value ?? '',
        description: parameter.description || parameter.schema?.description || '',
        disabled: value === undefined,
      };
    });
}

function requestBody(method, requestPath, override) {
  if (override !== undefined) return override;
  const { operation } = operationOf(method, requestPath);
  const schema = operation.requestBody?.content?.['application/json']?.schema;
  if (!schema) return undefined;
  return exampleObject(schema);
}

const loginTest = [
  'const body = pm.response.json();',
  'if (body.token) pm.collectionVariables.set("jwt", body.token);',
  'if (body.refreshToken) pm.collectionVariables.set("refreshToken", body.refreshToken);',
  'if (body.user && body.user.tenantId) pm.collectionVariables.set("tenantId", body.user.tenantId);',
  'if (body.user && body.user.tenantSlug) pm.collectionVariables.set("tenantSlug", body.user.tenantSlug);',
  'if (body.user && body.user.id) pm.collectionVariables.set("userId", body.user.id);',
].join('\n');

const csrfTest = [
  'const body = pm.response.json();',
  'if (body.token) pm.collectionVariables.set("csrfToken", body.token);',
].join('\n');

const cashPayment = {
  customerId: '{{customerId}}',
  cashRegisterId: '{{cashRegisterId}}',
  tableNumber: 1,
  totalAmount: 12.5,
  items: [{ productId: '{{productId}}', quantity: 1, taxType: 'standard' }],
  payment: { method: 'cash', tseRequired: true, amount: 12.5 },
  idempotencyKey: 'postman-cash-001',
};

const folders = [
  {
    name: 'Getting Started',
    description:
      'Run these in order on a Development API. Login stores the JWT. The tenant header is the collection variable tenantSlug, sent as X-Tenant-Id. Health does not require a token.',
    requests: [
      {
        name: 'Auth login (get JWT)',
        method: 'POST',
        path: '/api/Auth/login',
        auth: 'noauth',
        tests: loginTest,
        body: {
          loginIdentifier: '{{loginIdentifier}}',
          password: '{{password}}',
          clientApp: '{{clientApp}}',
        },
        note: 'clientApp is pos or admin. The test script writes jwt, refreshToken, tenantId, and tenantSlug.',
      },
      {
        name: 'Set tenant header',
        method: 'GET',
        path: '/api/tenants/switcher',
        note: 'Development resolves the mandant from X-Tenant-Id: {{tenantSlug}} (seeded demo slug: dev). Production api.regkasse.at ignores this header and uses the JWT tenant_id claim. Change the collection variable, then send this request.',
      },
      {
        name: 'Health check',
        method: 'GET',
        path: '/api/health',
        auth: 'noauth',
        note: 'Liveness of the API process. /api/health/live and /api/health/ready are the probe split.',
      },
      {
        name: 'CSRF token',
        method: 'GET',
        path: '/api/csrf/token',
        auth: 'noauth',
        tests: csrfTest,
        note: 'Development skips CSRF. When Security:Csrf is enabled, send the saved csrfToken as X-XSRF-TOKEN on POST, PUT, PATCH, and DELETE. Login, refresh, and /api/health are exempt.',
      },
    ],
  },
  {
    name: 'Auth',
    description: 'Shared /api/Auth surface used by POS and Admin. Login is repeated here so the folder stands alone.',
    requests: [
      {
        name: 'Login',
        method: 'POST',
        path: '/api/Auth/login',
        auth: 'noauth',
        tests: loginTest,
        body: {
          loginIdentifier: '{{loginIdentifier}}',
          password: '{{password}}',
          clientApp: '{{clientApp}}',
        },
      },
      {
        name: 'Current user',
        method: 'GET',
        path: '/api/Auth/me',
      },
      {
        name: 'Refresh token',
        method: 'POST',
        path: '/api/Auth/refresh',
        body: { refreshToken: '{{refreshToken}}', clientApp: '{{clientApp}}' },
      },
      {
        name: 'Verify 2FA',
        method: 'POST',
        path: '/api/Auth/verify-2fa',
        auth: 'noauth',
        body: { twoFactorToken: '{{twoFactorToken}}', code: '123456' },
        note: 'Super Admin TOTP step after password. Development can bypass 2FA. Do not use a real backup code in this collection.',
      },
      {
        name: 'Logout',
        method: 'POST',
        path: '/api/Auth/logout',
      },
      {
        name: 'Logout all sessions',
        method: 'POST',
        path: '/api/Auth/logout-all',
      },
      {
        name: 'Revoke refresh token',
        method: 'POST',
        path: '/api/Auth/revoke',
        body: { refreshToken: '{{refreshToken}}' },
      },
    ],
  },
  {
    name: 'POS',
    description: 'Cashier API. POS clients call /api/pos/*, /api/Auth/*, and /api/Receipts/* only.',
    folders: [
      {
        name: 'Cart',
        requests: [
          { name: 'Current cart', method: 'GET', path: '/api/pos/cart/current' },
          {
            name: 'Open cart',
            method: 'POST',
            path: '/api/pos/cart',
            body: { tableNumber: 1, notes: 'Postman sample' },
          },
          {
            name: 'Add product',
            method: 'POST',
            path: '/api/pos/cart/add-item',
            body: {
              productId: '{{productId}}',
              quantity: 1,
              tableNumber: 1,
              cashRegisterId: '{{cashRegisterId}}',
            },
          },
          {
            name: 'Get cart',
            method: 'GET',
            path: '/api/pos/cart/{cartId}',
          },
          {
            name: 'Update line',
            method: 'PUT',
            path: '/api/pos/cart/items/{itemId}',
            body: { quantity: 2 },
          },
          {
            name: 'Remove line',
            method: 'DELETE',
            path: '/api/pos/cart/items/{itemId}',
          },
          { name: 'Clear cart', method: 'POST', path: '/api/pos/cart/clear' },
        ],
      },
      {
        name: 'Payment',
        requests: [
          { name: 'Payment methods', method: 'GET', path: '/api/pos/payment/methods' },
          {
            name: 'Pay cash',
            method: 'POST',
            path: '/api/pos/payment',
            body: cashPayment,
            note: 'totalAmount is a client hint. The server recomputes catalog totals and rejects a mismatch. Idempotency-Key retries return the existing payment.',
          },
          {
            name: 'Get payment',
            method: 'GET',
            path: '/api/pos/payment/{id}',
            pathVars: { id: 'paymentId' },
          },
          {
            name: 'Receipt',
            method: 'GET',
            path: '/api/pos/payment/{id}/receipt',
            pathVars: { id: 'paymentId' },
          },
          {
            name: 'Receipt QR',
            method: 'GET',
            path: '/api/pos/payment/{id}/qr.png',
            pathVars: { id: 'paymentId' },
          },
          {
            name: 'Storno',
            method: 'POST',
            path: '/api/pos/payment/storno',
            body: {
              customerId: '{{customerId}}',
              cashRegisterId: '{{cashRegisterId}}',
              tableNumber: 1,
              totalAmount: 12.5,
              originalReceiptNumber: '{{receiptNumber}}',
              stornoReason: 'KundeStorniert',
              payment: { method: 'cash', tseRequired: true, amount: 12.5 },
            },
            note: 'Full-receipt cancellation. stornoReason is the RKSV enum name (KundeStorniert, FalscherBetrag, TechnischerFehler, Anderes).',
          },
        ],
      },
      {
        name: 'Shift',
        requests: [
          { name: 'Current shift', method: 'GET', path: '/api/pos/shift/current' },
          {
            name: 'Start shift',
            method: 'POST',
            path: '/api/pos/shift/start',
            body: { cashRegisterId: '{{cashRegisterId}}', startBalance: 100 },
          },
          {
            name: 'End shift',
            method: 'POST',
            path: '/api/pos/shift/end',
            body: { endBalance: 100, notes: 'Postman sample' },
          },
          {
            name: 'Daily closing status',
            method: 'GET',
            path: '/api/pos/shift/daily-closing/status',
          },
          {
            name: 'Daily closing',
            method: 'POST',
            path: '/api/pos/shift/daily-closing',
            body: { cashCount: 100, notes: 'Postman sample' },
            note: 'Tagesabschluss. A closed day blocks further payments until the next business day.',
          },
        ],
      },
    ],
  },
  {
    name: 'Admin',
    description: 'FA API under /api/admin/*. Mandanten-Admin is role Manager. Cross-tenant reads return 404.',
    folders: [
      {
        name: 'Users',
        requests: [
          {
            name: 'List users',
            method: 'GET',
            path: '/api/admin/users',
            query: { page: '1', pageSize: '20' },
          },
          {
            name: 'Create user',
            method: 'POST',
            path: '/api/admin/users',
            body: {
              email: 'cashier@example.com',
              firstName: 'E2E',
              lastName: 'Kassierer',
              role: 'Cashier',
              userName: 'cashier1',
            },
            note: 'role is the backend name (Cashier, Manager, SuperAdmin). The generated password is returned once.',
          },
          {
            name: 'Get user',
            method: 'GET',
            path: '/api/admin/users/{id}',
            pathVars: { id: 'userId' },
          },
          {
            name: 'Deactivate user',
            method: 'POST',
            path: '/api/admin/users/{id}/deactivate',
            pathVars: { id: 'userId' },
          },
          {
            name: 'Reset password',
            method: 'POST',
            path: '/api/admin/users/{id}/reset-password',
            pathVars: { id: 'userId' },
          },
        ],
      },
      {
        name: 'Tenants',
        requests: [
          {
            name: 'List tenants',
            method: 'GET',
            path: '/api/admin/tenants',
            query: { page: '1', pageSize: '20' },
          },
          {
            name: 'Slug availability',
            method: 'GET',
            path: '/api/admin/tenants/slug-availability',
            query: { slug: 'wien-cafe' },
          },
          {
            name: 'Create tenant',
            method: 'POST',
            path: '/api/admin/tenants',
            body: {
              name: 'Wien Cafe',
              slug: 'wien-cafe',
              countryCode: 'AT',
              vatRegime: 'AT_RKSV_STANDARD',
              email: 'info@wien-cafe.example',
              adminEmail: 'admin@wien-cafe.example',
            },
            note: 'Super Admin wizard. countryCode is ISO 3166-1 alpha-2. vatRegime must be allowed for that country. AT_RKSV_STANDARD is the Austrian default.',
          },
          {
            name: 'Get tenant',
            method: 'GET',
            path: '/api/admin/tenants/{tenantId}',
          },
          {
            name: 'List tenant users',
            method: 'GET',
            path: '/api/admin/tenants/{tenantId}/users',
          },
          {
            name: 'Create cashier',
            method: 'POST',
            path: '/api/admin/tenants/{tenantId}/users',
            body: {
              email: 'cashier@example.com',
              userName: 'cashier1',
              firstName: 'E2E',
              lastName: 'Kassierer',
              role: 'Cashier',
            },
          },
          {
            name: 'Assign user role',
            method: 'POST',
            path: '/api/admin/tenants/{tenantId}/users/assign',
            body: { userId: '{{userId}}', role: 'Cashier' },
          },
          {
            name: 'Impersonate tenant',
            method: 'POST',
            path: '/api/admin/tenants/{tenantId}/impersonate',
            note: 'Super Admin only. The issued JWT carries the target tenant_id and tenant_impersonation=true.',
          },
        ],
      },
      {
        name: 'Receipts',
        requests: [
          {
            name: 'List receipts',
            method: 'GET',
            path: '/api/Receipts/list',
            query: { page: '1', pageSize: '20', cashRegisterId: '{{cashRegisterId}}' },
          },
          {
            name: 'Get receipt',
            method: 'GET',
            path: '/api/Receipts/{receiptId}',
          },
          {
            name: 'Receipt by payment',
            method: 'GET',
            path: '/api/Receipts/by-payment/{paymentId}',
          },
          {
            name: 'List payments',
            method: 'GET',
            path: '/api/admin/payments',
            query: { page: '1', pageSize: '20' },
          },
          {
            name: 'Get payment',
            method: 'GET',
            path: '/api/admin/payments/{id}',
            pathVars: { id: 'paymentId' },
          },
          {
            name: 'Download receipt PDF',
            method: 'GET',
            path: '/api/admin/payments/{paymentId}/reprint',
            note: 'PDF reprint of the fiscal receipt. Save the response as a file.',
          },
        ],
      },
    ],
  },
  {
    name: 'Fiscal',
    description:
      'TSE status and DEP / RKSV special receipts. These samples do not change signing rules. A forced Monatsbeleg is a past-month catch-up, not a normal sale.',
    folders: [
      {
        name: 'TSE',
        requests: [
          { name: 'TSE status', method: 'GET', path: '/api/tse/status' },
          { name: 'TSE health', method: 'GET', path: '/api/tse/health' },
          { name: 'Admin TSE health', method: 'GET', path: '/api/admin/tse/health/status' },
          { name: 'List TSE devices', method: 'GET', path: '/api/admin/tse-management/devices' },
          {
            name: 'Provision TSE',
            method: 'POST',
            path: '/api/admin/tse-management/provision',
            body: { tenantId: '{{tenantId}}', cashRegisterId: '{{cashRegisterId}}' },
          },
          { name: 'Offline queue status', method: 'GET', path: '/api/admin/tse/offline-queue/status' },
          { name: 'Ausfall episodes', method: 'GET', path: '/api/admin/tse/ausfall/episodes' },
        ],
      },
      {
        name: 'DEP',
        requests: [
          { name: 'RKSV status', method: 'GET', path: '/api/rksv/status' },
          { name: 'RKSV runtime config', method: 'GET', path: '/api/admin/rksv/config' },
          {
            name: 'DEP export',
            method: 'GET',
            path: '/api/admin/rksv/dep-export',
            query: { cashRegisterId: '{{cashRegisterId}}' },
            note: 'Datenerfassungsprotokoll JSON for the selected register and period.',
          },
          {
            name: 'Validate DEP export',
            method: 'POST',
            path: '/api/admin/rksv/dep-export/validate',
            body: { exportJson: '{}' },
          },
          { name: 'DEP export history', method: 'GET', path: '/api/admin/rksv/dep-export/history' },
          { name: 'DEP export status', method: 'GET', path: '/api/admin/rksv/dep-export/status' },
          { name: 'Last DEP export', method: 'GET', path: '/api/admin/rksv/dep-export/last-export' },
          {
            name: 'Nullbeleg',
            method: 'POST',
            path: '/api/rksv/special-receipts/nullbeleg',
            body: { cashRegisterId: '{{cashRegisterId}}', reason: 'Postman sample' },
          },
          {
            name: 'Startbeleg',
            method: 'POST',
            path: '/api/rksv/special-receipts/startbeleg',
            body: { cashRegisterId: '{{cashRegisterId}}', reason: 'Postman sample' },
          },
          {
            name: 'Monatsbeleg (force past month)',
            method: 'POST',
            path: '/api/rksv/special-receipts/monatsbeleg',
            query: { force: 'true' },
            body: { cashRegisterId: '{{cashRegisterId}}', year: 2026, month: 10, reason: 'Postman sample' },
            note: 'force=true creates a late Monatsbeleg for a past month. The response includes the new BelegNr.',
          },
          {
            name: 'Jahresbeleg',
            method: 'POST',
            path: '/api/rksv/special-receipts/jahresbeleg',
            body: { cashRegisterId: '{{cashRegisterId}}', year: 2026, reason: 'Postman sample' },
          },
          {
            name: 'Schlussbeleg',
            method: 'POST',
            path: '/api/rksv/special-receipts/schlussbeleg',
            body: { cashRegisterId: '{{cashRegisterId}}', reason: 'Postman sample' },
          },
          { name: 'Monatsbeleg list', method: 'GET', path: '/api/admin/rksv/monatsbelege' },
        ],
      },
    ],
  },
];

function buildRequest(entry) {
  const { item, operation } = operationOf(entry.method, entry.path);
  const segments = entry.path
    .split('/')
    .filter(Boolean)
    .map((segment) => {
      const match = segment.match(/^\{(.+)\}$/);
      if (!match) return segment;
      const variable = entry.pathVars?.[match[1]] ?? match[1];
      return `{{${variable}}}`;
    });
  const query = queryParameters(item, operation, entry.query);
  const body = requestBody(entry.method, entry.path, entry.body);
  const description = [entry.note, operation.summary, operation.description]
    .filter(Boolean)
    .join('\n\n');
  const request = {
    name: entry.name,
    request: {
      method: entry.method,
      header: body
        ? [{ key: 'Content-Type', value: 'application/json' }]
        : [],
      url: {
        raw: `{{baseUrl}}/${segments.join('/')}`,
        host: ['{{baseUrl}}'],
        path: segments,
        ...(query.length ? { query } : {}),
      },
      description,
    },
  };
  if (entry.auth === 'noauth') {
    request.request.auth = { type: 'noauth' };
  }
  if (body !== undefined && entry.method !== 'GET') {
    request.request.body = {
      mode: 'raw',
      raw: `${JSON.stringify(body, null, 2)}\n`,
      options: { raw: { language: 'json' } },
    };
  }
  if (entry.tests) {
    request.event = [
      {
        listen: 'test',
        script: { type: 'text/javascript', exec: entry.tests.split('\n') },
      },
    ];
  }
  return request;
}

function buildFolder(folder) {
  const item = {
    name: folder.name,
    description: folder.description ?? '',
    item: [],
  };
  for (const request of folder.requests ?? []) {
    item.item.push(buildRequest(request));
  }
  for (const child of folder.folders ?? []) {
    item.item.push(buildFolder(child));
  }
  return item;
}

const collection = {
  info: {
    name: 'Regkasse API',
    _postman_id: 'regkasse-api',
    description:
      'Critical Regkasse API requests generated from backend/swagger.json. POS uses /api/pos, /api/Auth, and /api/Receipts. Admin uses /api/admin and /api/Auth. Replace collection variables before sending. Do not store real passwords in this file.',
    schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json',
  },
  auth: {
    type: 'bearer',
    bearer: [{ key: 'token', value: '{{jwt}}', type: 'string' }],
  },
  variable: [
    { key: 'baseUrl', value: 'http://localhost:5184' },
    { key: 'jwt', value: '' },
    { key: 'refreshToken', value: '' },
    { key: 'twoFactorToken', value: '' },
    { key: 'csrfToken', value: '' },
    { key: 'tenantSlug', value: 'dev' },
    { key: 'tenantId', value: '' },
    { key: 'userId', value: '' },
    { key: 'cashRegisterId', value: '' },
    { key: 'productId', value: '' },
    { key: 'customerId', value: '' },
    { key: 'cartId', value: '' },
    { key: 'itemId', value: '' },
    { key: 'paymentId', value: '' },
    { key: 'receiptId', value: '' },
    { key: 'receiptNumber', value: '' },
    { key: 'loginIdentifier', value: '' },
    { key: 'password', value: '' },
    { key: 'clientApp', value: 'pos' },
  ],
  item: folders.map(buildFolder),
};

const header = [
  { key: 'Accept', value: 'application/json' },
  { key: 'X-Tenant-Id', value: '{{tenantSlug}}' },
  { key: 'X-XSRF-TOKEN', value: '{{csrfToken}}' },
];

function applyHeaders(node) {
  if (node.request) {
    const existing = new Set((node.request.header ?? []).map((item) => item.key));
    node.request.header = [
      ...header.filter((item) => !existing.has(item.key)),
      ...(node.request.header ?? []),
    ];
    return;
  }
  for (const child of node.item ?? []) applyHeaders(child);
}

for (const folder of collection.item) applyHeaders(folder);

const output = path.join(root, 'docs', 'postman', 'Regkasse.postman_collection.json');
mkdirSync(path.dirname(output), { recursive: true });
writeFileSync(output, `${JSON.stringify(collection, null, 2)}\n`);

function walk(nodes, prefix, lines) {
  for (const node of nodes) {
    if (node.request) {
      lines.push(`${prefix}${node.request.method} ${node.name}`);
    } else {
      lines.push(`${prefix}${node.name}/`);
      walk(node.item ?? [], `${prefix}  `, lines);
    }
  }
}

const lines = [];
walk(collection.item, '', lines);
console.log(lines.join('\n'));
console.log(`requests ${lines.filter((line) => !line.trimEnd().endsWith('/')).length}`);
console.log(`bytes ${Buffer.byteLength(JSON.stringify(collection))}`);
