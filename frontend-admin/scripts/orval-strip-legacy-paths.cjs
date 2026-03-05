/**
 * Orval transformer: legacy product ve categories path'lerini spec'ten çıkarır.
 * FE-Admin bu endpoint'leri kullanmıyor (src/api/admin/* kullanılıyor).
 */
const LEGACY = ['/api/Product', '/api/Categories']; // path prefix'leri – silinirse orval bu endpoint'leri üretmez

module.exports = function (spec) {
    const paths = spec.paths || {};
    const filtered = {};
    for (const [path] of Object.entries(paths)) {
        const isLegacy = LEGACY.some((p) => path === p || path.startsWith(p + '/'));
        if (!isLegacy) filtered[path] = paths[path];
    }
    return { ...spec, paths: filtered };
};
