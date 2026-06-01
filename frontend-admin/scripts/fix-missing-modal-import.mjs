import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const srcRoot = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'src');

function walk(dir, out = []) {
  for (const name of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, name.name);
    if (name.isDirectory()) walk(p, out);
    else if (name.name.endsWith('.tsx')) out.push(p);
  }
  return out;
}

let fixed = 0;
for (const file of walk(srcRoot)) {
  const code = fs.readFileSync(file, 'utf8');
  if (!/<Modal[\s>]/.test(code)) continue;
  if (/import[^;]*\bModal\b[^;]*from\s+['"]antd['"]/.test(code)) continue;

  const antdImport = code.match(/import\s+\{([^}]+)\}\s+from\s+['"]antd['"]\s*;?/);
  if (antdImport) {
    const inner = antdImport[1].trim();
    const nextInner = inner.length ? `Modal, ${inner}` : 'Modal';
    const next = code.replace(antdImport[0], `import { ${nextInner} } from 'antd';`);
    fs.writeFileSync(file, next);
    fixed += 1;
    console.log('fixed', path.relative(srcRoot, file));
  } else {
    const firstImport = code.search(/^import\s/m);
    const insert = "import { Modal } from 'antd';\n";
    const next =
      firstImport >= 0
        ? code.slice(0, firstImport) + insert + code.slice(firstImport)
        : insert + code;
    fs.writeFileSync(file, next);
    fixed += 1;
    console.log('added import', path.relative(srcRoot, file));
  }
}
console.log(`Done. ${fixed} file(s) fixed.`);
