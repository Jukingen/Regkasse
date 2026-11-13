import fs from 'node:fs/promises';
import path from 'node:path';
import {
  atomicWriteFile,
  createValidators,
  csvEscape,
  ensureDir,
  flattenObject,
  indexRowsByTuple,
  parseArgs,
  parseCsv,
  readJsonFileIfExists,
  readManifest,
  resolveAppConfig,
  listActiveAppIds,
  tupleKey,
  writeReport,
} from './_shared.mjs';

const args = parseArgs(process.argv);
const manifest = await readManifest();
const locales = manifest.locales;
const dryRun = args['dry-run'] === 'true';
const selectedApp = args.app;
const appIds =
  !selectedApp || selectedApp === 'all'
    ? listActiveAppIds(manifest)
    : [selectedApp];

const rows = [];
const seenTriples = new Set();
const warnings = [];
const failures = [];
const missingByLocale = Object.fromEntries(locales.map((locale) => [locale, 0]));

for (const appId of appIds) {
  const app = resolveAppConfig(manifest, appId);
  if (app.deferred) {
    console.log(`Skipping deferred app "${appId}" (no locale catalog yet).`);
    continue;
  }
  const validators = createValidators(manifest, app);

  let existingIndex = new Map();
  try {
    const previous = await fs.readFile(app.csvPathAbs, 'utf8');
    existingIndex = indexRowsByTuple(parseCsv(previous));
  } catch {
    existingIndex = new Map();
  }

  const namespaces = [...app.namespaces].sort((a, b) => a.localeCompare(b));
  for (const namespace of namespaces) {
    if (!validators.validateNamespace(namespace)) {
      failures.push(`Invalid namespace in manifest for ${app.id}: ${namespace}`);
      continue;
    }

    const byLocale = {};
    for (const locale of locales) {
      const filePath = path.join(app.localesDirAbs, locale, `${namespace}.json`);
      const json = await readJsonFileIfExists(filePath);
      if (!json) {
        byLocale[locale] = {};
        continue;
      }
      byLocale[locale] = flattenObject(json);
    }

    const sourceKeys = Object.keys(byLocale[manifest.defaultLocale] ?? {}).sort();
    if (sourceKeys.length === 0) {
      warnings.push(`${app.id}:${namespace} has no keys in required default locale "${manifest.defaultLocale}"`);
      continue;
    }

    for (const key of sourceKeys) {
      if (!validators.validateKey(key)) {
        const message = `${app.id}:${namespace}:${key} has invalid key format or reserved prefix`;
        failures.push(message);
        continue;
      }
      const triple = tupleKey(app.id, namespace, key);
      if (seenTriples.has(triple)) {
        failures.push(`Duplicate tuple found: ${triple}`);
        continue;
      }
      seenTriples.add(triple);
      const old = existingIndex.get(triple) ?? {};
      const row = {
        app: app.id,
        namespace,
        key,
        description: old.description ?? '',
        status: old.status ?? '',
        notes: old.notes ?? '',
      };
      for (const locale of locales) {
        row[locale] = byLocale[locale]?.[key] ?? '';
        if (locale !== manifest.defaultLocale && !row[locale]) {
          missingByLocale[locale] += 1;
          warnings.push(`Missing ${locale} translation for ${app.id}:${namespace}:${key}`);
        }
      }
      rows.push(row);
    }
  }
}

if (failures.length > 0) {
  console.error('Export failed:');
  for (const f of failures) console.error(`- ${f}`);
  process.exit(1);
}

rows.sort((a, b) =>
  a.app.localeCompare(b.app) ||
  a.namespace.localeCompare(b.namespace) ||
  a.key.localeCompare(b.key)
);

const header = ['app', 'namespace', 'key', 'description', ...locales, 'status', 'notes'];
const csv = [header.join(',')]
  .concat(rows.map((row) => header.map((column) => csvEscape(row[column])).join(',')))
  .join('\n');

const outputCsvAbs = selectedApp && selectedApp !== 'all'
  ? resolveAppConfig(manifest, selectedApp).csvPathAbs
  : path.join(process.cwd(), 'localization', 'out', 'translations.csv');

if (!dryRun) {
  await ensureDir(path.dirname(outputCsvAbs));
  await atomicWriteFile(outputCsvAbs, `${csv}\n`, 'utf8');
}

const report = {
  app: selectedApp && selectedApp !== 'all' ? selectedApp : 'all',
  apps: appIds,
  exportedRows: rows.length,
  dryRun,
  failures,
  warnings,
  missingByLocale,
  outputCsv: outputCsvAbs,
  timestamp: new Date().toISOString(),
};
const reportPath = await writeReport(
  'export-report',
  selectedApp && selectedApp !== 'all' ? selectedApp : 'all',
  report
);

if (dryRun) {
  console.log(`Dry-run complete. Would export ${rows.length} translation rows to ${outputCsvAbs}`);
} else {
  console.log(`Exported ${rows.length} translation rows to ${outputCsvAbs}`);
}
console.log(`Report: ${reportPath}`);
if (warnings.length > 0) {
  console.warn(`Warnings (${warnings.length}):`);
  warnings.slice(0, 20).forEach((w) => console.warn(`- ${w}`));
  if (warnings.length > 20) console.warn(`- ... ${warnings.length - 20} more`);
}
