import fs from 'node:fs/promises';
import path from 'node:path';
import { DEFAULT_LOCALE, FORBIDDEN_T_PATTERNS, PROJECTS, SUPPORTED_LOCALES } from './projects.mjs';
import { flattenObject, listNamespaceFiles, parseArgs } from './utils.mjs';

const args = parseArgs(process.argv);
const targetProjects = args.project ? [PROJECTS[args.project]] : Object.values(PROJECTS);
const strictParity = args.strict === 'true';
if (targetProjects.includes(undefined)) {
  throw new Error(`Unknown project "${args.project}". Use --project frontend or --project frontend-admin`);
}

const failures = [];
const warnings = [];

for (const project of targetProjects) {
  const byLocale = await listNamespaceFiles(project);
  const defaultFiles = byLocale[DEFAULT_LOCALE];

  for (const namespaceFile of defaultFiles) {
    const defaultPath = path.join(project.localesDir, DEFAULT_LOCALE, namespaceFile);
    const defaultJson = JSON.parse(await fs.readFile(defaultPath, 'utf8'));
    const defaultKeys = Object.keys(flattenObject(defaultJson)).sort();

    for (const locale of SUPPORTED_LOCALES) {
      const targetPath = path.join(project.localesDir, locale, namespaceFile);
      let targetJson = {};
      try {
        targetJson = JSON.parse(await fs.readFile(targetPath, 'utf8'));
      } catch {
        failures.push(`${project.id}: missing locale file ${locale}/${namespaceFile}`);
        continue;
      }
      const targetKeys = Object.keys(flattenObject(targetJson)).sort();
      const missing = defaultKeys.filter((k) => !targetKeys.includes(k));
      if (missing.length) {
        const message = `${project.id}: ${locale}/${namespaceFile} missing keys -> ${missing.slice(0, 5).join(', ')}`;
        if (strictParity) failures.push(message);
        else warnings.push(message);
      }
    }
  }

  for (const sourceRoot of project.sourceGlobs) {
    await walkFiles(sourceRoot, async (filePath) => {
      if (!filePath.endsWith('.ts') && !filePath.endsWith('.tsx')) return;
      const content = await fs.readFile(filePath, 'utf8');
      for (const rule of FORBIDDEN_T_PATTERNS) {
        if (rule.test(content)) {
          failures.push(`${project.id}: forbidden translation usage in ${filePath} with ${rule}`);
        }
      }
    });
  }
}

if (failures.length > 0) {
  console.error('i18n validation failed:');
  for (const failure of failures) {
    console.error(`- ${failure}`);
  }
  process.exit(1);
}

if (warnings.length > 0) {
  console.warn('i18n validation warnings:');
  for (const warning of warnings) {
    console.warn(`- ${warning}`);
  }
}

console.log('i18n validation passed');

async function walkFiles(dir, onFile) {
  const items = await fs.readdir(dir, { withFileTypes: true });
  for (const item of items) {
    const full = path.join(dir, item.name);
    if (item.isDirectory()) {
      if (item.name === 'node_modules' || item.name === '.next' || item.name === '.expo') continue;
      await walkFiles(full, onFile);
      continue;
    }
    await onFile(full);
  }
}
