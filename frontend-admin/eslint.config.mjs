import nextVitals from 'eslint-config-next/core-web-vitals';
import nextTs from 'eslint-config-next/typescript';
import prettier from 'eslint-config-prettier/flat';
import importPlugin from 'eslint-plugin-import';
import reactHooks from 'eslint-plugin-react-hooks';
import { defineConfig, globalIgnores } from 'eslint/config';

/**
 * Next.js 16 flat config (replaces legacy `.eslintrc.json` + removed `next lint`).
 *
 * Base: `eslint-config-next/core-web-vitals` + `eslint-config-next/typescript`
 * TypeScript rules: `@typescript-eslint/*` via pinned
 * `@typescript-eslint/eslint-plugin` + `@typescript-eslint/parser` (also pulled by
 * `eslint-config-next` → `typescript-eslint`).
 * Import order: Prettier `@trivago/prettier-plugin-sort-imports` (see prettier.config.mjs).
 * `eslint-config-prettier` last so formatting rules do not fight Prettier.
 */
export default defineConfig([
  ...nextVitals,
  ...nextTs,
  {
    // Flat-config rule overrides that reference a plugin must declare that plugin here.
    plugins: {
      import: importPlugin,
      'react-hooks': reactHooks,
    },
    languageOptions: {
      // Keep parser aligned with the pinned @typescript-eslint/parser (Next TS preset
      // already sets this; explicit pin avoids drift if a transitive older parser appears).
      parserOptions: {
        ecmaVersion: 'latest',
        sourceType: 'module',
      },
    },
    settings: {
      'import/resolver': {
        typescript: true,
        node: true,
      },
    },
    rules: {
      // Orval-generated models can share type/const names; core no-redeclare cannot be disabled per line.
      'no-redeclare': 'off',

      // Prefer `@/lib/logger` / `technicalConsole` over raw console in app code.
      'no-console': 'error',

      // TypeScript
      '@typescript-eslint/no-unused-vars': [
        'warn',
        {
          argsIgnorePattern: '^_',
          varsIgnorePattern: '^_',
          caughtErrorsIgnorePattern: '^_',
        },
      ],
      '@typescript-eslint/no-explicit-any': 'warn',
      '@typescript-eslint/no-empty-object-type': 'warn',
      '@typescript-eslint/prefer-as-const': 'warn',

      // React Hooks (core correctness + gradual React Compiler rules)
      'react-hooks/rules-of-hooks': 'error',
      'react-hooks/exhaustive-deps': 'warn',
      'react-hooks/set-state-in-effect': 'warn',
      'react-hooks/preserve-manual-memoization': 'warn',
      'react-hooks/refs': 'warn',
      'react-hooks/immutability': 'warn',
      'react-hooks/purity': 'warn',

      // Import hygiene (order owned by Prettier trivago plugin)
      'import/first': 'error',
      'import/no-duplicates': 'warn',
      'import/newline-after-import': 'warn',

      /**
       * Ant Design 6 deprecated JSX props (component-scoped).
       * There is no built-in `no-restricted-props`; use AST selectors.
       * Do not ban Modal `width` or Collapse `bordered` — those remain valid.
       * Guide: docs/ANT_DESIGN_6_MIGRATION.md
       */
      'no-restricted-syntax': [
        'error',
        {
          selector:
            "JSXOpeningElement[name.name='Drawer'] > JSXAttribute[name.name='width']",
          message:
            "Ant Design 6: Drawer `width` is deprecated. Use `size` instead (see docs/ANT_DESIGN_6_MIGRATION.md).",
        },
        {
          selector:
            "JSXOpeningElement[name.name='Drawer'] > JSXAttribute[name.name='height']",
          message:
            "Ant Design 6: Drawer `height` is deprecated. Use `size` instead (see docs/ANT_DESIGN_6_MIGRATION.md).",
        },
        {
          selector:
            "JSXOpeningElement[name.name='Alert'] > JSXAttribute[name.name='message']",
          message:
            "Ant Design 6: Alert `message` is deprecated. Use `title` instead (see docs/ANT_DESIGN_6_MIGRATION.md).",
        },
        {
          selector:
            "JSXOpeningElement[name.name='Card'] > JSXAttribute[name.name='bordered']",
          message:
            'Ant Design 6: Card `bordered` is deprecated. Use variant="borderless" or variant="outlined" (see docs/ANT_DESIGN_6_MIGRATION.md).',
        },
        {
          selector:
            "JSXOpeningElement[name.name='Tag'] > JSXAttribute[name.name='bordered']",
          message:
            'Ant Design 6: Tag `bordered` is deprecated. Prefer variant="filled" / variant (see docs/ANT_DESIGN_6_MIGRATION.md).',
        },
        {
          selector: "JSXAttribute[name.name='destroyOnClose']",
          message:
            "Ant Design 6: `destroyOnClose` is deprecated on Modal/Drawer/Tabs. Use `destroyOnHidden` instead (see docs/ANT_DESIGN_6_MIGRATION.md).",
        },
        {
          selector: "JSXAttribute[name.name='dropdownRender']",
          message:
            "Ant Design 6: `dropdownRender` is deprecated on Dropdown/Select. Use `popupRender` instead (see docs/ANT_DESIGN_6_MIGRATION.md).",
        },
        {
          selector:
            "JSXOpeningElement[name.name='Space'] > JSXAttribute[name.name='direction']",
          message:
            "Ant Design 6: Space `direction` is deprecated. Use `orientation` instead (see docs/ANT_DESIGN_6_MIGRATION.md).",
        },
        {
          selector:
            "JSXOpeningElement[name.object.name='Space'][name.property.name='Compact'] > JSXAttribute[name.name='direction']",
          message:
            "Ant Design 6: Space.Compact `direction` is deprecated. Use `orientation` instead (see docs/ANT_DESIGN_6_MIGRATION.md).",
        },
        {
          selector:
            "JSXOpeningElement[name.name=/^(Modal|Drawer)$/] > JSXAttribute[name.name='maskClosable']",
          message:
            "Ant Design 6: `maskClosable` is deprecated. Use mask={{ closable: … }} instead (see docs/ANT_DESIGN_6_MIGRATION.md).",
        },
      ],
    },
  },
  {
    files: [
      'src/shared/dev/technicalConsole.ts',
      'scripts/**/*.{js,mjs,cjs,ts}',
      '**/*.{test,spec}.{ts,tsx}',
      '**/__tests__/**/*.{ts,tsx}',
    ],
    rules: {
      'no-console': 'off',
    },
  },
  prettier,
  globalIgnores([
    '.next/**',
    'out/**',
    'build/**',
    'coverage/**',
    'playwright-report/**',
    'test-results/**',
    'next-env.d.ts',
    'src/api/generated/**',
    'src/i18n/generated/**',
    'public/**',
  ]),
]);
