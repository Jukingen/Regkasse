import eslint from '@eslint/js'
import tseslintPlugin from '@typescript-eslint/eslint-plugin'
import tseslintParser from '@typescript-eslint/parser'
import reactPlugin from 'eslint-plugin-react'
import reactHooksPlugin from 'eslint-plugin-react-hooks'

export default [
  eslint.configs.recommended,
  {
    files: ['**/*.{js,jsx,ts,tsx}'],
    plugins: {
      '@typescript-eslint': tseslintPlugin,
      react: reactPlugin,
      'react-hooks': reactHooksPlugin,
    },
    languageOptions: {
      parser: tseslintParser,
      parserOptions: {
        ecmaVersion: 'latest',
        sourceType: 'module',
        ecmaFeatures: {
          jsx: true,
        },
      },
    },
    settings: {
      react: {
        version: 'detect',
      },
    },
    rules: {
      ...tseslintPlugin.configs.recommended.rules,
      // Orval-generated model dosyalarında type/const aynı isimle üretilebiliyor.
      // Bu durumda core 'no-redeclare' hatası disable comment ile kapatılamıyor.
      'no-redeclare': 'off',
      'react/react-in-jsx-scope': 'off',
      'react-hooks/rules-of-hooks': 'error',
      'react-hooks/exhaustive-deps': 'warn',
    },
  },
  {
    files: [
      'src/app/(protected)/rksv/**/*.{ts,tsx}',
      'src/features/invoices/**/*.{ts,tsx}',
      'src/shared/rksvAdminTruth.ts',
      'src/shared/investigationNavigation.ts',
      'src/shared/foReconciliationRowTriage.ts',
      'src/shared/contract/**/*.{ts,tsx}',
    ],
    rules: {
      // Release-quality gate: new explicit-any on truth surfaces should be reviewed (fix or justify).
      '@typescript-eslint/no-explicit-any': 'warn',
    },
  },
]
