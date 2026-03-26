import fs from 'node:fs/promises';
import path from 'node:path';
import {
  flattenObject,
  parseArgs,
  readJsonFileIfExists,
  readManifest,
  resolveAppConfig,
  writeReport,
} from './_shared.mjs';

const args = parseArgs(process.argv);
const manifest = await readManifest();
const appIds = (args.app ? [args.app] : Object.keys(manifest.apps)).sort((a, b) => a.localeCompare(b));
const strictMissing = args.strictMissing === 'true';
const strictDe = args.strictDe === 'true';
const hardcodedAsError = args.hardcodedAsError === 'true';

const failures = [];
const warnings = [];
const perApp = [];

for (const appId of appIds) {
  const app = resolveAppConfig(manifest, appId);
  const localeMatrix = await buildLocaleMatrix(app, manifest.locales);
  const knownNamespaces = new Set(app.namespaces);
  if (app.id === 'frontend-admin') {
    // runtime keys can be camelCase while files are kebab-case
    for (const ns of app.namespaces) {
      knownNamespaces.add(fromKebabToCamel(ns));
    }
  }
  const { files, usedEntries, hardcodedCandidates } = await scanUsage(app.id, knownNamespaces);

  const appFailures = [];
  const appWarnings = [];
  const missingByLocale = { de: [], en: [], tr: [] };
  const invalidNamespaceRefs = [];

  for (const entry of usedEntries) {
    const normalized = normalizeUsage(app.id, entry.rawKey, knownNamespaces);
    if (!normalized) continue;

    const { namespace, keyPath } = normalized;
    const resolvedNamespace = resolveNamespaceAlias(namespace, localeMatrix.namespaces, app.id);
    if (!resolvedNamespace) {
      invalidNamespaceRefs.push(formatUsageRef(entry, `invalid namespace "${namespace}"`));
      appFailures.push(`${app.id}: invalid namespace "${namespace}" in ${entry.file}:${entry.line}`);
      continue;
    }

    const deMap = localeMatrix.byLocale.de[resolvedNamespace] ?? {};
    const enMap = localeMatrix.byLocale.en[resolvedNamespace] ?? {};
    const trMap = localeMatrix.byLocale.tr[resolvedNamespace] ?? {};

    const composite = `${resolvedNamespace}.${keyPath}`;
    localeMatrix.usedKeys.add(composite);

    if (!hasValue(deMap, keyPath)) {
      missingByLocale.de.push(formatUsageRef(entry, composite));
      const msg = `${app.id}: missing de key ${composite} used at ${entry.file}:${entry.line}`;
      if (strictDe) appFailures.push(msg);
      else appWarnings.push(msg);
      continue;
    }

    if (!hasValue(enMap, keyPath)) {
      missingByLocale.en.push(formatUsageRef(entry, composite));
      const msg = `${app.id}: missing en key ${composite} used at ${entry.file}:${entry.line}`;
      if (strictMissing) appFailures.push(msg);
      else appWarnings.push(msg);
    }

    if (!hasValue(trMap, keyPath)) {
      missingByLocale.tr.push(formatUsageRef(entry, composite));
      const msg = `${app.id}: missing tr key ${composite} used at ${entry.file}:${entry.line}`;
      if (strictMissing) appFailures.push(msg);
      else appWarnings.push(msg);
    }
  }

  const unusedDeKeys = [];
  for (const [namespace, keyMap] of Object.entries(localeMatrix.byLocale.de)) {
    for (const keyPath of Object.keys(keyMap)) {
      const composite = `${namespace}.${keyPath}`;
      if (!localeMatrix.usedKeys.has(composite)) {
        unusedDeKeys.push(composite);
      }
    }
  }

  for (const hit of hardcodedCandidates) {
    const message = `${app.id}: hardcoded UI candidate ${hit.file}:${hit.line} -> ${hit.snippet}`;
    if (hardcodedAsError) appFailures.push(message);
    else appWarnings.push(message);
  }

  failures.push(...appFailures);
  warnings.push(...appWarnings);
  perApp.push({
    app: app.id,
    scannedFiles: files.length,
    usedKeyCount: usedEntries.length,
    uniqueUsedKeyCount: localeMatrix.usedKeys.size,
    invalidNamespaceRefs,
    missingByLocale: {
      de: dedupe(missingByLocale.de),
      en: dedupe(missingByLocale.en),
      tr: dedupe(missingByLocale.tr),
    },
    unusedDeKeys: dedupe(unusedDeKeys).sort((a, b) => a.localeCompare(b)),
    hardcodedCandidates: hardcodedCandidates.map((entry) => ({
      file: entry.file,
      line: entry.line,
      snippet: entry.snippet,
    })),
    failureCount: appFailures.length,
    warningCount: appWarnings.length,
  });
}

