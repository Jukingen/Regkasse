import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { describe, expect, it } from 'vitest';

const rootDir = path.dirname(fileURLToPath(import.meta.url));

describe('vitest.config.ts contract', () => {
  const source = readFileSync(path.join(rootDir, '../../../vitest.config.ts'), 'utf8');

  it('scopes unit tests to src and excludes Playwright e2e', () => {
    expect(source).toMatch(/include:\s*\[[\s\S]*src\/\*\*\/\*\.\{test,spec\}\.\{ts,tsx\}/);
    expect(source).toMatch(/tests\/e2e\/\*\*/);
    expect(source).toMatch(/setupFiles:\s*\[[\s\S]*vitest\.setup\.ts/);
    expect(source).toMatch(/['"]@['"]\s*:\s*path\.resolve/);
    expect(source).toMatch(/provider:\s*['"]v8['"]/);
  });
});
