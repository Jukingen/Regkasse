import { spawn } from 'node:child_process';
import path from 'node:path';
import { parseArgs } from './utils.mjs';

const args = parseArgs(process.argv);
const app = args.project || args.app;
if (!app) {
  throw new Error('Usage: node tools/i18n/export-csv.mjs --project <frontend|frontend-admin> [--dry-run]');
}

const script = path.resolve(process.cwd(), 'localization', 'scripts', 'export-translations.mjs');
const cliArgs = [script, '--app', app];
if (args['dry-run'] === 'true') cliArgs.push('--dry-run');

await new Promise((resolve, reject) => {
  const child = spawn(process.execPath, cliArgs, { stdio: 'inherit' });
  child.on('close', (code) => {
    if (code === 0) resolve();
    else reject(new Error(`export-translations failed with exit code ${code}`));
  });
});
