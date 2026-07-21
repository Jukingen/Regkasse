import fs from 'node:fs/promises';
import path from 'node:path';
import {
  ROOT,
  createValidators,
  flattenObject,
  parseArgs,
  readJsonFileIfExists,
  readManifest,
  resolveAppConfig,
  tupleKey,
  writeReport,
} from './_shared.mjs';

const args = parseArgs(process.argv);
const manifest = await readManifest();
const appIds = (args.app ? [args.app] : Object.keys(manifest.apps)).sort((a, b) => a.localeCompare(b));
const strictMissing = args.strictMissing === 'true';
const keyMode = args.keyMode ?? 'compat';
if (keyMode !== 'compat' && keyMode !== 'target') {
  throw new Error(`Unknown --keyMode "${keyMode}". Allowed: compat|target`);
}
const COMPAT_KEY_PATTERN = args.keyRegex ?? manifest.keyPattern ?? '^[a-z0-9][a-zA-Z0-9_]*(\\.[a-zA-Z0-9_]+)*$';
const TARGET_KEY_PATTERN = args.targetKeyRegex ?? manifest.targetKeyPattern ?? '^[a-z0-9]+(?:_[a-z0-9]+)*(?:\\.[a-z0-9]+(?:_[a-z0-9]+)*)*$';
const compatKeyRegex = new RegExp(COMPAT_KEY_PATTERN);
const targetKeyRegex = new RegExp(TARGET_KEY_PATTERN);
const reservedPrefixes = manifest.reservedPrefixes ?? ['product.', 'category.', 'modifier.'];
const orphanPolicyArg = args.orphanPolicy ?? args.orphanAsError;
const orphanPolicy =
  orphanPolicyArg === 'true' || orphanPolicyArg === 'error'
    ? 'error'
    : orphanPolicyArg === 'false' || orphanPolicyArg === 'warning'
      ? 'warning'
      : manifest.orphanNamespacePolicy === 'error'
        ? 'error'
        : 'warning';
const orphanAsError = orphanPolicy === 'error';

function pushIssue(target, value) {
  target.push(value);
}

function stableSortStrings(values) {
  return [...values].sort((a, b) => a.localeCompare(b));
}

function collectDuplicateJsonKeys(raw) {
  const duplicates = [];
  const stack = [];
  let i = 0;
  let line = 1;
  let col = 1;

  function bump(ch) {
    if (ch === '\n') {
      line += 1;
      col = 1;
    } else {
      col += 1;
    }
  }

  function skipWhitespace() {
    while (i < raw.length && /\s/.test(raw[i])) {
      bump(raw[i]);
      i += 1;
    }
  }

  function readString() {
    const startLine = line;
    const startCol = col;
    if (raw[i] !== '"') return null;
    i += 1;
    bump('"');
    let out = '';
    let escaped = false;
    while (i < raw.length) {
      const ch = raw[i];
      i += 1;
      bump(ch);
      if (escaped) {
        out += ch;
        escaped = false;
        continue;
      }
      if (ch === '\\') {
        escaped = true;
        continue;
      }
      if (ch === '"') {
        return { value: out, line: startLine, column: startCol };
      }
      out += ch;
    }
    return null;
  }

  while (i < raw.length) {
    skipWhitespace();
    if (i >= raw.length) break;
    const ch = raw[i];

    if (ch === '{') {
      stack.push(new Set());
      i += 1;
      bump(ch);
      continue;
    }
    if (ch === '}') {
      if (stack.length > 0) stack.pop();
      i += 1;
      bump(ch);
      continue;
    }
    if (ch === '"') {
      const str = readString();
      if (!str) continue;
      skipWhitespace();
      if (raw[i] === ':') {
        if (stack.length > 0) {
          const current = stack[stack.length - 1];
          if (current.has(str.value)) {
            duplicates.push({ key: str.value, line: str.line, column: str.column });
          } else {
            current.add(str.value);
          }
        }
        i += 1;
        bump(':');
      }
      continue;
    }

    i += 1;
    bump(ch);
  }

  return duplicates;
}

