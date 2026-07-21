/**
 * Orval transformer: canonical boundary disi legacy path'leri spec'ten çıkarır.
 * FE-Admin payment için /api/admin/payments/* ve /api/pos/payment/* kullanır.
 */
const LEGACY = ['/api/Product', '/api/Categories', '/api/Payment']; // path prefix'leri – silinirse orval bu endpoint'leri üretmez

/** Removed from backend; do not generate clients. */
const REMOVED_PATHS = new Set([
  '/api/admin/tenants/{tenantId}/users/invite',
  '/api/admin/users/invite',
]);

/** Legacy route aliases omitted from backend OpenAPI; strip if still present in swagger.json. */
const LEGACY_ROUTE_ALIASES = [
  '/api/CompanySettings',
  '/api/pos/company-profile',
  '/api/pos/payment/card',
];

module.exports = function (spec) {
  const paths = spec.paths || {};
  const filtered = {};
  for (const [path] of Object.entries(paths)) {
    if (REMOVED_PATHS.has(path)) continue;
    const isLegacy = LEGACY.some((p) => path === p || path.startsWith(p + '/'));
    if (isLegacy) continue;
    const isLegacyAlias = LEGACY_ROUTE_ALIASES.some((p) => path === p || path.startsWith(p + '/'));
    if (isLegacyAlias) continue;
    filtered[path] = paths[path];
  }
  return { ...spec, paths: filtered };
};
