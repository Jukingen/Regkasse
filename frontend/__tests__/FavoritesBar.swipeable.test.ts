import { describe, expect, it } from '@jest/globals';
import fs from 'fs';
import path from 'path';

/**
 * Source-level guard: FavoritesBar must use ReanimatedSwipeable (RNGH deprecates
 * the Animated-based Swipeable) and Gesture Handler ScrollView for horizontal lists.
 */
describe('FavoritesBar gesture migration', () => {
  const source = fs.readFileSync(path.join(__dirname, '../components/FavoritesBar.tsx'), 'utf8');

  it('imports ReanimatedSwipeable (not deprecated Swipeable)', () => {
    expect(source).toContain("from 'react-native-gesture-handler/ReanimatedSwipeable'");
    expect(source).not.toContain("import { Swipeable } from 'react-native-gesture-handler'");
  });

  it('uses Gesture Handler ScrollView for swipe/scroll negotiation', () => {
    expect(source).toMatch(/import\s*\{\s*ScrollView\s*\}\s*from\s*'react-native-gesture-handler'/);
  });

  it('tunes swipe friction/threshold for delete actions', () => {
    expect(source).toContain('friction={2}');
    expect(source).toContain('overshootRight={false}');
    expect(source).toContain('rightThreshold={40}');
  });
});

describe('Reanimated config', () => {
  it('registers react-native-reanimated/plugin after other babel plugins', () => {
    const babel = fs.readFileSync(path.join(__dirname, '../babel.config.js'), 'utf8');
    const reanimatedIdx = babel.indexOf("'react-native-reanimated/plugin'");
    const moduleResolverIdx = babel.indexOf("'module-resolver'");
    expect(reanimatedIdx).toBeGreaterThan(-1);
    expect(moduleResolverIdx).toBeGreaterThan(-1);
    expect(reanimatedIdx).toBeGreaterThan(moduleResolverIdx);
    expect(babel).toContain('// Must be last');
  });
});
