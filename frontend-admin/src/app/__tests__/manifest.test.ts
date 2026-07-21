import { existsSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import manifest from '@/app/manifest';

describe('PWA manifest', () => {
  it('exposes required install fields', () => {
    const m = manifest();
    expect(m.name).toBe('Regkasse Admin');
    expect(m.short_name).toBe('Regkasse');
    expect(m.start_url).toBe('/dashboard');
    expect(m.display).toBe('standalone');
    expect(m.theme_color).toBe('#1677ff');
    expect(m.background_color).toBe('#ffffff');
    expect(m.icons?.length).toBeGreaterThanOrEqual(2);
  });

  it('references icon files that exist under public/', () => {
    const m = manifest();
    for (const icon of m.icons ?? []) {
      const file = join(process.cwd(), 'public', icon.src.replace(/^\//, ''));
      expect(existsSync(file), `missing ${icon.src}`).toBe(true);
    }
    expect(existsSync(join(process.cwd(), 'public/manifest.json'))).toBe(true);
    expect(existsSync(join(process.cwd(), 'public/apple-touch-icon.png'))).toBe(true);
  });
});