const failures = [];
const warnings = [];
const perApp = [];

function toKebabCase(input) {
  return String(input ?? '')
    .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
    .replace(/_/g, '-')
    .toLowerCase();
}

async function extractFrontendRuntimeNamespaces(root) {
  const file = path.join(root, 'frontend', 'i18n', 'index.ts');
  const source = await fs.readFile(file, 'utf8').catch(() => '');
  if (!source) return [];
  const match = source.match(/en:\s*\{([\s\S]*?)\}\s*,\s*de:/m);
  if (!match) return [];
  return [...match[1].matchAll(/^\s*([a-zA-Z0-9_-]+)\s*:/gm)].map((m) => m[1]);
}

async function extractAdminRuntimeNamespaces(root) {
  const file = path.join(root, 'frontend-admin', 'src', 'i18n', 'config.ts');
  const source = await fs.readFile(file, 'utf8').catch(() => '');
  if (!source) return [];
  const match = source.match(/de:\s*\{([\s\S]*?)\}\s*,\s*en:/m);
  if (!match) return [];
  return [...match[1].matchAll(/([a-zA-Z0-9_-]+)\s*:/g)].map((m) => m[1]);
}

for (const appId of appIds) {
  const app = resolveAppConfig(manifest, appId);
  const validators = createValidators(manifest, app);
  const locales = manifest.locales;
  const defaultLocale = manifest.defaultLocale;
  const seenTriples = new Set();
  const appFailures = [];
  const appWarnings = [];
  const appTargetViolations = [];
  const parity = {};
  const orphanNamespaceFiles = [];
  const runtimeConfigMismatches = [];

  // 8) Orphan namespace files + file system namespace matrix
  const fsNamespaces = new Set();
  for (const locale of locales) {
    const localeDir = path.join(app.localesDirAbs, locale);
    const files = (await fs.readdir(localeDir).catch(() => [])).sort((a, b) => a.localeCompare(b));
    for (const file of files) {
      if (!file.endsWith('.json')) continue;
      const namespace = file.replace(/\.json$/, '');
      fsNamespaces.add(namespace);
      if (!app.namespaces.includes(namespace)) {
        orphanNamespaceFiles.push(`${locale}/${file}`);
        const msg = `${app.id}: orphan namespace file ${locale}/${file} (not in manifest allowlist)`;
        if (orphanAsError) pushIssue(appFailures, msg);
        else pushIssue(appWarnings, msg);
      }

      const raw = await fs.readFile(path.join(localeDir, file), 'utf8').catch(() => '');
      if (raw) {
        const duplicates = collectDuplicateJsonKeys(raw);
        for (const dup of duplicates) {
          pushIssue(
            appFailures,
            `${app.id}: duplicate key in ${locale}/${file} -> "${dup.key}" at ${dup.line}:${dup.column}`
          );
        }
      }
    }
  }

  // 9) Runtime config <-> file system mismatch
  const runtimeNamespacesRaw = app.id === 'frontend'
    ? await extractFrontendRuntimeNamespaces(ROOT)
    : await extractAdminRuntimeNamespaces(ROOT);
  const runtimeNamespacesNormalized = runtimeNamespacesRaw.map((ns) => toKebabCase(ns));
  const manifestSet = new Set(app.namespaces);
  const fsSet = new Set([...fsNamespaces]);
  for (const runtimeNs of runtimeNamespacesRaw) {
    const runtimeKebab = toKebabCase(runtimeNs);
    if (!manifestSet.has(runtimeNs) && !manifestSet.has(runtimeKebab)) {
      runtimeConfigMismatches.push(`runtime namespace "${runtimeNs}" not found in manifest allowlist`);
      appFailures.push(`${app.id}: runtime namespace "${runtimeNs}" not found in manifest allowlist`);
    }
    if (!fsSet.has(runtimeNs) && !fsSet.has(runtimeKebab)) {
      runtimeConfigMismatches.push(`runtime namespace "${runtimeNs}" has no filesystem namespace file`);
      appFailures.push(`${app.id}: runtime namespace "${runtimeNs}" has no filesystem namespace file`);
    }
  }

  // 1..7) manifest allowlist, duplicates, missing, key regex, reserved prefix, parity
  for (const namespace of app.namespaces) {
    if (!validators.validateNamespace(namespace)) {
      appFailures.push(`${app.id}: namespace not in allowlist: ${namespace}`);
      continue;
    }

    const defaultPath = path.join(app.localesDirAbs, defaultLocale, `${namespace}.json`);
    let defaultJson = null;
    try {
      defaultJson = await readJsonFileIfExists(defaultPath);
    } catch (error) {
      appFailures.push(`${app.id}: ${String(error?.message ?? error)}`);
      parity[namespace] = { defaultKeys: 0, missingDeValues: 0, missingByLocale: Object.fromEntries(locales.filter((l) => l !== defaultLocale).map((l) => [l, 0])) };
      continue;
    }
    if (!defaultJson) {
      appFailures.push(`${app.id}: missing required ${defaultLocale}/${namespace}.json`);
      parity[namespace] = { defaultKeys: 0, missingDeValues: 0, missingByLocale: Object.fromEntries(locales.filter((l) => l !== defaultLocale).map((l) => [l, 0])) };
      continue;
    }

    const defaultFlat = flattenObject(defaultJson);
    const defaultKeys = Object.keys(defaultFlat).sort();
    let missingDeValues = 0;
    const missingByLocale = Object.fromEntries(locales.filter((l) => l !== defaultLocale).map((l) => [l, 0]));

    for (const key of defaultKeys) {
      if (!compatKeyRegex.test(key)) {
        appFailures.push(`${app.id}: invalid key regex "${namespace}.${key}" (expected ${COMPAT_KEY_PATTERN})`);
      }
      if (!targetKeyRegex.test(key)) {
        const msg = `${app.id}: target key policy violation "${namespace}.${key}" (expected ${TARGET_KEY_PATTERN})`;
        appTargetViolations.push(msg);
        if (keyMode === 'target') appFailures.push(msg);
        else appWarnings.push(msg);
      }
      for (const prefix of reservedPrefixes) {
        if (key.startsWith(prefix)) {
          appFailures.push(`${app.id}: reserved prefix violation "${namespace}.${key}"`);
        }
      }

      const tuple = tupleKey(app.id, namespace, key);
      if (seenTriples.has(tuple)) {
        appFailures.push(`${app.id}: duplicate tuple ${tuple}`);
      }
      seenTriples.add(tuple);

      if (!String(defaultFlat[key] ?? '').trim()) {
        missingDeValues += 1;
        appFailures.push(`${app.id}: missing de value for ${namespace}.${key}`);
      }
    }

    for (const locale of locales) {
      if (locale === defaultLocale) continue;
      const localePath = path.join(app.localesDirAbs, locale, `${namespace}.json`);
      let localeJson = null;
      try {
        localeJson = await readJsonFileIfExists(localePath);
      } catch (error) {
        appFailures.push(`${app.id}: ${String(error?.message ?? error)}`);
        continue;
      }
      if (!localeJson) {
        missingByLocale[locale] += defaultKeys.length;
        appWarnings.push(`${app.id}: missing optional ${locale}/${namespace}.json`);
        continue;
      }
      const localeFlat = flattenObject(localeJson);
      for (const key of defaultKeys) {
        if (!(key in localeFlat) || !String(localeFlat[key] ?? '').trim()) {
          missingByLocale[locale] += 1;
          const msg = `${app.id}: missing ${locale} translation for ${namespace}.${key}`;
          if (strictMissing) appFailures.push(msg);
          else appWarnings.push(msg);
        }
      }
      for (const extra of Object.keys(localeFlat)) {
        if (!defaultKeys.includes(extra)) {
          appWarnings.push(`${app.id}: extra key in ${locale}/${namespace}.json -> ${extra}`);
        }
      }
    }

    parity[namespace] = {
      defaultKeys: defaultKeys.length,
      missingDeValues,
      missingByLocale,
    };
  }

  failures.push(...appFailures);
  warnings.push(...appWarnings);
  perApp.push({
    orphanAsError,
    app: app.id,
    strictMissing,
    keyRegex: COMPAT_KEY_PATTERN,
    targetKeyRegex: TARGET_KEY_PATTERN,
    keyMode,
    reservedPrefixes,
    namespaceAllowlistCount: app.namespaces.length,
    filesystemNamespaceCount: fsNamespaces.size,
    runtimeNamespaceCount: runtimeNamespacesRaw.length,
    runtimeNamespaces: runtimeNamespacesRaw,
    runtimeNamespacesNormalized,
    runtimeConfigMismatches,
    orphanNamespaceFiles,
    targetViolationCount: appTargetViolations.length,
    targetViolations: appTargetViolations,
    localeParity: parity,
    failureCount: appFailures.length,
    warningCount: appWarnings.length,
  });
}

