import fs from 'node:fs/promises';
import path from 'node:path';
import {
  atomicWriteFile,
  createValidators,
  ensureDir,
  parseArgs,
  parseCsv,
  readJsonFileIfExists,
  readManifest,
  resolveAppConfig,
  sortFlatEntries,
  sortObjectDeep,
  tupleKey,
  unflattenObject,
  writeReport,
} from './_shared.mjs';

const args = parseArgs(process.argv);
const appId = args.app;
if (!appId) throw new Error('Usage: node localization/scripts/import-translations.mjs --app <frontend|frontend-admin> [--input <csv>] [--dry-run]');

const manifest = await readManifest();
const app = resolveAppConfig(manifest, appId);
const locales = manifest.locales;
const validators = createValidators(manifest, app);

const inputPath = args.input ? path.resolve(args.input) : app.csvPathAbs;
const dryRun = args['dry-run'] === 'true';

const content = await fs.readFile(inputPath, 'utf8');
const rows = parseCsv(content);
const failures = [];
const warnings = [];
const duplicates = [];
const invalidRows = [];
const missingRows = [];
const requiredFieldRows = [];
const wrongAppRows = [];
const missingOptionalRows = [];

const byNamespaceByLocale = {};
const seenTriples = new Set();

const requiredColumns = ['app', 'namespace', 'key', ...locales, 'description', 'status', 'notes'];
const detectedColumns = rows.length > 0 ? Object.keys(rows[0]) : [];
for (const col of requiredColumns) {
  if (!detectedColumns.includes(col)) {
    failures.push(`Missing required CSV column: ${col}`);
  }
}

for (let idx = 0; idx < rows.length; idx += 1) {
  const row = rows[idx];
  const rowNo = idx + 2; // include header row
  const csvApp = String(row.app ?? '').trim();
  const namespace = String(row.namespace ?? '').trim();
  const key = String(row.key ?? '').trim();

  if (!csvApp || !namespace || !key) {
    const reason = `Row ${rowNo} missing required app|namespace|key`;
    failures.push(reason);
    requiredFieldRows.push({ rowNo, reason, row });
    continue;
  }

  if (csvApp !== app.id) {
    const reason = `Row ${rowNo} belongs to app "${csvApp}", expected "${app.id}"`;
    wrongAppRows.push({ rowNo, reason, row });
    continue;
  }

  const triple = tupleKey(app.id, namespace, key);

  if (seenTriples.has(triple)) {
    const msg = `Duplicate tuple in CSV: ${triple} (row ${rowNo})`;
    failures.push(msg);
    duplicates.push(msg);
    continue;
  }
  seenTriples.add(triple);

  if (!validators.validateNamespace(namespace)) {
    const msg = `Namespace not allowed: ${namespace} (row ${rowNo})`;
    failures.push(msg);
    invalidRows.push({ rowNo, triple, reason: msg });
    continue;
  }
  if (!validators.validateKey(key)) {
    const msg = `Invalid key format or reserved prefix: ${namespace}.${key} (row ${rowNo})`;
    failures.push(msg);
    invalidRows.push({ rowNo, triple, reason: msg });
    continue;
  }
  if (!String(row[manifest.defaultLocale] ?? '').trim()) {
    const msg = `Missing required "${manifest.defaultLocale}" text for ${triple} (row ${rowNo})`;
    failures.push(msg);
    missingRows.push({ rowNo, triple, reason: msg });
    continue;
  }

  if (!byNamespaceByLocale[namespace]) byNamespaceByLocale[namespace] = {};
  for (const locale of locales) {
    if (!byNamespaceByLocale[namespace][locale]) byNamespaceByLocale[namespace][locale] = {};
    byNamespaceByLocale[namespace][locale][key] = String(row[locale] ?? '');
    if (locale !== manifest.defaultLocale && !String(row[locale] ?? '').trim()) {
      const warning = `Missing optional ${locale} translation for ${triple} (row ${rowNo})`;
      warnings.push(warning);
      missingOptionalRows.push({ rowNo, triple, locale, warning });
    }
  }
}

if (failures.length > 0) {
  console.error('Import failed:');
  for (const f of failures) console.error(`- ${f}`);
  process.exit(1);
}

let writeCount = 0;
let changedFiles = 0;
for (const namespace of Object.keys(byNamespaceByLocale).sort((a, b) => a.localeCompare(b))) {
  for (const locale of locales) {
    const flat = byNamespaceByLocale[namespace][locale] ?? {};
    const sortedFlat = Object.fromEntries(sortFlatEntries(flat));
    const nested = sortObjectDeep(unflattenObject(sortedFlat));
    const target = path.join(app.localesDirAbs, locale, `${namespace}.json`);
    const payload = `${JSON.stringify(nested, null, 2)}\n`;
    const currentJson = await readJsonFileIfExists(target);
    const currentPayload = currentJson ? `${JSON.stringify(currentJson, null, 2)}\n` : '';
    const changed = currentPayload !== payload;
    if (!dryRun) {
      await ensureDir(path.dirname(target));
      if (changed) {
        await atomicWriteFile(target, payload, 'utf8');
        changedFiles += 1;
      }
    } else if (changed) {
      changedFiles += 1;
    }
    writeCount += 1;
  }
}

if (dryRun) console.log(`Dry-run complete. Would evaluate ${writeCount} locale files, ${changedFiles} changed.`);
else console.log(`Imported ${rows.length} rows, evaluated ${writeCount} locale files, wrote ${changedFiles} changed files.`);

if (wrongAppRows.length > 0) {
  warnings.push(`Skipped ${wrongAppRows.length} rows that belong to other apps`);
}

if (warnings.length > 0) {
  console.warn(`Warnings (${warnings.length}):`);
  warnings.slice(0, 20).forEach((w) => console.warn(`- ${w}`));
  if (warnings.length > 20) console.warn(`- ... ${warnings.length - 20} more`);
}

const report = {
  app: app.id,
  inputCsv: inputPath,
  dryRun,
  rowCount: rows.length,
  processedTuples: seenTriples.size,
  evaluatedFiles: writeCount,
  changedFiles,
  summary: {
    failureCount: failures.length,
    warningCount: warnings.length,
    duplicateCount: duplicates.length,
    invalidRowCount: invalidRows.length,
    missingDefaultCount: missingRows.length,
    missingOptionalCount: missingOptionalRows.length,
    requiredFieldErrorCount: requiredFieldRows.length,
    wrongAppRowCount: wrongAppRows.length,
  },
  errors: {
    failures,
    duplicates,
    invalidRows,
    missingRows,
    requiredFieldRows,
    wrongAppRows,
  },
  warnings: {
    messages: warnings,
    missingOptionalRows,
  },
  timestamp: new Date().toISOString(),
};
const reportPath = await writeReport('import-report', app.id, report);
console.log(`Report: ${reportPath}`);
