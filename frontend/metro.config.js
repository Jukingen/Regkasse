const { getDefaultConfig } = require('expo/metro-config');

const config = getDefaultConfig(__dirname);

// Metro bundler performans ayarları
config.maxWorkers = 2; // CPU kullanımını azalt
config.resetCache = false; // Cache'i koru

// FIX: import.meta error - Web için ES module desteği
config.resolver = {
  ...config.resolver,
  sourceExts: [...(config.resolver?.sourceExts || []), 'mjs', 'cjs'],
};

// Bundle analizi için
config.transformer = {
  ...config.transformer,
  minifierConfig: {
    keep_fnames: true,
    mangle: {
      keep_fnames: true,
    },
  },
};

module.exports = config;