/**
 * Replace @/lib/antdApp imports with App.useApp() inside the primary export/hook.
 * Run: node frontend-admin/scripts/migrate-to-use-app-hook.mjs
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const srcRoot = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'src');

function walk(dir, out = []) {
  for (const name of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, name.name);
    if (name.isDirectory()) {
      if (name.name === 'node_modules') continue;
      walk(p, out);
    } else if (/\.(tsx?)$/.test(name.name)) out.push(p);
  }
  return out;
}

function usesStaticMessage(code) {
  return /\bmessage\.(success|error|warning|info|loading|open)\b/.test(code);
}

function usesStaticModal(code) {
  return /\bmodal\.(confirm|success|warning|error|info)\b/.test(code);
}

function usesStaticNotification(code) {
  return /\bnotification\.(open|success|error|warning|info)\b/.test(code);
}

const ENTRY_PATTERNS = [
  /export\s+default\s+function\s+\w+\s*\([^)]*\)\s*\{/,
  /export\s+function\s+use\w+\s*\([^)]*\)\s*\{/,
  /export\s+function\s+\w+\s*\([^)]*\)\s*\{/,
  /function\s+use\w+\s*\([^)]*\)\s*\{/,
  /export\s+const\s+\w+\s*=\s*\([^)]*\)\s*=>\s*\{/,
  /export\s+function\s+\w+\s*=\s*\([^)]*\)\s*=>\s*\{/,
];

function injectUseApp(code) {
  if (code.includes('App.useApp()')) return code;

  const parts = [];
  if (usesStaticMessage(code)) parts.push('message');
  if (usesStaticModal(code)) parts.push('modal');
  if (usesStaticNotification(code)) parts.push('notification');
  if (parts.length === 0) return code;

  const hookLine = `  const { ${parts.join(', ')} } = App.useApp();\n`;

  for (const pattern of ENTRY_PATTERNS) {
    const match = code.match(pattern);
    if (match) {
      const insertAt = match.index + match[0].length;
      return code.slice(0, insertAt) + '\n' + hookLine + code.slice(insertAt);
    }
  }

  return code;
}

function ensureAppInAntdImport(code) {
  const antdImport = code.match(/import\s+\{([^}]+)\}\s+from\s+['"]antd['"]\s*;?/);
  if (antdImport) {
    const inner = antdImport[1];
    if (/\bApp\b/.test(inner)) return code;
    return code.replace(
      antdImport[0],
      `import { App, ${inner.trim()} } from 'antd';`,
    );
  }

  const useClient = code.match(/^['"]use client['"];\s*\n/m);
  const appImport = "import { App } from 'antd';\n";
  if (useClient) {
    return code.replace(useClient[0], useClient[0] + appImport);
  }
  return appImport + code;
}

function transform(code, filePath) {
  if (!code.includes("@/lib/antdApp")) return null;

  const rel = path.relative(srcRoot, filePath).replace(/\\/g, '/');

  // Pure helper: only receives message.open as arg — drop unused import
  if (rel === 'shared/errors/openApiErrorMessage.tsx') {
    let next = code.replace(/import\s+\{[^}]*\}\s+from\s+['"]@\/lib\/antdApp['"]\s*;?\n?/g, '');
    return next === code ? null : next;
  }

  if (!usesStaticMessage(code) && !usesStaticModal(code) && !usesStaticNotification(code)) {
    let next = code.replace(/import\s+\{[^}]*\}\s+from\s+['"]@\/lib\/antdApp['"]\s*;?\n?/g, '');
    return next === code ? null : next;
  }

  let next = code.replace(/import\s+\{[^}]*\}\s+from\s+['"]@\/lib\/antdApp['"]\s*;?\n?/g, '');
  next = ensureAppInAntdImport(next);
  next = injectUseApp(next);
  return next === code ? null : next;
}

let changed = 0;
let skipped = [];

for (const file of walk(srcRoot)) {
  const code = fs.readFileSync(file, 'utf8');
  const out = transform(code, file);
  if (!out) continue;
  if (out.includes("@/lib/antdApp") && usesStaticMessage(out)) {
    skipped.push(path.relative(srcRoot, file));
  }
  fs.writeFileSync(file, out);
  changed += 1;
  console.log('updated', path.relative(srcRoot, file));
}

console.log(`Done. ${changed} file(s).`);
if (skipped.length) {
  console.log('May need manual App.useApp:', skipped.join(', '));
}
