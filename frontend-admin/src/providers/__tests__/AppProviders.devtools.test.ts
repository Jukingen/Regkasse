import { readFileSync } from 'node:fs';
import path from 'node:path';

import { describe, expect, it } from 'vitest';

/**
 * Guard: React Query Devtools must not be a static import on the root provider
 * (keeps the package out of the production client graph).
 */
describe('AppProviders React Query Devtools loading', () => {
  it('does not statically import @tanstack/react-query-devtools', () => {
    const file = path.join(__dirname, '../AppProviders.tsx');
    const source = readFileSync(file, 'utf8');

    expect(source).not.toMatch(
      /import\s*\{[^}]*ReactQueryDevtools[^}]*\}\s*from\s*['"]@tanstack\/react-query-devtools['"]/
    );
    expect(source).not.toMatch(/from\s*['"]@tanstack\/react-query-devtools['"]/);
    expect(source).toMatch(/process\.env\.NODE_ENV\s*===\s*['"]development['"]/);
    expect(source).toMatch(/ReactQueryDevtoolsLazy/);
  });
});
