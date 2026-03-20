module.exports = {
  extends: ['universe/native', 'universe/shared/typescript-analysis'],
  overrides: [
    {
      files: ['*.ts', '*.tsx', '*.d.ts'],
      parserOptions: {
        project: './tsconfig.json',
      },
    },
  ],
  rules: {
    'prettier/prettier': 'off',
    '@typescript-eslint/no-unused-vars': 'warn',
    'react-hooks/exhaustive-deps': 'warn',
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
}; 