// Human-readable report
for (const appSummary of perApp) {
  console.log(`\n=== Localization Validate :: ${appSummary.app} ===`);
  console.log(`Key mode: ${appSummary.keyMode}`);
  console.log(`Compat key regex: ${appSummary.keyRegex}`);
  console.log(`Target key regex: ${appSummary.targetKeyRegex}`);
  console.log(`Reserved prefixes: ${appSummary.reservedPrefixes.join(', ')}`);
  console.log(`Allowlist namespaces: ${appSummary.namespaceAllowlistCount}, FS namespaces: ${appSummary.filesystemNamespaceCount}, Runtime namespaces: ${appSummary.runtimeNamespaceCount}`);
  console.log(`Failures: ${appSummary.failureCount}, Warnings: ${appSummary.warningCount}, Target violations: ${appSummary.targetViolationCount}`);
  const parityRows = Object.entries(appSummary.localeParity)
    .map(([namespace, data]) => {
      const missingLocales = Object.entries(data.missingByLocale).map(([locale, count]) => `${locale}:${count}`).join(', ');
      return `- ${namespace}: keys=${data.defaultKeys}, missingDe=${data.missingDeValues}, missing(${missingLocales})`;
    })
    .slice(0, 20);
  if (parityRows.length > 0) {
    console.log('Locale parity (sample):');
    parityRows.forEach((row) => console.log(row));
  }
}

