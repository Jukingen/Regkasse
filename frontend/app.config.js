/**
 * Dynamic Expo config. app.json stays the universal POS identity.
 *
 * EXPO_PUBLIC_BRAND is read only while Expo evaluates this file (EAS / prebuild).
 * Unset, "universal", and "default" return app.json unchanged.
 * No JS screen, API, or vertical-profile behavior reads this variable.
 *
 * Optional brand assets (used when the files exist, otherwise the universal art):
 *   assets/brands/gastronomy/icon.png
 *   assets/brands/gastronomy/adaptive-icon.png
 *   assets/brands/gastronomy/splash.png
 */
const fs = require('fs');
const path = require('path');

const appJson = require('./app.json');

const BRANDS = {
  gastronomy: {
    name: 'Regkasse Gastronomie',
    slug: 'regkasse-gastronomie',
    androidPackage: 'com.registrierkasse.gastronomy',
    iosBundleIdentifier: 'com.registrierkasse.gastronomy',
    icon: './assets/brands/gastronomy/icon.png',
    adaptiveIcon: './assets/brands/gastronomy/adaptive-icon.png',
    splash: './assets/brands/gastronomy/splash.png',
  },
};

function resolveAsset(relativePath, fallback) {
  if (!relativePath) return fallback;
  const absolute = path.join(__dirname, relativePath);
  return fs.existsSync(absolute) ? relativePath : fallback;
}

function splashImageFrom(config) {
  for (const plugin of config.plugins ?? []) {
    if (Array.isArray(plugin) && plugin[0] === 'expo-splash-screen') {
      return plugin[1]?.image;
    }
  }
  return undefined;
}

function withBrandAssets(config, brand) {
  const icon = resolveAsset(brand.icon, config.icon);
  const adaptiveIcon = resolveAsset(
    brand.adaptiveIcon,
    config.android?.adaptiveIcon?.foregroundImage ?? config.icon
  );
  const splashImage = resolveAsset(brand.splash, splashImageFrom(config) ?? config.icon);

  const plugins = (config.plugins ?? []).map((plugin) => {
    if (!Array.isArray(plugin)) return plugin;
    if (plugin[0] === 'expo-splash-screen') {
      const options = { ...plugin[1], image: splashImage };
      if (options.dark) {
        options.dark = { ...options.dark, image: splashImage };
      }
      return [plugin[0], options];
    }
    if (plugin[0] === 'expo-notifications') {
      return [plugin[0], { ...plugin[1], icon }];
    }
    return plugin;
  });

  return {
    ...config,
    name: brand.name,
    slug: brand.slug,
    icon,
    splash: {
      image: splashImage,
      resizeMode: 'contain',
      backgroundColor: config.backgroundColor ?? '#F5F5F5',
    },
    ios: {
      ...config.ios,
      bundleIdentifier: brand.iosBundleIdentifier,
    },
    android: {
      ...config.android,
      package: brand.androidPackage,
      adaptiveIcon: {
        ...(config.android?.adaptiveIcon ?? {}),
        foregroundImage: adaptiveIcon,
      },
    },
    plugins,
  };
}

function withEasProject(config, brandId) {
  const gastronomyId = (process.env.EAS_PROJECT_ID_GASTRONOMY ?? '').trim();
  const universalId = (process.env.EAS_PROJECT_ID ?? '').trim();
  const projectId = brandId === 'gastronomy' ? gastronomyId || universalId : universalId;
  if (!projectId) return config;
  return {
    ...config,
    extra: {
      ...(config.extra ?? {}),
      eas: {
        ...(config.extra?.eas ?? {}),
        projectId,
      },
    },
  };
}

/** @param {{ config?: import('expo/config').ExpoConfig }} ctx */
module.exports = ({ config } = {}) => {
  const base = config && config.slug ? config : appJson.expo;
  const brandId = (process.env.EXPO_PUBLIC_BRAND ?? '').trim().toLowerCase();
  if (!brandId || brandId === 'universal' || brandId === 'default') {
    return withEasProject(base, null);
  }
  const brand = BRANDS[brandId];
  if (!brand) {
    throw new Error(
      `Unknown EXPO_PUBLIC_BRAND "${brandId}". Supported: ${Object.keys(BRANDS).join(', ')}. Leave it unset for the universal POS binary.`
    );
  }
  return withEasProject(withBrandAssets(base, brand), brandId);
};
