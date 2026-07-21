const { defineConfig, globalIgnores } = require('eslint/config');
const globals = require('globals');
const reactHooks = require('eslint-plugin-react-hooks');
const universeNative = require('eslint-config-universe/flat/native');
const typescriptAnalysis = require('eslint-config-universe/flat/shared/typescript-analysis');

/**
 * Flat ESLint config for the Expo / React Native POS app.
 * Extends eslint-config-universe (native + typed TypeScript analysis).
 *
 * Note: ESLint 10 breaks eslint-plugin-react (used by universe). Stay on ESLint 9.x
 * until the React plugin peer range includes ESLint 10.
 */
module.exports = defineConfig([
  globalIgnores([
    '**/node_modules/**',
    '**/.expo/**',
    '**/dist/**',
    '**/web-build/**',
    '**/android/**',
    '**/ios/**',
    '**/backup/**',
    '**/coverage/**',
    '**/*.tsbuildinfo',
  ]),

  universeNative,
  typescriptAnalysis,

  // Typed linting (universe/shared/typescript-analysis) needs a TS project service.
  {
    files: ['**/*.ts', '**/*.tsx', '**/*.d.ts'],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: __dirname,
      },
    },
  },

  // Node globals for tooling / config files
  {
    files: [
      'eslint.config.js',
      'babel.config.js',
      'metro.config.js',
      'jest.config.js',
      'jest.setup.ts',
      'prettier.config.*',
      'scripts/**/*.{js,cjs,mjs,ts}',
    ],
    languageOptions: {
      globals: globals.node,
    },
  },

  {
    plugins: {
      'react-hooks': reactHooks,
    },
    rules: {
      // Formatting is owned by Prettier CLI (see .prettierrc), not eslint-plugin-prettier.
      'prettier/prettier': 'off',

      // React Compiler / hooks v7 rules — warn until POS screens are migrated.
      // Keep classic rules-of-hooks / exhaustive-deps as configured by universe.
      'react-hooks/set-state-in-effect': 'warn',
      'react-hooks/refs': 'warn',
      'react-hooks/immutability': 'warn',
      'react-hooks/purity': 'warn',
      'react-hooks/preserve-manual-memoization': 'warn',
      'react-hooks/use-memo': 'warn',

      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['**/frontend-admin/**', '@/api/legacy/*'],
              message:
                'POS/mobile must stay on canonical payment lane (services/api/paymentService + /api/pos/payment). Do not import admin legacy modules.',
            },
          ],
        },
      ],
    },
  },
]);
