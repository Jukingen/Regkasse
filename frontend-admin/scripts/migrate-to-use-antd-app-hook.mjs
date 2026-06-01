/**
 * Replace App.useApp() with useAntdApp() from @/hooks/useAntdApp.
 * Run: node frontend-admin/scripts/migrate-to-use-antd-app-hook.mjs
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const srcRoot = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'src');
const HOOK_IMPORT = "import { useAntdApp } from '@/hooks/useAntdApp';\n";
const SKIP = new Set([
  path.join(srcRoot, 'hooks', 'useAntdApp.ts'),
  path.join(srcRoot, 'lib', 'AntdAppBridgeRegistrar.tsx'),
  path.join(srcRoot, 'lib', 'antdApp.ts'),
]);

function walk(dir, out = []) {
  for (const name of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, name.name);
    if (name.isDirectory()) walk(p, out);
    else if (/\.(tsx?)$/.test(name.name)) out.push(p);
  }
  return out;
}

function removeAppFromAntdImport(code) {
  return code.replace(/import\s+\{([^}]+)\}\s+from\s+['"]antd['"]\s*;?/g, (full, inner) => {
    const parts = inner
      .split(',')
      .map((p) => p.trim())
      .filter((p) => p && p !== 'App' && !/^type\s+App\b/.test(p));
    if (parts.length === 0) return '';
    return `import { ${parts.join(', ')} } from 'antd';`;
  });
}

function transform(code, filePath) {
  if (!code.includes('App.useApp()')) return null;
  if (SKIP.has(filePath)) return null;

  let next = code.replace(/App\.useApp\(\)/g, 'useAntdApp()');

  if (!next.includes("from '@/hooks/useAntdApp'")) {
    const useClient = next.match(/^['"]use client['"];\s*\n/m);
    if (useClient) {
      next = next.replace(useClient[0], useClient[0] + HOOK_IMPORT);
    } else {
      const firstImport = next.search(/^import\s/m);
      next =
        firstImport >= 0
          ? next.slice(0, firstImport) + HOOK_IMPORT + next.slice(firstImport)
          : HOOK_IMPORT + next;
    }
  }

  if (!/\bApp\b/.test(next.replace(/useAntdApp/g, ''))) {
    next = removeAppFromAntdImport(next);
  }

  next = next.replace(/\n{3,}/g, '\n\n');
  return next === code ? null : next;
}

let changed = 0;
for (const file of walk(srcRoot)) {
  const code = fs.readFileSync(file, 'utf8');
  const out = transform(code, file);
  if (out) {
    fs.writeFileSync(file, out);
    changed += 1;
    console.log('updated', path.relative(srcRoot, file));
  }
}
console.log(`Done. ${changed} file(s).`);
