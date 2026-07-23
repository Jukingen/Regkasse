import fs from 'node:fs/promises';
import path from 'node:path';
import {
  flattenObject,
  parseArgs,
  readJsonFileIfExists,
  readManifest,
  resolveAppConfig,
  listActiveAppIds,
  writeReport,
} from './_shared.mjs';

const args = parseArgs(process.argv);
const manifest = await readManifest();
const appIds = args.app ? [args.app] : listActiveAppIds(manifest);
const warnOnly = args['warn-only'] === 'true';

const failures = [];
const warnings = [];
const violations = [];

const SOURCE_PATTERNS = [
  {
    code: 'NO_T_PRODUCT_NAME',
    severity: 'error',
    regex: /\bt\(\s*product\.name\b/g,
    message: 'Forbidden dynamic key from product.name',
  },
  {
    code: 'NO_T_CATEGORY_NAME',
    severity: 'error',
    regex: /\bt\(\s*(category|cat)\.name\b/g,
    message: 'Forbidden dynamic key from category.name/cat.name',
  },
  {
    code: 'NO_T_DYNAMIC_TEMPLATE',
    severity: 'error',
    regex: /\bt\(\s*`[^`]*\$\{[^`]*`/g,
    message: 'Forbidden template-literal translation key',
  },
  {
    code: 'NO_T_BACKEND_DATA_KEY',
    severity: 'error',
    regex: /\bt\(\s*(item|row|record|product|category|modifier|apiData|response|payload)\.[a-zA-Z_$][\w$]*(\.[a-zA-Z_$][\w$]*)*\s*[,)]/g,
    message: 'Forbidden translation key derived from backend/runtime data',
  },
  {
    code: 'NO_I18N_T_DYNAMIC_VALUE',
    severity: 'warning',
    regex: /\bi18n\.t\(\s*[a-zA-Z_$][\w$.]*\s*[,)]/g,
    message: 'Potential dynamic non-literal key in i18n.t(...)',
  },
  {
    code: 'NO_T_DYNAMIC_VALUE',
    severity: 'warning',
    regex: /\bt\(\s*[a-zA-Z_$][\w$.]*\s*[,)]/g,
    message: 'Potential dynamic non-literal key in t(...)',
  },
];

for (const appId of appIds) {
  const app = resolveAppConfig(manifest, appId);
  if (app.deferred) {
    console.log(`Skipping deferred app "${appId}" (no locale catalog yet).`);
    continue;
  }
  const appRoot = path.dirname(app.localesDirAbs);
  await scanSource(app.id, appRoot);
  await scanLocaleKeys(app.id, app.localesDirAbs, manifest.defaultLocale);
}

const reportPath = await writeReport('boundary-report', args.app ?? 'all', {
  appIds,
  failures,
  warnings,
  violations,
  timestamp: new Date().toISOString(),
});

console.log(`Report: ${reportPath}`);
if (warnings.length > 0) {
  console.warn(`Boundary warnings (${warnings.length}):`);
  warnings.slice(0, 50).forEach((w) => console.warn(`- ${w}`));
  if (warnings.length > 50) console.warn(`- ... ${warnings.length - 50} more`);
}
if (failures.length > 0) {
  if (warnOnly) {
    console.warn(`Boundary failures downgraded to warnings because --warn-only=true (${failures.length})`);
    failures.forEach((f) => console.warn(`- ${f}`));
    console.log('Boundary check passed in warn-only mode.');
    process.exit(0);
  }
  console.error('Boundary check failed:');
  failures.forEach((f) => console.error(`- ${f}`));
  process.exit(1);
}
console.log('Boundary check passed.');

async function scanSource(appId, rootDir) {
  await walk(rootDir, async (filePath) => {
    if (!filePath.endsWith('.ts') && !filePath.endsWith('.tsx')) return;
    if (filePath.includes(`${path.sep}node_modules${path.sep}`)) return;
    if (filePath.includes(`${path.sep}.next${path.sep}`)) return;
    const content = await fs.readFile(filePath, 'utf8');
    for (const pattern of SOURCE_PATTERNS) {
      const matches = [...content.matchAll(pattern.regex)];
      for (const match of matches) {
        const { line, column } = offsetToLineCol(content, match.index ?? 0);
        const snippet = (match[0] ?? '').slice(0, 120);
        const msg = `${appId}:${pattern.code}:${filePath}:${line}:${column}`;
        const violation = {
          app: appId,
          code: pattern.code,
          severity: pattern.severity,
          file: filePath,
          line,
          column,
          message: pattern.message,
          snippet,
        };
        violations.push(violation);
        if (pattern.severity === 'warning') warnings.push(msg);
        else failures.push(msg);
      }
    }
  });
}

async function scanLocaleKeys(appId, localesDirAbs, defaultLocale) {
  const deDir = path.join(localesDirAbs, defaultLocale);
  const files = await fs.readdir(deDir).catch(() => []);
  for (const file of files) {
    if (!file.endsWith('.json')) continue;
    const namespace = file.replace(/\.json$/, '');
    const json = await readJsonFileIfExists(path.join(deDir, file));
    if (!json) continue;
    const flat = flattenObject(json);
    for (const key of Object.keys(flat)) {
      // Anti-pattern: products.<real_category_name> (human/domain names as keys)
      if (
        namespace === 'products' &&
        !key.includes('.') &&
        (/^[A-Z]/.test(key) || /[\s&]/.test(key) || /[^\x00-\x7F]/.test(key))
      ) {
        const msg = `${appId}:PRODUCTS_DOMAIN_LIKE_KEY:${namespace}.${key}`;
        failures.push(msg);
        violations.push({
          app: appId,
          code: 'PRODUCTS_DOMAIN_LIKE_KEY',
          severity: 'error',
          file: path.join(deDir, file),
          line: null,
          column: null,
          message: 'Domain-like human names must not be used as translation keys',
          snippet: `${namespace}.${key}`,
        });
      }
    }
  }
}

async function walk(dir, onFile) {
  const entries = await fs.readdir(dir, { withFileTypes: true }).catch(() => []);
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      await walk(full, onFile);
      continue;
    }
    await onFile(full);
  }
}

function offsetToLineCol(text, offset) {
  const sliced = text.slice(0, offset);
  const lines = sliced.split('\n');
  return {
    line: lines.length,
    column: (lines[lines.length - 1]?.length ?? 0) + 1,
  };
}
