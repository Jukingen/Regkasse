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
  it('relies on babel-preset-expo for reanimated/worklets (no duplicate plugin)', () => {
    const babel = fs.readFileSync(path.join(__dirname, '../babel.config.js'), 'utf8');
    expect(babel).toContain("'babel-preset-expo'");
    expect(babel).toContain("'module-resolver'");
    // Preset injects reanimated/worklets once; listing the plugin again breaks transform order.
    expect(babel).not.toContain("'react-native-reanimated/plugin'");
    expect(babel).toMatch(/Do not re-list those plugins|babel-preset-expo already enables/i);
  });
});
