#!/usr/bin/env node
/**
 * Thin wrapper → localization/scripts/validate-translations.mjs
 * Prefer: npm run i18n:validate (root) or localization package scripts.
 *
 * Usage: node tools/i18n/validate.mjs --project <frontend|frontend-admin> [--strict true]
 */
import { spawn } from 'node:child_process';
import path from 'node:path';
import { parseArgs } from './utils.mjs';

const args = parseArgs(process.argv);
if (args.help === 'true' || args.h === 'true') {
  console.log('Usage: node tools/i18n/validate.mjs --project <frontend|frontend-admin> [--strict true]');
  console.log('Prefer: localization/scripts/validate-translations.mjs');
  process.exit(0);
}
const app = args.project || args.app;
if (!app) {
  throw new Error(
    'Usage: node tools/i18n/validate.mjs --project <frontend|frontend-admin> [--strict true]',
  );
}

const script = path.resolve(process.cwd(), 'localization', 'scripts', 'validate-translations.mjs');
const cliArgs = [script, '--app', app];
if (args.strict === 'true') {
  cliArgs.push('--strictMissing', 'true');
}

await new Promise((resolve, reject) => {
  const child = spawn(process.execPath, cliArgs, { stdio: 'inherit' });
  child.on('close', (code) => {
    if (code === 0) resolve();
    else reject(new Error(`validate-translations failed with exit code ${code}`));
  });
});
