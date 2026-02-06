module.exports = function (api) {
  api.cache(true);
  return {
    presets: ['babel-preset-expo'],
    plugins: [
      'react-native-reanimated/plugin',
      ['module-resolver', {
        root: ['./'],
        extensions: ['.ios.js', '.android.js', '.js', '.ts', '.tsx', '.json'],
        alias: {
          '@': './',
          'tests': ['./tests/'],
          '@components': './components',
          stream: 'stream-browserify',
          crypto: 'crypto-browserify',
          buffer: 'buffer',
          util: 'util',
          assert: 'assert',
          path: 'path-browserify',
          os: 'os-browserify/browser',
        },
      }],
      // Memory leak önleme
      '@babel/plugin-transform-runtime',
      '@babel/plugin-transform-optional-chaining',
      '@babel/plugin-transform-nullish-coalescing-operator',
      // FIX: import.meta transformation for web compatibility
      ['babel-plugin-transform-import-meta', { module: 'CommonJS' }],
    ],
    // Memory optimizasyonları
    compact: false,
    sourceMaps: false,
    retainLines: false,
    // Environment-specific overrides
    env: {
      production: {
        plugins: [
          // Remove console.logs in production
          ['transform-remove-console', { exclude: ['error', 'warn'] }],
        ],
      },
    },
  };
}; 