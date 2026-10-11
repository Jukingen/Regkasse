/**
 * Orval tags-split templates emit indented blank lines, and `output.prettier`
 * does not remove them. This step strips trailing whitespace from
 * `src/api/generated` so the client stays `git diff --check` clean.
 * Orval owns these files; do not edit them by hand.
 */
import { existsSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const generatedRoot = join(dirname(fileURLToPath(import.meta.url)), '..', 'src', 'api', 'generated');

function walk(dir, files) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const fullPath = join(dir, entry.name);
    if (entry.isDirectory()) {
      walk(fullPath, files);
      continue;
    }
    if (entry.isFile() && /\.(ts|tsx)$/.test(entry.name)) {
      files.push(fullPath);
    }
  }
}

function stripFile(file) {
  const text = readFileSync(file, 'utf8');
  const newline = text.includes('\r\n') ? '\r\n' : '\n';
  const endsWithNewline = text.endsWith('\n');
  const lines = text.split(/\r?\n/);
  if (endsWithNewline) {
    lines.pop();
  }

  let changed = false;
  const cleaned = lines.map((line) => {
    const next = line.replace(/[ \t]+$/u, '');
    if (next !== line) {
      changed = true;
    }
    return next;
  });

  if (!changed) {
    return false;
  }

  const body = cleaned.join(newline) + (endsWithNewline ? newline : '');
  writeFileSync(file, body);
  return true;
}

function main() {
  if (!existsSync(generatedRoot)) {
    console.error(`strip-generated-trailing-whitespace: missing ${generatedRoot}`);
    process.exit(1);
  }

  const files = [];
  walk(generatedRoot, files);
  let cleaned = 0;
  for (const file of files) {
    if (stripFile(file)) {
      cleaned += 1;
    }
  }

  console.log(
    `strip-generated-trailing-whitespace: cleaned ${cleaned} of ${files.length} files`,
  );
}

main();
