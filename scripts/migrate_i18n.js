const fs = require('fs');
const path = require('path');

const locales = ['en', 'de', 'tr'];
const sourceDir = path.join(__dirname, '../frontend/i18n/locales');
// New structure: frontend/i18n/locales/{lang}/{ns}.json
// We assume the folders {lang} already exist (created in previous step).

const mappings = {
    // sourceKey: targetNamespace
    'common': 'common',
    'loading': 'common',
    'errors': 'common',
    'notifications': 'common',
    'auth': 'auth',
    'navigation': 'navigation',
    'settings': 'settings',
    'payment': 'payment',
    'cashRegister': 'checkout',
    'cart': 'checkout',
    'tax': 'checkout',
    'taxType': 'checkout',
    'rksv': 'checkout',
    'products': 'products', // assuming products key exists or mapping 'product'
    'product': 'products',
    'categories': 'products',
    'categoryDescriptions': 'products',
    'search': 'products',
    'inventory': 'products',
    'tables': 'tables',
    'orders': 'orders',
    'customers': 'customers',
    'employees': 'employees',
    'reports': 'reports',
    'system': 'system',
};

locales.forEach(lang => {
    const filePath = path.join(sourceDir, `${lang}.json`);
    if (!fs.existsSync(filePath)) {
        console.error(`Source file not found: ${filePath}`);
        return;
    }

    const content = JSON.parse(fs.readFileSync(filePath, 'utf8'));
    const namespaces = {};

    // Initialize namespaces
    Object.values(mappings).forEach(ns => {
        namespaces[ns] = {};
    });
    // catch-all for unmapped
    namespaces['legacy'] = {};

    Object.keys(content).forEach(key => {
        const targetNS = mappings[key] || 'legacy';
        // If mapping is 1:1 (e.g. auth -> auth), we can put it spread or kept as object?
        // "Namespace approach" usually means file 'auth.json' has keys 'login', 'logout'.
        // So we should SPREAD the content of content[key] into the root of the namespace object.
        // UNLESS it interacts with other keys.
        // e.g. content['common'] = { save: '...' } -> common.json = { save: '...' }

        if (typeof content[key] === 'object' && content[key] !== null) {
            Object.assign(namespaces[targetNS], content[key]);
        } else {
            namespaces[targetNS][key] = content[key];
        }
    });

    // Write files
    Object.keys(namespaces).forEach(ns => {
        if (Object.keys(namespaces[ns]).length === 0) return;

        const targetFile = path.join(sourceDir, lang, `${ns}.json`);
        fs.writeFileSync(targetFile, JSON.stringify(namespaces[ns], null, 2));
        console.log(`Wrote ${lang}/${ns}.json`);
    });
});
