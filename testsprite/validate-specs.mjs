#!/usr/bin/env node
/**
 * Validate testsprite YAML API specs against backend/swagger.json.
 * No live server required — safe for every PR.
 *
 * Usage (repo root):
 *   node testsprite/validate-specs.mjs
 *   npm run testsprite:validate
 */
import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');
const swaggerPath = join(root, 'backend', 'swagger.json');
const apiDir = join(__dirname, 'api');

if (!existsSync(swaggerPath)) {
  console.error(`Missing ${swaggerPath}`);
  process.exit(1);
}

const swagger = JSON.parse(readFileSync(swaggerPath, 'utf8'));
const swaggerPaths = swagger.paths ?? {};

/** @returns {{ file: string, method: string, path: string }[]} */
function extractEndpoints(ymlText, file) {
  const out = [];
  const re = /endpoint:\s*["']?(GET|POST|PUT|PATCH|DELETE)\s+(\/[^"'\s]+)["']?/gi;
  let m;
  while ((m = re.exec(ymlText)) !== null) {
    out.push({ file, method: m[1].toLowerCase(), path: m[2] });
  }
  return out;
}

/** Normalize OpenAPI path templates vs suite placeholders. */
function candidatesFor(path) {
  const list = new Set([path]);
  // {{var}} → {var}
  list.add(path.replace(/\{\{([^}]+)\}\}/g, '{$1}'));
  // {anything} → keep; also try common swagger param names
  list.add(path.replace(/\{\{[^}]+\}\}/g, '{id}'));
  list.add(path.replace(/\{[^}]+\}/g, '{id}'));
  return [...list];
}

function findSwaggerPath(path) {
  for (const c of candidatesFor(path)) {
    if (swaggerPaths[c]) return c;
  }
  // Fuzzy: same segment count, param slots align
  const want = path.replace(/\{\{[^}]+\}\}/g, '{}').replace(/\{[^}]+\}/g, '{}').split('/');
  for (const sp of Object.keys(swaggerPaths)) {
    const have = sp.replace(/\{[^}]+\}/g, '{}').split('/');
    if (want.length !== have.length) continue;
    let ok = true;
    for (let i = 0; i < want.length; i++) {
      if (want[i] === '{}' || have[i] === '{}') continue;
      if (want[i] !== have[i]) {
        ok = false;
        break;
      }
    }
    if (ok) return sp;
  }
  return null;
}

const files = readdirSync(apiDir).filter((f) => f.endsWith('.yml') || f.endsWith('.yaml'));
if (files.length === 0) {
  console.error(`No YAML specs in ${apiDir}`);
  process.exit(1);
}

const failures = [];
let checked = 0;

for (const file of files) {
  const text = readFileSync(join(apiDir, file), 'utf8');
  if (!/^name:\s*/m.test(text) || !/^tests:\s*/m.test(text)) {
    failures.push(`${file}: missing top-level name/tests`);
  }
  for (const ep of extractEndpoints(text, file)) {
    checked += 1;
    const matched = findSwaggerPath(ep.path);
    if (!matched) {
      failures.push(`${file}: path not in swagger.json → ${ep.method.toUpperCase()} ${ep.path}`);
      continue;
    }
    const ops = swaggerPaths[matched];
    if (!ops[ep.method]) {
      failures.push(
        `${file}: method ${ep.method.toUpperCase()} missing on ${matched} (suite: ${ep.path})`,
      );
    }
  }
}

if (failures.length) {
  console.error('TestSprite API spec validation failed:\n');
  for (const f of failures) console.error(`- ${f}`);
  console.error(`\nChecked ${checked} endpoint(s) across ${files.length} file(s).`);
  process.exit(1);
}

console.log(
  `OK: ${files.length} API suite(s), ${checked} endpoint(s) align with backend/swagger.json`,
);
