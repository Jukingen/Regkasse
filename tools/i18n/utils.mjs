import fs from 'node:fs/promises';
import path from 'node:path';
import { SUPPORTED_LOCALES } from './projects.mjs';

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

export async function ensureDir(dir) {
  await fs.mkdir(dir, { recursive: true });
}

export function flattenObject(obj, parentKey = '') {
  const out = {};
  for (const [key, value] of Object.entries(obj ?? {})) {
    const full = parentKey ? `${parentKey}.${key}` : key;
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      Object.assign(out, flattenObject(value, full));
    } else {
      out[full] = String(value ?? '');
    }
  }
  return out;
}

export function unflattenObject(flat) {
  const result = {};
  for (const [dotKey, value] of Object.entries(flat)) {
    const segments = dotKey.split('.');
    let ref = result;
    while (segments.length > 1) {
      const segment = segments.shift();
      if (!ref[segment]) ref[segment] = {};
      ref = ref[segment];
    }
    ref[segments[0]] = value;
  }
  return result;
}

export async function listNamespaceFiles(project) {
  const byLocale = {};
  for (const locale of SUPPORTED_LOCALES) {
    const localeDir = path.join(project.localesDir, locale);
    try {
      const names = await fs.readdir(localeDir);
      byLocale[locale] = names.filter((name) => name.endsWith('.json')).sort();
    } catch {
      byLocale[locale] = [];
    }
  }
  return byLocale;
}

export function csvEscape(value) {
  const stringValue = String(value ?? '');
  return `"${stringValue.replaceAll('"', '""')}"`;
}

export function csvUnescape(value) {
  const trimmed = value.trim();
  if (!trimmed.startsWith('"') || !trimmed.endsWith('"')) return trimmed;
  return trimmed.slice(1, -1).replaceAll('""', '"');
}

export function parseCsv(content) {
  const rows = [];
  const lines = content.split(/\r?\n/).filter(Boolean);
  if (lines.length === 0) return rows;
  const header = splitCsvLine(lines[0]);
  for (const line of lines.slice(1)) {
    const values = splitCsvLine(line);
    const row = {};
    header.forEach((h, idx) => {
      row[h] = csvUnescape(values[idx] ?? '');
    });
    rows.push(row);
  }
  return rows;
}

function splitCsvLine(line) {
  const out = [];
  let current = '';
  let inside = false;
  for (let i = 0; i < line.length; i += 1) {
    const char = line[i];
    const next = line[i + 1];
    if (char === '"' && next === '"' && inside) {
      current += '"';
      i += 1;
      continue;
    }
    if (char === '"') {
      inside = !inside;
      continue;
    }
    if (char === ',' && !inside) {
      out.push(current);
      current = '';
      continue;
    }
    current += char;
  }
  out.push(current);
  return out;
}
