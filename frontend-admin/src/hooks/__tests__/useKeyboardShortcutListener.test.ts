import { renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useKeyboardShortcutListener } from '@/hooks/useKeyboardShortcutListener';
import { KEYBOARD_SHORTCUT_EVENTS } from '@/shared/keyboardShortcuts';

describe('useKeyboardShortcutListener', () => {
  beforeEach(() => {
    vi.stubGlobal('addEventListener', document.addEventListener.bind(document));
    vi.stubGlobal('removeEventListener', document.removeEventListener.bind(document));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('invokes handler for CustomEvent detail', () => {
    const handler = vi.fn();
    renderHook(() =>
      useKeyboardShortcutListener<{ index: number }>(KEYBOARD_SHORTCUT_EVENTS.navigateTab, handler)
    );

    document.dispatchEvent(
      new CustomEvent(KEYBOARD_SHORTCUT_EVENTS.navigateTab, { detail: { index: 2 } })
    );

    expect(handler).toHaveBeenCalledWith({ index: 2 });
  });

  it('does not invoke handler when disabled', () => {
    const handler = vi.fn();
    renderHook(() =>
      useKeyboardShortcutListener(KEYBOARD_SHORTCUT_EVENTS.triggerSave, handler, false)
    );

    document.dispatchEvent(new CustomEvent(KEYBOARD_SHORTCUT_EVENTS.triggerSave));

    expect(handler).not.toHaveBeenCalled();
  });
});