const sortedWarnings = stableSortStrings(warnings);
const sortedFailures = stableSortStrings(failures);

if (sortedWarnings.length > 0) {
  console.warn(`\nValidation warnings (${warnings.length}):`);
  sortedWarnings.slice(0, 50).forEach((w) => console.warn(`- ${w}`));
  if (sortedWarnings.length > 50) console.warn(`- ... ${sortedWarnings.length - 50} more`);
}

const reportPath = await writeReport('validate-report', args.app ?? 'all', {
  appIds,
  strictMissing,
  orphanAsError,
  keyMode,
  keyRegex: COMPAT_KEY_PATTERN,
  targetKeyRegex: TARGET_KEY_PATTERN,
  reservedPrefixes,
  perApp: perApp.sort((a, b) => a.app.localeCompare(b.app)),
  warnings: sortedWarnings,
  failures: sortedFailures,
  summary: {
    failureCount: sortedFailures.length,
    warningCount: sortedWarnings.length,
  },
  timestamp: new Date().toISOString(),
});
console.log(`\nJSON report: ${reportPath}`);
const textReportPath = await writeTextReport({
  appIds,
  strictMissing,
  orphanPolicy,
  keyMode,
  keyRegex: COMPAT_KEY_PATTERN,
  targetKeyRegex: TARGET_KEY_PATTERN,
  reservedPrefixes,
  perApp: perApp.sort((a, b) => a.app.localeCompare(b.app)),
  warnings: sortedWarnings,
  failures: sortedFailures,
});
console.log(`Text report: ${textReportPath}`);

