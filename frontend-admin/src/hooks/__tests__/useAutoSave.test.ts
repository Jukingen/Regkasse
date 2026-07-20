import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import {
    clearAutoSaveDraft,
    readAutoSaveDraft,
    useAutoSave,
    writeAutoSaveDraft,
} from '@/hooks/useAutoSave';

describe('useAutoSave', () => {
    beforeEach(() => {
        vi.useFakeTimers();
        window.localStorage.clear();
    });

    afterEach(() => {
        vi.useRealTimers();
        window.localStorage.clear();
    });

    it('debounces and calls onSave', async () => {
        const onSave = vi.fn().mockResolvedValue(undefined);
        const { result, rerender } = renderHook(
            ({ data }) => useAutoSave(data, onSave, 200, { skipInitial: true }),
            { initialProps: { data: { name: 'a' } } },
        );

        rerender({ data: { name: 'b' } });
        expect(result.current.saving).toBe(false);

        await act(async () => {
            await vi.advanceTimersByTimeAsync(200);
        });

        expect(onSave).toHaveBeenCalledWith({ name: 'b' });
        expect(result.current.saving).toBe(false);
        expect(result.current.saved).toBe(true);
    });

    it('sets error when onSave rejects', async () => {
        const onSave = vi.fn().mockRejectedValue(new Error('fail'));
        const { rerender, result } = renderHook(
            ({ data }) => useAutoSave(data, onSave, 50, { skipInitial: true }),
            { initialProps: { data: { a: 1 } } },
        );

        rerender({ data: { a: 2 } });
        await act(async () => {
            await vi.advanceTimersByTimeAsync(50);
        });

        expect(result.current.error).toBe(true);
        expect(result.current.saved).toBe(false);
    });

    it('draft helpers read/write/clear localStorage', () => {
        writeAutoSaveDraft('test:draft', { name: 'stored' });
        expect(readAutoSaveDraft('test:draft')).toEqual({ name: 'stored' });
        clearAutoSaveDraft('test:draft');
        expect(readAutoSaveDraft('test:draft')).toBeNull();
    });
});
