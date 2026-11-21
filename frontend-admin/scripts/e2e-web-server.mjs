/**
 * Playwright web server for frontend-admin.
 *
 * Builds, then starts the production server. `next build` can exit 1 after a
 * finished compile because of the Next.js security notice; `next start` still
 * has to run. A shell `&&` would skip it. On Linux, `&` would background the
 * build, so this stays in Node.
 */
import { spawn } from 'node:child_process';

function npm(script) {
  return spawn('npm', ['run', script], {
    stdio: 'inherit',
    shell: true,
    env: process.env,
  });
}

let current = npm('build');

function stop() {
  if (current && !current.killed) current.kill();
}

process.on('SIGTERM', stop);
process.on('SIGINT', stop);

current.on('exit', () => {
  current = npm('start');
  current.on('exit', (code) => {
    process.exit(code ?? 0);
  });
});
