import { describe, expect, it } from 'vitest';
import { buildAntdTheme } from '@/theme/buildAntdTheme';

describe('buildAntdTheme density overrides', () => {
  it('sets compact table padding', () => {
    const theme = buildAntdTheme('light', 'compact');
    expect(theme.components?.Table?.padding).toBe(8);
    expect(theme.components?.Table?.paddingLG).toBe(8);
  });

  it('sets comfortable table padding', () => {
    const theme = buildAntdTheme('dark', 'comfortable');
    expect(theme.components?.Table?.padding).toBe(16);
    expect(theme.components?.Table?.paddingLG).toBe(24);
  });
});
