module.exports = function (api) {
  api.cache(true);
  return {
    presets: ['babel-preset-expo'],
    plugins: [
      ['module-resolver', {
        root: ['./'],
        extensions: ['.ios.js', '.android.js', '.js', '.ts', '.tsx', '.json'],
        alias: {
          '@': './',
          tests: ['./tests/'],
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

      // Prevent memory leaks
      '@babel/plugin-transform-runtime',
      '@babel/plugin-transform-optional-chaining',
      '@babel/plugin-transform-nullish-coalescing-operator',

      // FIX: import.meta transformation for web compatibility
      // ['babel-plugin-transform-import-meta', { module: 'CommonJS' }],

      // Must be last
      'react-native-reanimated/plugin',
    ],
    compact: false,
    sourceMaps: false,
    retainLines: false,
    env: {
      production: {
        plugins: [
          ['transform-remove-console', { exclude: ['error', 'warn'] }],
        ],
      },
    },
  };
};
