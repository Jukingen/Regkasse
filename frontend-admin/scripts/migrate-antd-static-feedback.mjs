/**
 * One-off: route antd static message/modal calls through @/lib/antdApp (App context bridge).
 * Run from repo root: node frontend-admin/scripts/migrate-antd-static-feedback.mjs
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const srcRoot = path.join(__dirname, '..', 'src');

const SKIP = new Set([
  path.join(srcRoot, 'lib', 'antdAppBridge.ts'),
  path.join(srcRoot, 'lib', 'antdApp.ts'),
  path.join(srcRoot, 'lib', 'AntdAppBridgeRegistrar.tsx'),
  path.join(srcRoot, 'i18n', 'generated', 'translationKeys.ts'),
]);

function walk(dir, out = []) {
  for (const name of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, name.name);
    if (name.isDirectory()) {
      if (name.name === 'node_modules' || name.name === '__tests__') continue;
      walk(p, out);
    } else if (/\.(tsx?|jsx?)$/.test(name.name)) {
      out.push(p);
    }
  }
  return out;
}

function usesStaticFeedback(code) {
  return (
    /\bmessage\.(success|error|warning|info|loading|open)\b/.test(code) ||
    /\bModal\.(confirm|success|warning|error|info)\b/.test(code)
  );
}

function transform(code, filePath) {
  if (!usesStaticFeedback(code)) return null;
  if (SKIP.has(filePath)) return null;
  if (code.includes("from '@/lib/antdApp'") || code.includes('from "@/lib/antdApp"')) {
    let next = code;
    if (/\bModal\.(confirm|success|warning|error|info)\b/.test(next)) {
      next = next.replace(/\bModal\.(confirm|success|warning|error|info)\b/g, 'modal.$1');
    }
    return next === code ? null : next;
  }

  let next = code;
  const needsMessage = /\bmessage\.(success|error|warning|info|loading|open)\b/.test(next);
  const needsModal = /\bModal\.(confirm|success|warning|error|info)\b/.test(next);

  if (needsModal) {
    next = next.replace(/\bModal\.(confirm|success|warning|error|info)\b/g, 'modal.$1');
  }

  const antdImportRe = /import\s+(type\s+)?\{([^}]+)\}\s+from\s+['"]antd['"]\s*;?/g;
  let antdAppImport = '';
  if (needsMessage && needsModal)
    antdAppImport = "import { message, modal } from '@/lib/antdApp';\n";
  else if (needsMessage) antdAppImport = "import { message } from '@/lib/antdApp';\n";
  else if (needsModal) antdAppImport = "import { modal } from '@/lib/antdApp';\n";

  let insertedAntdApp = false;
  next = next.replace(antdImportRe, (full, typePrefix, inner) => {
    const isTypeOnly = Boolean(typePrefix);
    const parts = inner
      .split(',')
      .map((p) => p.trim())
      .filter(Boolean);
    const usesModalJsx = /<Modal[\s>]/.test(code);
    const kept = parts.filter((p) => {
      const name = p
        .replace(/^type\s+/, '')
        .split(/\s+as\s+/)[0]
        .trim();
      if (name === 'message') return false;
      if (name === 'Modal' && !usesModalJsx) return false;
      return true;
    });
    if (!isTypeOnly && (needsMessage || needsModal) && !insertedAntdApp) {
      insertedAntdApp = true;
    }
    if (kept.length === 0) {
      return antdAppImport && !isTypeOnly ? '' : '';
    }
    const prefix = typePrefix ?? '';
    const rebuilt = `import ${prefix}{ ${kept.join(', ')} } from 'antd';`;
    if (!isTypeOnly && insertedAntdApp && !code.includes("from '@/lib/antdApp'")) {
      return antdAppImport + rebuilt;
    }
    return rebuilt;
  });

  if ((needsMessage || needsModal) && !next.includes("from '@/lib/antdApp'")) {
    const firstImport = next.search(/^import\s/m);
    if (firstImport >= 0) {
      next = next.slice(0, firstImport) + antdAppImport + next.slice(firstImport);
    } else {
      next = antdAppImport + next;
    }
  }

  // Standalone: import { message } from 'antd'
  next = next.replace(
    /import\s+\{\s*message\s*\}\s+from\s+['"]antd['"]\s*;?\n?/g,
    "import { message } from '@/lib/antdApp';\n"
  );

  return next === code ? null : next;
}

const files = walk(srcRoot);
let changed = 0;
for (const file of files) {
  const code = fs.readFileSync(file, 'utf8');
  const out = transform(code, file);
  if (out) {
    fs.writeFileSync(file, out);
    changed += 1;
    console.log('updated', path.relative(srcRoot, file));
  }
}
console.log(`Done. ${changed} file(s) updated.`);
