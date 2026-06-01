/**
 * One-off codemod: antd 6 deprecations in frontend-admin/src.
 * - Space direction -> orientation
 * - Modal maskClosable -> mask.closable
 * - Alert message -> title (multiline + inline)
 * - Statistic valueStyle -> styles.content
 * - Spin tip -> description
 * - Modal/Drawer destroyOnClose -> destroyOnHidden
 * - Dropdown dropdownRender -> popupRender
 * - Dropdown overlayClassName -> classNames.root
 * - Drawer width -> size (presets 256/300/400 -> "default", 512 -> "large", else numeric size)
 */
import fs from 'node:fs';
import path from 'node:path';

const ROOT = path.resolve(import.meta.dirname, '../src');

function walk(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(p, out);
    else if (p.endsWith('.tsx')) out.push(p);
  }
  return out;
}

function migrateAlertMultiline(content) {
  const eol = content.includes('\r\n') ? '\r\n' : '\n';
  const lines = content.split(/\r?\n/);
  let inAlert = false;
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (/<Alert\b/.test(line)) inAlert = true;
    if (inAlert && /^\s+message=/.test(line)) {
      lines[i] = line.replace(/^(\s+)message=/, '$1title=');
    }
    if (inAlert && (/<\/Alert>/.test(line) || /<Alert[^>]*\/>\s*$/.test(line.trim()))) {
      inAlert = false;
    }
  }
  return lines.join(eol);
}

function migrateStatisticValueStyle(content) {
  let next = content.replace(
    /valueStyle=\{\{([^{}]*)\}\}/g,
    'styles={{ content: { $1 } }}',
  );
  next = next.replace(
    /valueStyle=\{([^{][^}\n]*)\}/g,
    'styles={{ content: $1 }}',
  );
  return next;
}

function migrateSpinTip(content) {
  const eol = content.includes('\r\n') ? '\r\n' : '\n';
  const lines = content.split(/\r?\n/);
  for (let i = 0; i < lines.length; i++) {
    if (!/<Spin\b/.test(lines[i])) continue;
    lines[i] = lines[i]
      .replace(/\btip=/g, 'description=')
      .replace(/\btip\s*=/g, 'description=');
  }
  return lines.join(eol);
}

function migrateOverlayClassName(content) {
  return content.replace(
    /overlayClassName="([^"]+)"/g,
    'classNames={{ root: "$1" }}',
  );
}

function migrateDrawerWidth(content) {
  const presetDefault = new Set([256, 300, 400]);
  const eol = content.includes('\r\n') ? '\r\n' : '\n';
  const lines = content.split(/\r?\n/);
  let inDrawer = false;
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (/<Drawer\b/.test(line)) inDrawer = true;
    if (inDrawer && /\bwidth=\{(\d+)\}/.test(line)) {
      const width = Number(line.match(/\bwidth=\{(\d+)\}/)[1]);
      let replacement;
      if (presetDefault.has(width)) replacement = 'size="default"';
      else if (width === 512) replacement = 'size="large"';
      else replacement = `size={${width}}`;
      lines[i] = line.replace(/\bwidth=\{\d+\}/, replacement);
    }
    if (inDrawer && />/.test(line)) inDrawer = false;
  }
  return lines.join(eol);
}

function migrateFile(content) {
  let next = content;
  next = next.replace(/direction="vertical"/g, 'orientation="vertical"');
  next = next.replace(/direction="horizontal"/g, 'orientation="horizontal"');
  next = next.replace(/maskClosable=\{([^}]+)\}/g, 'mask={{ closable: $1 }}');
  next = next.replace(/<Alert([^>\n]*)\smessage=/g, '<Alert$1 title=');
  next = migrateAlertMultiline(next);
  next = migrateStatisticValueStyle(next);
  next = migrateSpinTip(next);
  next = next.replace(/\bdestroyOnClose\b/g, 'destroyOnHidden');
  next = next.replace(/\bdropdownRender\b/g, 'popupRender');
  next = migrateOverlayClassName(next);
  next = migrateDrawerWidth(next);
  return next;
}

let changed = 0;
for (const file of walk(ROOT)) {
  const original = fs.readFileSync(file, 'utf8');
  const updated = migrateFile(original);
  if (updated !== original) {
    fs.writeFileSync(file, updated, 'utf8');
    changed++;
  }
}

console.log(`Updated ${changed} files under ${ROOT}`);
