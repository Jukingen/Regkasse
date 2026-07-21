import { describe, expect, it, vi } from 'vitest';

import { resolveActivityActionLabel } from '@/features/audit/utils/resolveActivityActionLabel';
import { USER_FACING_MISSING_TRANSLATION_LABEL } from '@/i18n/translationFallback';

describe('resolveActivityActionLabel', () => {
  it('uses common audit action labels when mapped', () => {
    const t = vi.fn((key: string) =>
      key === 'common.auditLogs.actionLabels.userUpdate' ? 'Benutzer aktualisiert' : key
    );
    expect(resolveActivityActionLabel('USER_UPDATE', t)).toBe('Benutzer aktualisiert');
  });

  it('uses activity.actions catalog for POS audit rows', () => {
    const t = vi.fn((key: string) =>
      key === 'activity.actions.POS_REG_READY'
        ? 'Kassenbereitschaft'
        : USER_FACING_MISSING_TRANSLATION_LABEL
    );
    expect(resolveActivityActionLabel('POS_REG_READY', t)).toBe('Kassenbereitschaft');
  });

  it('falls back to raw action when no translation exists', () => {
    const t = vi.fn(() => USER_FACING_MISSING_TRANSLATION_LABEL);
    expect(resolveActivityActionLabel('CustomTestAction', t)).toBe('CustomTestAction');
  });
});
