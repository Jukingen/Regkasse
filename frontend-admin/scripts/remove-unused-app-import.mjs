import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const srcRoot = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'src');

function walk(dir, out = []) {
  for (const name of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, name.name);
    if (name.isDirectory()) walk(p, out);
    else if (/\.(tsx?)$/.test(name.name)) out.push(p);
  }
  return out;
}

function transform(code) {
  if (!code.includes("from '@/hooks/useAntdApp'")) return null;
  // Keep App when used as JSX or App.* API (not useAntdApp)
  if (/<App[\s>]/.test(code) || /\bApp\.[A-Z]/.test(code)) return null;

  let next = code.replace(/import\s+\{([^}]+)\}\s+from\s+['"]antd['"]\s*;?/g, (full, inner) => {
    const parts = inner
      .split(',')
      .map((p) => p.trim())
      .filter((p) => p && p !== 'App');
    if (parts.length === 0) return '';
    return `import { ${parts.join(', ')} } from 'antd';`;
  });

  next = next.replace(/\nimport \{ \} from 'antd';\n/g, '\n');
  return next === code ? null : next;
}

let n = 0;
for (const file of walk(srcRoot)) {
  const code = fs.readFileSync(file, 'utf8');
  const out = transform(code);
  if (out) {
    fs.writeFileSync(file, out);
    n += 1;
  }
}
console.log(`Removed unused App import from ${n} file(s).`);