const sortedFailures = [...new Set(failures)].sort((a, b) => a.localeCompare(b));
const sortedWarnings = [...new Set(warnings)].sort((a, b) => a.localeCompare(b));

for (const appSummary of perApp) {
  console.log(`\n=== Localization Usage Check :: ${appSummary.app} ===`);
  console.log(`Files scanned: ${appSummary.scannedFiles}`);
  console.log(`Used keys: ${appSummary.usedKeyCount} (unique ${appSummary.uniqueUsedKeyCount})`);
  console.log(
    `Missing used keys -> de:${appSummary.missingByLocale.de.length} en:${appSummary.missingByLocale.en.length} tr:${appSummary.missingByLocale.tr.length}`,
  );
  console.log(`Invalid namespace references: ${appSummary.invalidNamespaceRefs.length}`);
  console.log(`Unused de keys (feasible): ${appSummary.unusedDeKeys.length}`);
  console.log(`Hardcoded UI candidates: ${appSummary.hardcodedCandidates.length}`);
  console.log(`Failures: ${appSummary.failureCount}, Warnings: ${appSummary.warningCount}`);
}

const reportPath = await writeReport('usage-report', args.app ?? 'all', {
  appIds,
  strictMissing,
  strictDe,
  hardcodedAsError,
  perApp,
  warnings: sortedWarnings,
  failures: sortedFailures,
  summary: {
    failureCount: sortedFailures.length,
    warningCount: sortedWarnings.length,
  },
  timestamp: new Date().toISOString(),
});
console.log(`\nUsage report: ${reportPath}`);

if (sortedWarnings.length > 0) {
  console.warn(`Warnings (${sortedWarnings.length}):`);
  sortedWarnings.slice(0, 50).forEach((item) => console.warn(`- ${item}`));
  if (sortedWarnings.length > 50) console.warn(`- ... ${sortedWarnings.length - 50} more`);
}

if (sortedFailures.length > 0) {
  console.error('\nLocalization usage check failed:');
  sortedFailures.forEach((item) => console.error(`- ${item}`));
  process.exit(1);
}

console.log('\nLocalization usage check passed.');

async function buildLocaleMatrix(app, locales) {
  const byLocale = { de: {}, en: {}, tr: {} };
  const namespaces = new Set();
  for (const locale of locales) {
    const localeDir = path.join(app.localesDirAbs, locale);
    const files = (await fs.readdir(localeDir).catch(() => [])).filter((f) => f.endsWith('.json'));
    for (const file of files) {
      const namespace = file.replace(/\.json$/, '');
      namespaces.add(namespace);
      const json = await readJsonFileIfExists(path.join(localeDir, file));
      const flat = flattenObject(json ?? {});
      byLocale[locale][namespace] = flat;
    }
  }
  return {
    byLocale,
    namespaces,
    usedKeys: new Set(),
  };
}

