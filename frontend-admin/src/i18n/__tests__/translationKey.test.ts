import { describe, expect, it } from 'vitest';

import { isAdminTranslationKey, typedT } from '../translationKey';

describe('translationKey', () => {
  it('isAdminTranslationKey kabul eder', () => {
    expect(isAdminTranslationKey('adminShell.branding.drawerTitle')).toBe(true);
    expect(isAdminTranslationKey('__definitely_not_a_key__')).toBe(false);
  });

  it('typedT bilinen anahtarı iletir', () => {
    const t = (key: string) => `ok:${key}`;
    expect(typedT(t, 'common.buttons.save')).toBe('ok:common.buttons.save');
  });
});
