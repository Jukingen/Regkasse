import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const manifestPath = path.join(root, 'localization', 'namespace-manifest.json');
const manifest = JSON.parse(await fs.readFile(manifestPath, 'utf8'));

const appEntries = Object.entries(manifest.apps ?? {});

export const PROJECTS = Object.fromEntries(
  appEntries.map(([id, app]) => [
    id,
    {
      id,
      localesDir: path.join(root, app.localesDir),
      outputCsv: path.join(root, app.csvPath),
      namespaces: [...(app.namespaces ?? [])].sort((a, b) => a.localeCompare(b)),
    },
  ]),
);

export const SUPPORTED_LOCALES = [...(manifest.locales ?? ['de', 'en', 'tr'])];
export const DEFAULT_LOCALE = manifest.defaultLocale ?? 'de';
export const RESERVED_PREFIXES = [...(manifest.reservedPrefixes ?? ['product.', 'category.', 'modifier.'])];
