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

for (const file of walk(srcRoot)) {
  const code = fs.readFileSync(file, 'utf8');
  const usesMessage = /\bmessage\.(success|error|warning|info|loading|open)\b/.test(code);
  const usesModal = /\bmodal\.(confirm|success|warning|error|info)\b/.test(code);
  const usesNotification = /\bnotification\.(open|success|error|warning|info)\b/.test(code);
  if (!usesMessage && !usesModal && !usesNotification) continue;
  if (code.includes('useAntdApp()') || code.includes('App.useApp()')) continue;
  if (file.includes('antdAppBridge')) continue;
  console.log(path.relative(srcRoot, file));
}
