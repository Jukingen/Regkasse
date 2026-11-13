import { describe, expect, it } from 'vitest';

import { resolveSidebarSubtitle, sidebarSubtitleKey } from '@/shared/sidebarSubtitle';

describe('sidebarSubtitle', () => {
  it('maps nav label keys to nav.subtitle.*', () => {
    expect(sidebarSubtitleKey('nav.cashRegisters')).toBe('nav.subtitle.cashRegisters');
    expect(sidebarSubtitleKey('nav.rksv.title')).toBe('nav.subtitle.rksv.title');
  });

  it('maps settings label keys to nav.subtitle aliases', () => {
    expect(sidebarSubtitleKey('settings.tabs.tse')).toBe('nav.subtitle.settingsTse');
  });

  it('resolves subtitle text when key exists', () => {
    const t = (key: string) =>
      key === 'nav.subtitle.rksv.title' ? 'TSE, Sonderbelege, DEP-Export' : 'Übersetzung nicht verfügbar';
    expect(resolveSidebarSubtitle(t, 'nav.rksv.title')).toMatch(/TSE/i);
  });
});
