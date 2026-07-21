import { describe, expect, it } from '@jest/globals';
import fs from 'fs';
import path from 'path';

const root = path.join(__dirname, '..');

function read(rel: string): string {
  return fs.readFileSync(path.join(root, rel), 'utf8');
}

describe('react-native-screens setup', () => {
  it('pins Expo SDK 56 compatible react-native-screens', () => {
    const pkg = JSON.parse(read('package.json')) as {
      dependencies: Record<string, string>;
    };
    expect(pkg.dependencies['react-native-screens']).toBe('4.25.2');
  });

  it('enables native screens and freeze in root layout before navigators', () => {
    const layout = read('app/_layout.tsx');
    expect(layout).toContain("from 'react-native-screens'");
    expect(layout).toContain('enableScreens(true)');
    expect(layout).toContain('enableFreeze(true)');

    const enableIdx = layout.indexOf('enableScreens(true)');
    const stackIdx = layout.indexOf('<Stack');
    expect(enableIdx).toBeGreaterThan(-1);
    expect(stackIdx).toBeGreaterThan(enableIdx);
  });

  it('opts nested stacks into freezeOnBlur for inactive routes', () => {
    expect(read('app/_layout.tsx')).toContain('freezeOnBlur: true');
    expect(read('app/(screens)/_layout.tsx')).toContain('freezeOnBlur: true');
    expect(read('app/customer/_layout.tsx')).toContain('freezeOnBlur: true');
  });
});
