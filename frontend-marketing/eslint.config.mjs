/**
 * Flat ESLint config, same shape as the other Next.js packages.
 * The package lint script is `tsc --noEmit` (frontend-sites has no ESLint dependency).
 */
export default [
  {
    ignores: ['.next/**', 'node_modules/**'],
    rules: {
      'no-console': 'error',
    },
  },
];
