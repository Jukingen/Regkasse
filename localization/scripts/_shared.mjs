import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const MANIFEST_PATH = path.join(ROOT, 'localization', 'namespace-manifest.json');

export function parseArgs(argv) {
  const args = {};
  for (let i = 2; i < argv.length; i += 1) {
    const token = argv[i];
    if (!token.startsWith('--')) continue;
    const key = token.slice(2);
    const value = argv[i + 1] && !argv[i + 1].startsWith('--') ? argv[++i] : 'true';
    args[key] = value;
  }
  return args;
}

export async function readManifest() {
  const raw = await fs.readFile(MANIFEST_PATH, 'utf8');
  return JSON.parse(raw);
}

export function resolveAppConfig(manifest, appId) {
  const app = manifest.apps?.[appId];
  if (!app) throw new Error(`Unknown app "${appId}".`);
  return {
    ...app,
    id: appId,
    localesDirAbs: path.join(ROOT, app.localesDir),
    csvPathAbs: path.join(ROOT, app.csvPath),
  };
}

export async function ensureDir(dir) {
  await fs.mkdir(dir, { recursive: true });
}

export async function atomicWriteFile(targetPath, content, encoding = 'utf8') {
  const dir = path.dirname(targetPath);
  const base = path.basename(targetPath);
  const temp = path.join(
    dir,
    `.${base}.tmp-${process.pid}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
  );
  await ensureDir(dir);
  await fs.writeFile(temp, content, encoding);
  try {
    await fs.rename(temp, targetPath);
  } catch (error) {
    try {
      await fs.unlink(temp);
    } catch {
      // best-effort cleanup
    }
    throw error;
  }
}

export function tupleKey(app, namespace, key) {
  return `${app}|${namespace}|${key}`;
}

export function flattenObject(obj, parent = '') {
  const out = {};
  for (const [key, value] of Object.entries(obj ?? {})) {
    const full = parent ? `${parent}.${key}` : key;
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      Object.assign(out, flattenObject(value, full));
    } else {
      out[full] = String(value ?? '');
    }
  }
  return out;
}

export function unflattenObject(flat) {
  const out = {};
  for (const [key, value] of Object.entries(flat ?? {})) {
    const parts = key.split('.');
    let cursor = out;
    while (parts.length > 1) {
      const part = parts.shift();
      if (!cursor[part]) cursor[part] = {};
      cursor = cursor[part];
    }
    cursor[parts[0]] = value;
  }
  return out;
}

export function sortFlatEntries(flat) {
  return Object.entries(flat ?? {}).sort(([a], [b]) => a.localeCompare(b));
}

export function sortObjectDeep(value) {
  if (Array.isArray(value)) return value.map(sortObjectDeep);
  if (!value || typeof value !== 'object') return value;
  const out = {};
  for (const key of Object.keys(value).sort((a, b) => a.localeCompare(b))) {
    out[key] = sortObjectDeep(value[key]);
  }
  return out;
}

export function csvEscape(value) {
  return `"${String(value ?? '').replaceAll('"', '""')}"`;
}

export function csvUnescape(value) {
  const trimmed = String(value ?? '').trim();
  if (!trimmed.startsWith('"') || !trimmed.endsWith('"')) return trimmed;
  return trimmed.slice(1, -1).replaceAll('""', '"');
}

function splitCsvLine(line) {
  const out = [];
  let current = '';
  let inside = false;
  for (let i = 0; i < line.length; i += 1) {
    const ch = line[i];
    const next = line[i + 1];
    if (ch === '"' && next === '"' && inside) {
      current += '"';
      i += 1;
      continue;
    }
    if (ch === '"') {
      inside = !inside;
      continue;
    }
    if (ch === ',' && !inside) {
      out.push(current);
      current = '';
      continue;
    }
    current += ch;
  }
  out.push(current);
  return out;
}

export function parseCsv(content) {
  if (!String(content ?? '').trim()) return [];

  const records = [];
  let current = '';
  let insideQuotes = false;
  for (let i = 0; i < content.length; i += 1) {
    const ch = content[i];
    const next = content[i + 1];

    if (ch === '"' && next === '"' && insideQuotes) {
      current += '""';
      i += 1;
      continue;
    }
    if (ch === '"') {
      insideQuotes = !insideQuotes;
      current += ch;
      continue;
    }
    if ((ch === '\n' || ch === '\r') && !insideQuotes) {
      if (ch === '\r' && next === '\n') i += 1;
      if (current.trim().length > 0) records.push(current);
      current = '';
      continue;
    }
    current += ch;
  }
  if (current.trim().length > 0) records.push(current);
  if (records.length === 0) return [];

  const header = splitCsvLine(records[0]).map(csvUnescape);
  const rows = [];
  for (const record of records.slice(1)) {
    const cols = splitCsvLine(record);
    const row = {};
    header.forEach((name, index) => {
      row[name] = csvUnescape(cols[index] ?? '');
    });
    rows.push(row);
  }
  return rows;
}

export function indexRowsByTuple(rows) {
  const index = new Map();
  for (const row of rows) {
    if (!row.app || !row.namespace || !row.key) continue;
    index.set(tupleKey(row.app, row.namespace, row.key), row);
  }
  return index;
}

export async function writeReport(name, appId, report) {
  const outDir = path.join(ROOT, 'localization', 'out', 'reports');
  await ensureDir(outDir);
  const file = path.join(outDir, `${name}.${appId}.json`);
  await atomicWriteFile(file, `${JSON.stringify(report, null, 2)}\n`, 'utf8');
  return file;
}

export function createValidators(manifest, appConfig) {
  const keyRegex = new RegExp(manifest.keyPattern);
  const namespaceSet = new Set(appConfig.namespaces);
  const reservedPrefixes = manifest.reservedPrefixes ?? [];

  function validateNamespace(namespace) {
    return namespaceSet.has(namespace);
  }

  function validateKey(key) {
    if (!keyRegex.test(key)) return false;
    for (const prefix of reservedPrefixes) {
      if (key.startsWith(prefix)) return false;
    }
    return true;
  }

  return { validateNamespace, validateKey };
}

export async function readJsonFileIfExists(filePath) {
  try {
    const raw = await fs.readFile(filePath, 'utf8');
    try {
      return JSON.parse(raw);
    } catch (error) {
      throw new Error(formatJsonParseError(filePath, raw, error));
    }
  } catch (error) {
    if (error?.code === 'ENOENT') return null;
    throw error;
  }
}

export { ROOT };

function formatJsonParseError(filePath, raw, error) {
  const message = String(error?.message ?? 'Unknown JSON parse error');
  const posMatch = message.match(/position\s+(\d+)/i);
  if (!posMatch) {
    return `Invalid JSON in ${filePath}: ${message}`;
  }
  const position = Number(posMatch[1]);
  const safePos = Number.isFinite(position) && position >= 0 ? position : 0;
  const head = raw.slice(0, safePos);
  const line = head.split('\n').length;
  const col = safePos - (head.lastIndexOf('\n') + 1) + 1;
  const contextStart = Math.max(0, safePos - 40);
  const contextEnd = Math.min(raw.length, safePos + 40);
  const context = raw.slice(contextStart, contextEnd).replace(/\r?\n/g, '\\n');
  return `Invalid JSON in ${filePath}:${line}:${col} (position ${safePos}) - ${message}. Context: "${context}"`;
}
