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
    ],
  };
}; 