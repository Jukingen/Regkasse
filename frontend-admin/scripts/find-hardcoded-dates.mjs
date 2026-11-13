/**
 * Find hardcoded date display patterns in frontend-admin that should use `@/lib/dateUtils`.
 *
 * Keep `YYYY-MM-DD` / `formatIsoDate` for API params and export filenames.
 *
 * Usage (repo root):
 *   node frontend-admin/scripts/find-hardcoded-dates.mjs
 *   node frontend-admin/scripts/find-hardcoded-dates.mjs --json
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SRC_ROOT = path.resolve(__dirname, '../src');

const PATTERNS = [
  {
    id: 'toLocaleDateString',
    re: /\.toLocaleDateString\s*\(/g,
    note: 'Prefer formatDate from @/lib/dateUtils',
  },
  {
    id: 'toLocaleString-date',
    re: /new Date\([^)]*\)\.toLocaleString\s*\(/g,
    note: 'Prefer formatDateTime / formatDateTimeSeconds',
  },
  {
    id: 'toLocaleTimeString',
    re: /\.toLocaleTimeString\s*\(/g,
    note: 'Prefer formatDateTimeSeconds or a dedicated time helper',
  },
  {
    id: 'dayjs-german-date',
    re: /\.format\(\s*['"]DD\.MM\.YYYY['"]\s*\)/g,
    note: 'Prefer formatDate(...)',
  },
  {
    id: 'dayjs-german-datetime',
    re: /\.format\(\s*['"]DD\.MM\.YYYY HH:mm['"]\s*\)/g,
    note: 'Prefer formatDateTime(...)',
  },
  {
    id: 'dayjs-german-datetime-seconds',
    re: /\.format\(\s*['"]DD\.MM\.YYYY HH:mm:ss['"]\s*\)/g,
    note: 'Prefer formatDateTimeSeconds(...) or formatUtcDateTime(...)',
  },
  {
    id: 'iso-split-T',
    re: /\.toISOString\(\)\s*\.\s*split\(\s*['"]T['"]\s*\)/g,
    note: 'Prefer formatIsoDate(...) for calendar dates',
  },
  {
    id: 'moment',
    re: /\bmoment\s*\(/g,
    note: 'moment is not used — migrate to dayjs / dateUtils',
  },
];

/** Paths that intentionally keep raw patterns (tests, formatters themselves). */
const IGNORE_PATH_PARTS = [
  `${path.sep}lib${path.sep}dateUtils.ts`,
  `${path.sep}lib${path.sep}dateFormatter.ts`,
  `${path.sep}lib${path.sep}hooks${path.sep}useDateFormatter.ts`,
  `${path.sep}i18n${path.sep}formatting.ts`,
  `${path.sep}__tests__${path.sep}`,
  `${path.sep}__mocks__${path.sep}`,
];

function shouldIgnore(filePath) {
  return IGNORE_PATH_PARTS.some((part) => filePath.includes(part));
}

function walk(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === 'node_modules' || entry.name === '.next') continue;
      walk(full, out);
    } else if (/\.(tsx?|jsx?)$/.test(entry.name)) {
      out.push(full);
    }
  }
  return out;
}

function main() {
  const asJson = process.argv.includes('--json');
  const files = walk(SRC_ROOT);
  /** @type {{ id: string; file: string; line: number; text: string; note: string }[]} */
  const hits = [];

  for (const file of files) {
    if (shouldIgnore(file)) continue;
    const content = fs.readFileSync(file, 'utf8');
    const lines = content.split(/\r?\n/);
    const rel = path.relative(path.resolve(__dirname, '../..'), file).replace(/\\/g, '/');

    for (const pattern of PATTERNS) {
      for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        pattern.re.lastIndex = 0;
        if (pattern.re.test(line)) {
          hits.push({
            id: pattern.id,
            file: rel,
            line: i + 1,
            text: line.trim(),
            note: pattern.note,
          });
        }
      }
    }
  }

  if (asJson) {
    process.stdout.write(`${JSON.stringify({ count: hits.length, hits }, null, 2)}\n`);
    return;
  }

  if (hits.length === 0) {
    console.log('No hardcoded date display patterns found.');
    return;
  }

  console.log(`Found ${hits.length} hardcoded date pattern(s):\n`);
  let currentId = '';
  for (const hit of hits) {
    if (hit.id !== currentId) {
      currentId = hit.id;
      console.log(`\n## ${hit.id}`);
      console.log(`   ${hit.note}\n`);
    }
    console.log(`${hit.file}:${hit.line}`);
    console.log(`  ${hit.text}`);
  }
}

main();
