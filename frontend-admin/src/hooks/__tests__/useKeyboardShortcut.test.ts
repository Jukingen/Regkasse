import { renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { useKeyboardShortcut } from '@/hooks/useKeyboardShortcut';

describe('useKeyboardShortcut', () => {
  it('calls onTrigger for Ctrl+K outside inputs', () => {
    const onTrigger = vi.fn();
    renderHook(() =>
      useKeyboardShortcut('k', {
        metaOrCtrl: true,
        onTrigger,
      })
    );

    const event = new KeyboardEvent('keydown', { key: 'k', ctrlKey: true, bubbles: true });
    let prevented = false;
    Object.defineProperty(event, 'preventDefault', {
      value: () => {
        prevented = true;
      },
    });
    document.body.dispatchEvent(event);

    expect(onTrigger).toHaveBeenCalledTimes(1);
    expect(prevented).toBe(true);
  });
});
