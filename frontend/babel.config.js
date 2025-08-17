module.exports = function (api) {
  api.cache(true);
  return {
    presets: [
      ['babel-preset-expo', {
        // Memory optimizasyonları
        useBuiltIns: 'usage',
        corejs: 3,
        targets: {
          node: 'current'
        }
      }]
    ],
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
      '@babel/plugin-transform-nullish-coalescing-operator'
    ],
    // Memory optimizasyonları
    compact: false,
    sourceMaps: false,
    retainLines: false
  };
}; 