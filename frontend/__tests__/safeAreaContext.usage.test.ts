import { describe, expect, it } from '@jest/globals';
import fs from 'fs';
import path from 'path';

const root = path.join(__dirname, '..');

function read(rel: string): string {
  return fs.readFileSync(path.join(root, rel), 'utf8');
}

describe('SafeAreaContext usage', () => {
  it('pins react-native-safe-area-context to Expo SDK 56 range', () => {
    const pkg = JSON.parse(read('package.json')) as {
      dependencies: Record<string, string>;
    };
    expect(pkg.dependencies['react-native-safe-area-context']).toMatch(/^~?5\.7\./);
  });

  it('roots SafeAreaProvider with initialWindowMetrics', () => {
    const src = read('app/_layout.tsx');
    expect(src).toContain('SafeAreaProvider');
    expect(src).toContain('initialWindowMetrics');
    expect(src).toContain('initialMetrics={initialWindowMetrics}');
  });

  it('applies top inset once in tab chrome (avoids double padding)', () => {
    const tabs = read('app/(tabs)/_layout.tsx');
    expect(tabs).toContain('paddingTop: insets.top + 6');
    expect(tabs).toContain('headerStatusBarHeight: 0');
    expect(tabs).toContain('paddingBottom: insets.bottom');
  });

  it('does not use React Native SafeAreaView on cash-register or demos', () => {
    const cash = read('app/(tabs)/cash-register.tsx');
    expect(cash).not.toMatch(/SafeAreaView/);
    expect(cash).toContain('useSafeAreaInsets');

    const taskmaster = read('app/(screens)/taskmaster.tsx');
    expect(taskmaster).toContain("from 'react-native-safe-area-context'");
    expect(taskmaster).not.toMatch(/SafeAreaView,\s*$/m);

    const simpleTodo = read('components/SimpleTodo.tsx');
    expect(simpleTodo).toContain("from 'react-native-safe-area-context'");
    expect(simpleTodo).not.toMatch(/SafeAreaView\s*\}\s*from\s*'react-native'/);
  });

  it('wraps auth screens with safe-area-context SafeAreaView', () => {
    const login = read('app/(auth)/login.tsx');
    expect(login).toContain("from 'react-native-safe-area-context'");
    expect(login).toContain("edges={['top', 'bottom']}");

    const changePassword = read('app/(auth)/change-password.tsx');
    expect(changePassword).toContain("from 'react-native-safe-area-context'");
    expect(changePassword).toContain("edges={['top', 'bottom']}");
  });
});
