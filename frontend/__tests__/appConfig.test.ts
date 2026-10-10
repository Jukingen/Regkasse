/**
 * @jest-environment node
 *
 * app.config.js is build identity only. The universal path must match app.json.
 */
import { afterEach, describe, expect, it } from '@jest/globals';

import appJson from '../app.json';

const loadConfig = require('../app.config.js') as (ctx?: {
  config?: typeof appJson.expo;
}) => typeof appJson.expo & {
  splash?: { image?: string };
  extra?: { eas?: { projectId?: string } };
};

const ORIGINAL_BRAND = process.env.EXPO_PUBLIC_BRAND;
const ORIGINAL_PROJECT = process.env.EAS_PROJECT_ID;
const ORIGINAL_GASTRONOMY_PROJECT = process.env.EAS_PROJECT_ID_GASTRONOMY;

function withEnv(brand: string | undefined, evaluate: () => void): void {
  if (brand == null) delete process.env.EXPO_PUBLIC_BRAND;
  else process.env.EXPO_PUBLIC_BRAND = brand;
  evaluate();
}

afterEach(() => {
  if (ORIGINAL_BRAND == null) delete process.env.EXPO_PUBLIC_BRAND;
  else process.env.EXPO_PUBLIC_BRAND = ORIGINAL_BRAND;
  if (ORIGINAL_PROJECT == null) delete process.env.EAS_PROJECT_ID;
  else process.env.EAS_PROJECT_ID = ORIGINAL_PROJECT;
  if (ORIGINAL_GASTRONOMY_PROJECT == null) delete process.env.EAS_PROJECT_ID_GASTRONOMY;
  else process.env.EAS_PROJECT_ID_GASTRONOMY = ORIGINAL_GASTRONOMY_PROJECT;
});

describe('app.config universal binary', () => {
  it('keeps app.json identity when EXPO_PUBLIC_BRAND is unset', () => {
    delete process.env.EAS_PROJECT_ID;
    delete process.env.EAS_PROJECT_ID_GASTRONOMY;
    withEnv(undefined, () => {
      const config = loadConfig({ config: appJson.expo });
      expect(config.name).toBe(appJson.expo.name);
      expect(config.slug).toBe(appJson.expo.slug);
      expect(config.version).toBe(appJson.expo.version);
      expect(config.icon).toBe(appJson.expo.icon);
      expect(config.android?.package).toBe(appJson.expo.android.package);
      expect(config.ios?.bundleIdentifier).toBe(appJson.expo.ios.bundleIdentifier);
      expect(config.extra).toBeUndefined();
    });
  });

  it('treats universal and default as the same binary', () => {
    delete process.env.EAS_PROJECT_ID;
    withEnv('universal', () => {
      const config = loadConfig({ config: appJson.expo });
      expect(config.slug).toBe('cash-register');
      expect(config.android?.package).toBe('com.registrierkasse.cashregister');
    });
  });
});

describe('app.config gastronomy listing', () => {
  it('overrides store identity and keeps the same version', () => {
    delete process.env.EAS_PROJECT_ID;
    delete process.env.EAS_PROJECT_ID_GASTRONOMY;
    withEnv('gastronomy', () => {
      const config = loadConfig({ config: appJson.expo });
      expect(config.name).toBe('Regkasse Gastronomie');
      expect(config.slug).toBe('regkasse-gastronomie');
      expect(config.version).toBe(appJson.expo.version);
      expect(config.android?.package).toBe('com.registrierkasse.gastronomy');
      expect(config.ios?.bundleIdentifier).toBe('com.registrierkasse.gastronomy');
      expect(config.icon).toBe(appJson.expo.icon);
      expect(config.splash?.image).toBe('./assets/images/adaptive-icon.png');
      const splash = (config.plugins ?? []).find(
        (plugin) => Array.isArray(plugin) && plugin[0] === 'expo-splash-screen'
      );
      const splashOptions = Array.isArray(splash) ? splash[1] : undefined;
      const splashImage =
        splashOptions !== null &&
        typeof splashOptions === 'object' &&
        'image' in splashOptions &&
        typeof splashOptions.image === 'string'
          ? splashOptions.image
          : undefined;
      expect(splashImage).toBe('./assets/images/adaptive-icon.png');
    });
  });

  it('rejects an unknown brand instead of shipping the universal package under that name', () => {
    withEnv('vet', () => {
      expect(() => loadConfig({ config: appJson.expo })).toThrow(/Unknown EXPO_PUBLIC_BRAND/);
    });
  });
});
