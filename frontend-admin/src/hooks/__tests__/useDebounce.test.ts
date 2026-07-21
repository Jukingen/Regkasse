import { act, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { useDebounce } from '@/hooks/useDebounce';

describe('useDebounce', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('returns initial value immediately and updates after delay', () => {
    vi.useFakeTimers();
    const { result, rerender } = renderHook(({ value, delay }) => useDebounce(value, delay), {
      initialProps: { value: 'a', delay: 200 },
    });

    expect(result.current).toBe('a');
    rerender({ value: 'b', delay: 200 });
    expect(result.current).toBe('a');
    act(() => {
      vi.advanceTimersByTime(199);
    });
    expect(result.current).toBe('a');
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(result.current).toBe('b');
  });

  it('cancels pending update when value changes quickly', () => {
    vi.useFakeTimers();
    const { result, rerender } = renderHook(({ value }) => useDebounce(value, 100), {
      initialProps: { value: 'one' },
    });
    rerender({ value: 'two' });
    act(() => {
      vi.advanceTimersByTime(50);
    });
    rerender({ value: 'three' });
    act(() => {
      vi.advanceTimersByTime(100);
    });
    expect(result.current).toBe('three');
  });
});