async function scanUsage(appId, knownNamespaces) {
  const roots = appId === 'frontend'
    ? ['frontend/app', 'frontend/components', 'frontend/contexts', 'frontend/features', 'frontend/hooks', 'frontend/services']
    : ['frontend-admin/src'];

  const files = [];
  for (const relRoot of roots) {
    const absRoot = path.resolve(relRoot);
    await walk(absRoot, (file) => {
      if (!/\.(ts|tsx|js|jsx)$/.test(file)) return;
      if (isIgnoredPath(file)) return;
      files.push(file);
    });
  }

  const usedEntries = [];
  const hardcodedCandidates = [];
  for (const file of files) {
    const content = await fs.readFile(file, 'utf8').catch(() => '');
    if (!content) continue;
    const isJsxLike = /\.(tsx|jsx)$/.test(file);

    for (const match of content.matchAll(/\b(?:i18n\.)?t\(\s*['"`]([^'"`]+)['"`]\s*[,)]/g)) {
      const rawKey = match[1];
      const { line } = offsetToLineCol(content, match.index ?? 0);
      usedEntries.push({
        file: toRelative(file),
        line,
        rawKey,
      });
    }

    if (!isJsxLike) continue;

    const lines = content.split('\n');
    lines.forEach((lineText, index) => {
      const line = index + 1;
      if (lineText.includes('t(') || lineText.includes('i18n.t(')) return;
      if (/^\s*\/\//.test(lineText) || /^\s*\*/.test(lineText)) return;
      if (/console\.(log|warn|error)\(/.test(lineText)) return;

      // JSX text nodes: >Text< candidates
      const jsxTextMatch = lineText.includes('</')
        ? lineText.match(/>\s*([A-Za-zÄÖÜäöüß][^<{]{2,})\s*</)
        : null;
      if (jsxTextMatch && isLikelyUserFacingText(jsxTextMatch[1])) {
        hardcodedCandidates.push({
          file: toRelative(file),
          line,
          snippet: truncate(jsxTextMatch[1].trim()),
        });
      }

      // Common UI props likely user-facing.
      if (!lineText.includes('<')) return;
      const propRegex = /\b(?:title|subtitle|label|placeholder|message|description|okText|cancelText|headerTitle|accessibilityLabel)\s*=\s*['"`]([^'"`]+)['"`]/g;
      for (const propMatch of lineText.matchAll(propRegex)) {
        const text = propMatch[1];
        if (!isLikelyUserFacingText(text)) continue;
        hardcodedCandidates.push({
          file: toRelative(file),
          line,
          snippet: truncate(text),
        });
      }
    });
  }

  return {
    files: dedupe(files.map((f) => toRelative(f))),
    usedEntries,
    hardcodedCandidates: dedupeObjects(hardcodedCandidates),
  };
}

function normalizeUsage(appId, rawKey, knownNamespaces) {
  if (!rawKey || typeof rawKey !== 'string') return null;
  const key = rawKey.trim();
  if (!key) return null;

  if (key.includes(':')) {
    const [namespace, keyPath] = key.split(':', 2);
    if (!namespace || !keyPath) return null;
    return { namespace: namespace.trim(), keyPath: keyPath.trim() };
  }

  if (appId === 'frontend-admin') {
    const [namespace, ...rest] = key.split('.');
    if (!namespace || rest.length === 0) return null;
    return { namespace: namespace.trim(), keyPath: rest.join('.').trim() };
  }

  // frontend i18next: allow "namespace.path" if first segment is a known namespace.
  if (appId === 'frontend') {
    const [first, ...rest] = key.split('.');
    if (knownNamespaces.has(first) && rest.length > 0) {
      return { namespace: first, keyPath: rest.join('.').trim() };
    }
  }

  // i18next default namespace fallback
  return { namespace: 'common', keyPath: key };
}

function resolveNamespaceAlias(namespace, namespaces, appId) {
  if (namespaces.has(namespace)) return namespace;
  if (appId === 'frontend-admin') {
    const kebab = namespace
      .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
      .replace(/_/g, '-')
      .toLowerCase();
    if (namespaces.has(kebab)) return kebab;
  }
  return null;
}

function hasValue(flatMap, keyPath) {
  if (!flatMap || typeof flatMap !== 'object') return false;
  return Object.prototype.hasOwnProperty.call(flatMap, keyPath) && String(flatMap[keyPath] ?? '').trim().length > 0;
}

function formatUsageRef(entry, value) {
  return `${value} @ ${entry.file}:${entry.line}`;
}

function dedupe(items) {
  return [...new Set(items)];
}

function dedupeObjects(items) {
  const seen = new Set();
  const out = [];
  for (const item of items) {
    const token = `${item.file}|${item.line}|${item.snippet}`;
    if (seen.has(token)) continue;
    seen.add(token);
    out.push(item);
  }
  return out;
}

function truncate(value, max = 120) {
  return value.length > max ? `${value.slice(0, max - 3)}...` : value;
}

function isLikelyUserFacingText(value) {
  if (!value) return false;
  const trimmed = value.trim();
  if (trimmed.length < 3) return false;
  if (/^[A-Z0-9_ -]+$/.test(trimmed) && trimmed.length <= 2) return false;
  if (/^https?:\/\//.test(trimmed)) return false;
  if (/^[./\\]/.test(trimmed)) return false;
  return /[A-Za-zÄÖÜäöüß]/.test(trimmed);
}

function isIgnoredPath(filePath) {
  const normalized = filePath.replaceAll('\\', '/');
  return (
    normalized.includes('/node_modules/') ||
    normalized.includes('/.next/') ||
    normalized.includes('/dist/') ||
    normalized.includes('/build/') ||
    normalized.includes('/coverage/') ||
    normalized.includes('/__tests__/') ||
    normalized.includes('/test/') ||
    normalized.includes('/tests/') ||
    normalized.includes('/examples/') ||
    normalized.includes('/api/') ||
    normalized.includes('/api/generated/')
  );
}

function fromKebabToCamel(value) {
  return String(value).replace(/-([a-z])/g, (_, ch) => ch.toUpperCase());
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

function toRelative(filePath) {
  const rel = path.relative(process.cwd(), path.resolve(filePath));
  return rel || '.';
}

function offsetToLineCol(text, offset) {
  const sliced = text.slice(0, offset);
  const lines = sliced.split('\n');
  return {
    line: lines.length,
    column: (lines[lines.length - 1]?.length ?? 0) + 1,
  };
}