// Exit code policy
// - exit 1: any failure
// - exit 0: warnings only / clean
if (sortedFailures.length > 0) {
  console.error('\nValidation failed:');
  sortedFailures.forEach((f) => console.error(`- ${f}`));
  console.error('\nHow to fix:');
  console.error('- Orphan JSON: add namespace to localization/namespace-manifest.json or remove stray files');
  console.error('- Missing de value / duplicate keys: edit locale JSON under app localesDir');
  console.error('- strictMissing en/tr gaps: fill en/ and tr/ files to match de keys');
  console.error('- Report: localization/out/reports/validate-report.<app>.json');
  process.exit(1);
}

console.log('\nTranslation validation passed.');

async function writeTextReport(payload) {
  const reportDir = path.join(ROOT, 'localization', 'out', 'reports');
  await fs.mkdir(reportDir, { recursive: true });
  const filePath = path.join(reportDir, `validate-report.${args.app ?? 'all'}.txt`);
  const lines = [];
  lines.push('Localization Validation Report');
  lines.push(`apps: ${payload.appIds.join(', ')}`);
  lines.push(`keyMode: ${payload.keyMode}`);
  lines.push(`strictMissing: ${payload.strictMissing}`);
  lines.push(`orphanPolicy: ${payload.orphanPolicy}`);
  lines.push(`compatKeyRegex: ${payload.keyRegex}`);
  lines.push(`targetKeyRegex: ${payload.targetKeyRegex}`);
  lines.push(`reservedPrefixes: ${payload.reservedPrefixes.join(', ')}`);
  lines.push(`failureCount: ${payload.failures.length}`);
  lines.push(`warningCount: ${payload.warnings.length}`);
  lines.push('');
  for (const appSummary of payload.perApp) {
    lines.push(`## ${appSummary.app}`);
    lines.push(`failures=${appSummary.failureCount}, warnings=${appSummary.warningCount}`);
    lines.push(`namespaces allowlist=${appSummary.namespaceAllowlistCount}, filesystem=${appSummary.filesystemNamespaceCount}, runtime=${appSummary.runtimeNamespaceCount}`);
    if (appSummary.runtimeConfigMismatches.length > 0) {
      lines.push('runtime mismatches:');
      for (const entry of stableSortStrings(appSummary.runtimeConfigMismatches)) {
        lines.push(`- ${entry}`);
      }
    }
    lines.push('locale parity:');
    for (const namespace of Object.keys(appSummary.localeParity).sort((a, b) => a.localeCompare(b))) {
      const data = appSummary.localeParity[namespace];
      const missingLocales = Object.entries(data.missingByLocale)
        .sort(([a], [b]) => a.localeCompare(b))
        .map(([locale, count]) => `${locale}:${count}`)
        .join(', ');
      lines.push(`- ${namespace}: keys=${data.defaultKeys}, missingDe=${data.missingDeValues}, missing(${missingLocales})`);
    }
    lines.push('');
  }
  if (payload.failures.length > 0) {
    lines.push('Failures:');
    for (const failure of payload.failures) lines.push(`- ${failure}`);
    lines.push('');
  }
  if (payload.warnings.length > 0) {
    lines.push('Warnings:');
    for (const warning of payload.warnings) lines.push(`- ${warning}`);
    lines.push('');
  }
  await fs.writeFile(filePath, `${lines.join('\n')}\n`, 'utf8');
  return filePath;
}
