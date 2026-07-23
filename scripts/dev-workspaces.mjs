// Parallel `npm run dev -w <pkg>` for long-running servers.
// Native `npm run dev --workspaces` is sequential and blocks on the first server.

import { spawn } from 'node:child_process';
import process from 'node:process';

const workspaces = [
  ['backend', '@regkasse/backend'],
  ['pos', 'cash-register'],
  ['admin', 'registrierkasse-admin'],
  ['sites', 'regkasse-sites'],
];

const children = [];

function shutdown(code = 0) {
  for (const child of children) {
    if (!child.killed) {
      child.kill('SIGTERM');
    }
  }
  process.exit(code);
}

process.on('SIGINT', () => shutdown(0));
process.on('SIGTERM', () => shutdown(0));

for (const [label, name] of workspaces) {
  const child = spawn(`npm run dev -w ${name}`, {
    stdio: 'inherit',
    shell: true,
    env: process.env,
  });
  child.on('exit', (code, signal) => {
    if (signal) {
      console.error(`[${label}] exited via ${signal}`);
      shutdown(1);
      return;
    }
    if (code && code !== 0) {
      console.error(`[${label}] exited with code ${code}`);
      shutdown(code);
    }
  });
  children.push(child);
}

console.log(
  'Starting workspace dev servers in parallel:',
  workspaces.map(([label]) => label).join(', '),
);
