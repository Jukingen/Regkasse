import { renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useCommandPaletteKeyboard } from '@/features/command-palette/useCommandPaletteKeyboard';

describe('useCommandPaletteKeyboard', () => {
  beforeEach(() => {
    vi.stubGlobal('addEventListener', window.addEventListener.bind(window));
    vi.stubGlobal('removeEventListener', window.removeEventListener.bind(window));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('calls onOpen for Ctrl+K outside editable fields', () => {
    const onOpen = vi.fn();
    renderHook(() => useCommandPaletteKeyboard(onOpen));

    const event = new KeyboardEvent('keydown', { key: 'k', ctrlKey: true, bubbles: true });
    const prevented = { value: false };
    Object.defineProperty(event, 'preventDefault', {
      value: () => {
        prevented.value = true;
      },
    });
    document.body.dispatchEvent(event);

    expect(onOpen).toHaveBeenCalledTimes(1);
    expect(prevented.value).toBe(true);
  });

  it('ignores Ctrl+K when focus is in an input', () => {
    const onOpen = vi.fn();
    const input = document.createElement('input');
    document.body.appendChild(input);
    input.focus();

    renderHook(() => useCommandPaletteKeyboard(onOpen));

    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', ctrlKey: true, bubbles: true }));

    expect(onOpen).not.toHaveBeenCalled();
    document.body.removeChild(input);
  });
});
