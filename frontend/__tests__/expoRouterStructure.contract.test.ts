/**
 * Contract: expo-router file tree and navigation hygiene for POS frontend.
 * Does not boot Metro — asserts route files / absence of accidental routes.
 */
import { describe, expect, test } from '@jest/globals';
import * as fs from 'fs';
import * as path from 'path';

const appRoot = path.join(__dirname, '../app');

function exists(rel: string): boolean {
  return fs.existsSync(path.join(appRoot, rel));
}

describe('expo-router structure contract', () => {
  test('core layout files exist', () => {
    expect(exists('_layout.tsx')).toBe(true);
    expect(exists('(auth)/_layout.tsx')).toBe(true);
    expect(exists('(tabs)/_layout.tsx')).toBe(true);
    expect(exists('(screens)/_layout.tsx')).toBe(true);
    expect(exists('customer/_layout.tsx')).toBe(true);
    expect(exists('index.tsx')).toBe(true);
  });

  test('primary auth → tabs routes exist', () => {
    expect(exists('(auth)/login.tsx')).toBe(true);
    expect(exists('(auth)/change-password.tsx')).toBe(true);
    expect(exists('(tabs)/cash-register.tsx')).toBe(true);
    expect(exists('(tabs)/cart.tsx')).toBe(true);
    expect(exists('(tabs)/settings.tsx')).toBe(true);
    expect(exists('(tabs)/admin-menu.tsx')).toBe(true);
  });

  test('payment / operational stack screens exist', () => {
    expect(exists('(screens)/PaymentHistoryScreen.tsx')).toBe(true);
    expect(exists('(screens)/SplitScreen.tsx')).toBe(true);
    expect(exists('(screens)/offline-queue.tsx')).toBe(true);
    expect(exists('(screens)/license-activate.tsx')).toBe(true);
  });

  test('components are not registered as routes under app/', () => {
    expect(exists('components')).toBe(false);
    expect(exists('simple-test.tsx')).toBe(false);
  });

  test('scanners live under components/ (not app routes)', () => {
    const componentsRoot = path.join(__dirname, '../components');
    expect(fs.existsSync(path.join(componentsRoot, 'QrCustomerScanner.tsx'))).toBe(true);
    expect(fs.existsSync(path.join(componentsRoot, 'VoucherScanner.tsx'))).toBe(true);
  });

  test('settings navigates to tab admin-menu (not screens duplicate)', () => {
    const settingsSource = fs.readFileSync(path.join(appRoot, '(tabs)/settings.tsx'), 'utf8');
    expect(settingsSource).toContain("router.push('/(tabs)/admin-menu'");
    expect(settingsSource).not.toContain("router.push('/(screens)/admin-menu'");
  });

  test('auth layout redirects authenticated users to cash-register', () => {
    const authLayout = fs.readFileSync(path.join(appRoot, '(auth)/_layout.tsx'), 'utf8');
    expect(authLayout).toContain('Redirect');
    expect(authLayout).toContain('/(tabs)/cash-register');
  });

  test('tabs layout redirects unauthenticated users to login', () => {
    const tabsLayout = fs.readFileSync(path.join(appRoot, '(tabs)/_layout.tsx'), 'utf8');
    expect(tabsLayout).toContain('Redirect');
    expect(tabsLayout).toContain('/(auth)/login');
  });

  test('root layout keeps native splash until app is ready', () => {
    const rootLayout = fs.readFileSync(path.join(appRoot, '_layout.tsx'), 'utf8');
    expect(rootLayout).toContain("from 'expo-splash-screen'");
    expect(rootLayout).toContain('SplashScreen.preventAutoHideAsync');
    expect(rootLayout).toMatch(/SplashScreen\.hide(?:Async)?\s*\(/);
  });

  test('root layout loads custom fonts before render and gates splash', () => {
    const rootLayout = fs.readFileSync(path.join(appRoot, '_layout.tsx'), 'utf8');
    expect(rootLayout).toContain("from 'expo-font'");
    expect(rootLayout).toContain('useFonts');
    expect(rootLayout).toContain('CUSTOM_FONT_MAP');
    expect(rootLayout).toContain('fontsReady');
    expect(rootLayout).toContain('isAppReady');

    const fontsModule = fs.readFileSync(path.join(__dirname, '../constants/fonts.ts'), 'utf8');
    expect(fontsModule).toContain('OCRA-B');
    expect(fontsModule).toContain('RECEIPT_FONT_FAMILY');
    expect(fontsModule).toContain('RECEIPT_FONT_FALLBACK');
    expect(fontsModule).toContain('monospace');
    expect(fs.existsSync(path.join(__dirname, '../assets/fonts/OCRA-B.ttf'))).toBe(true);
  });

  test('root layout hides status bar during splash and uses themed status bar', () => {
    const rootLayout = fs.readFileSync(path.join(appRoot, '_layout.tsx'), 'utf8');
    expect(rootLayout).toContain('StatusBar.setHidden(true)');
    expect(rootLayout).toContain('StatusBar.setHidden(false');
    expect(rootLayout).toContain('ThemedStatusBar');
    expect(rootLayout).not.toMatch(/<StatusBar\s+style=["']auto["']/);

    const themedStatusBar = fs.readFileSync(
      path.join(__dirname, '../components/ThemedStatusBar.tsx'),
      'utf8'
    );
    expect(themedStatusBar).toContain('useTheme');
    expect(themedStatusBar).toMatch(/isDark\s*\?\s*['"]light['"]\s*:\s*['"]dark['"]/);
  });

  test('auth screens keep light status bar on dark/gradient chrome', () => {
    const login = fs.readFileSync(path.join(appRoot, '(auth)/login.tsx'), 'utf8');
    const changePassword = fs.readFileSync(
      path.join(appRoot, '(auth)/change-password.tsx'),
      'utf8'
    );
    expect(login).toContain('<StatusBar style="light"');
    expect(changePassword).toContain('<StatusBar style="light"');
  });

  test('app.json configures expo-splash-screen plugin with image asset', () => {
    const appJsonPath = path.join(__dirname, '../app.json');
    const appJson = JSON.parse(fs.readFileSync(appJsonPath, 'utf8')) as {
      expo: { plugins: (string | [string, Record<string, unknown>])[] };
    };
    const splashPlugin = appJson.expo.plugins.find(
      (entry) => Array.isArray(entry) && entry[0] === 'expo-splash-screen'
    );
    expect(splashPlugin).toBeDefined();
    const [, options] = splashPlugin as [string, { image?: string; backgroundColor?: string }];
    expect(options.image).toBe('./assets/images/adaptive-icon.png');
    expect(options.backgroundColor).toBeTruthy();
    expect(fs.existsSync(path.join(__dirname, '../assets/images/adaptive-icon.png'))).toBe(true);
  });

  test('app.json configures expo-font plugin with OCRA-B asset', () => {
    const appJsonPath = path.join(__dirname, '../app.json');
    const appJson = JSON.parse(fs.readFileSync(appJsonPath, 'utf8')) as {
      expo: { plugins: (string | [string, Record<string, unknown>])[] };
    };
    const fontPlugin = appJson.expo.plugins.find(
      (entry) => Array.isArray(entry) && entry[0] === 'expo-font'
    );
    expect(fontPlugin).toBeDefined();
    const [, options] = fontPlugin as [string, { fonts?: string[] }];
    expect(options.fonts).toContain('./assets/fonts/OCRA-B.ttf');
  });

  test('app.json starts with status bar hidden for splash', () => {
    const appJsonPath = path.join(__dirname, '../app.json');
    const appJson = JSON.parse(fs.readFileSync(appJsonPath, 'utf8')) as {
      expo: {
        scheme?: string | string[];
        plugins: (string | [string, Record<string, unknown>])[];
      };
    };
    const statusBarPlugin = appJson.expo.plugins.find(
      (entry) =>
        entry === 'expo-status-bar' || (Array.isArray(entry) && entry[0] === 'expo-status-bar')
    );
    expect(statusBarPlugin).toBeDefined();
    expect(Array.isArray(statusBarPlugin)).toBe(true);
    const [, options] = statusBarPlugin as [string, { hidden?: boolean; style?: string }];
    expect(options.hidden).toBe(true);

    const scheme = appJson.expo.scheme;
    const schemes = Array.isArray(scheme) ? scheme : scheme ? [scheme] : [];
    expect(schemes).toEqual(expect.arrayContaining(['cashregister', 'regkasse']));
  });

  test('tenant deep-link bridge route exists', () => {
    expect(exists('tenant/[slug].tsx')).toBe(true);
    expect(exists('order-tracker.tsx')).toBe(true);
  });

  test('app.json configures system UI background and plugins', () => {
    const appJsonPath = path.join(__dirname, '../app.json');
    const appJson = JSON.parse(fs.readFileSync(appJsonPath, 'utf8')) as {
      expo: {
        backgroundColor?: string;
        ios?: { backgroundColor?: string };
        plugins: (string | [string, Record<string, unknown>])[];
      };
    };
    expect(appJson.expo.backgroundColor).toBe('#F5F5F5');
    expect(appJson.expo.ios?.backgroundColor).toBe('#F5F5F5');
    expect(appJson.expo.plugins).toEqual(expect.arrayContaining(['expo-system-ui']));
    const navPlugin = appJson.expo.plugins.find(
      (entry) => Array.isArray(entry) && entry[0] === 'expo-navigation-bar'
    );
    expect(navPlugin).toBeDefined();
    const [, navOptions] = navPlugin as [string, { hidden?: boolean; style?: string }];
    expect(navOptions.hidden).toBe(false);
  });

  test('root layout wires ThemedStatusBar and ThemedSystemUI', () => {
    const rootLayout = fs.readFileSync(path.join(appRoot, '_layout.tsx'), 'utf8');
    expect(rootLayout).toContain('ThemedStatusBar');
    expect(rootLayout).toContain('ThemedSystemUI');
    expect(rootLayout).toContain('useDeepLinkNavigation');
  });
});
