import { describe, expect, it } from 'vitest';
import { getGlobalSearchOptionId, getGlobalSearchShortcutLabel } from '@/shared/globalSearchA11y';

describe('globalSearchA11y', () => {
    it('builds stable option ids', () => {
        expect(getGlobalSearchOptionId('listbox', 2)).toBe('listbox-option-2');
    });

    it('returns a shortcut label', () => {
        expect(getGlobalSearchShortcutLabel()).toMatch(/K/);
    });
});
