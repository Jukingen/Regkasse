/**
 * Expo SDK 56 Babel config.
 *
 * `babel-preset-expo` already enables:
 * - `@babel/plugin-transform-runtime` (helpers / regenerator)
 * - optional chaining & nullish coalescing (Hermes / web presets)
 * - `react-native-worklets` / `react-native-reanimated` plugin when installed
 *
 * Do not re-list those plugins here — duplicates break transform order
 * (Reanimated/worklets must run once, and Expo injects them inside the preset).
 */
module.exports = function (api) {
  // Bust Babel cache when NODE_ENV changes (test vs development plugins differ).
  api.cache.using(() => process.env.NODE_ENV || process.env.BABEL_ENV || 'development');

  return {
    presets: ['babel-preset-expo'],
    plugins: [
      // Path aliases (align with tsconfig paths / Jest)
      [
        'module-resolver',
        {
          root: ['./'],
          extensions: ['.ios.js', '.android.js', '.js', '.ts', '.tsx', '.json'],
          alias: {
            '@': './',
            '@/shared': './shared',
            shared: './shared',
            tests: ['./tests/'],
            '@components': './components',
            // Used by PaymentSuccessQr / paymentService for base64 data URLs
            buffer: 'buffer',
          },
        },
      ],
    ],
    env: {
      // Convert dynamic import() to require() so Jest (CJS) does not need --experimental-vm-modules
      test: {
        plugins: ['babel-plugin-dynamic-import-node'],
      },
      production: {
        plugins: [['transform-remove-console', { exclude: ['error', 'warn'] }]],
      },
    },
  };
};
