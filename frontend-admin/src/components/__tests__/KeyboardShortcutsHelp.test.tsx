import { act, fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  KeyboardShortcutsHelp,
  openKeyboardShortcutsHelp,
} from '@/components/KeyboardShortcutsHelp';
import { KEYBOARD_SHORTCUT_EVENTS } from '@/shared/keyboardShortcuts';

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string) => key,
  }),
}));

describe('KeyboardShortcutsHelp', () => {
  it('opens from the trigger button and lists shortcuts', () => {
    render(<KeyboardShortcutsHelp />);

    fireEvent.click(screen.getByRole('button', { name: 'keyboardShortcuts.help' }));

    expect(screen.getByText('keyboardShortcuts.title')).toBeTruthy();
    expect(screen.getByText('keyboardShortcuts.search')).toBeTruthy();
    expect(screen.getByText('keyboardShortcuts.inputHint')).toBeTruthy();
  });

  it('opens via openKeyboardShortcutsHelp when showTrigger is false', async () => {
    render(<KeyboardShortcutsHelp showTrigger={false} />);

    expect(screen.queryByText('keyboardShortcuts.title')).toBeNull();
    act(() => {
      openKeyboardShortcutsHelp();
    });
    expect(await screen.findByText('keyboardShortcuts.title')).toBeTruthy();
  });

  it('closes on regkasse:closeModal', () => {
    const onOpenChange = vi.fn();
    render(<KeyboardShortcutsHelp open showTrigger={false} onOpenChange={onOpenChange} />);

    document.dispatchEvent(new CustomEvent(KEYBOARD_SHORTCUT_EVENTS.closeModal));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});
