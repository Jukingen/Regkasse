/**
 * useTenantChangeListener – tenant switch side effects and guard behavior.
 */
import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import {
    DEV_TENANT_CHANGED_EVENT,
    DEV_TENANT_LOCAL_STORAGE_KEY,
} from '@/features/auth/services/devTenant';
import {
    TENANT_SWITCH_RELOAD_SAFETY_MS,
    useTenantChangeListener,
} from '../useTenantChangeListener';

const mockQueryClientClear = vi.fn();
const mockBeginTenantSwitch = vi.fn();
const mockEndTenantSwitch = vi.fn();
const mockPersistBootstrap = vi.fn();
const mockRemoveToken = vi.fn();
const mockMessageInfo = vi.fn();
const mockReload = vi.fn();

vi.mock('@tanstack/react-query', () => ({
    useQueryClient: () => ({
        clear: mockQueryClientClear,
    }),
}));

vi.mock('@/features/auth/services/tenantSwitchController', () => ({
    beginTenantSwitch: () => mockBeginTenantSwitch(),
    endTenantSwitch: () => mockEndTenantSwitch(),
}));

vi.mock('@/features/auth/services/tenantStorage', () => ({
    tenantStorage: {
        persistBootstrap: (...args: unknown[]) => mockPersistBootstrap(...args),
    },
}));

vi.mock('@/features/auth/services/authStorage', () => ({
    authStorage: {
        removeToken: () => mockRemoveToken(),
    },
}));

vi.mock('@/hooks/useAntdApp', () => ({
    useAntdApp: () => ({
        message: {
            info: (...args: unknown[]) => mockMessageInfo(...args),
        },
        modal: { confirm: vi.fn() },
        notification: {},
    }),
}));

vi.mock('@/i18n', () => ({
    useI18n: () => ({
        t: (key: string, params?: { slug?: string }) =>
            params?.slug ? `${key}:${params.slug}` : key,
    }),
}));

function Wrapper({ children }: { children: React.ReactNode }) {
    return React.createElement(React.Fragment, null, children);
}

function dispatchDevTenantChanged(slug: string, previousSlug?: string) {
    window.dispatchEvent(
        new CustomEvent(DEV_TENANT_CHANGED_EVENT, {
            detail: { slug, previousSlug },
        }),
    );
}

function dispatchStorageChange(newValue: string, oldValue: string | null) {
    window.dispatchEvent(
        new StorageEvent('storage', {
            key: DEV_TENANT_LOCAL_STORAGE_KEY,
            newValue,
            oldValue,
            storageArea: window.localStorage,
        }),
    );
}

describe('useTenantChangeListener', () => {
    const originalNodeEnv = process.env.NODE_ENV;
    let consoleWarnSpy: ReturnType<typeof vi.spyOn>;

    beforeEach(() => {
        vi.clearAllMocks();
        process.env.NODE_ENV = 'development';
        window.localStorage.clear();
        mockReload.mockImplementation(() => undefined);
        consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
        Object.defineProperty(window, 'location', {
            value: { reload: mockReload, hostname: 'localhost' },
            configurable: true,
            writable: true,
        });
    });

    afterEach(() => {
        process.env.NODE_ENV = originalNodeEnv;
        window.localStorage.clear();
        consoleWarnSpy.mockRestore();
        vi.useRealTimers();
    });

    it('tenant change via DEV_TENANT_CHANGED_EVENT triggers clear, toast, persist, and reload', () => {
        window.localStorage.setItem(DEV_TENANT_LOCAL_STORAGE_KEY, 'cafe');
        renderHook(() => useTenantChangeListener(), { wrapper: Wrapper });

        act(() => {
            dispatchDevTenantChanged('bar', 'cafe');
        });

        expect(mockQueryClientClear).toHaveBeenCalledTimes(1);
        expect(mockMessageInfo).toHaveBeenCalledWith('adminShell.tenant.switch.toast:bar');
        expect(mockPersistBootstrap).toHaveBeenCalledWith({ tenantSlug: 'bar' });
        expect(mockRemoveToken).toHaveBeenCalledTimes(1);
        expect(mockBeginTenantSwitch).toHaveBeenCalledTimes(1);
        expect(mockReload).toHaveBeenCalledTimes(1);
    });

    it('same slug via DEV_TENANT_CHANGED_EVENT does not trigger clear or reload', () => {
        window.localStorage.setItem(DEV_TENANT_LOCAL_STORAGE_KEY, 'cafe');
        renderHook(() => useTenantChangeListener(), { wrapper: Wrapper });

        act(() => {
            dispatchDevTenantChanged('cafe', 'bar');
        });

        expect(mockQueryClientClear).not.toHaveBeenCalled();
        expect(mockMessageInfo).not.toHaveBeenCalled();
        expect(mockPersistBootstrap).not.toHaveBeenCalled();
        expect(mockReload).not.toHaveBeenCalled();
    });

    it('cross-tab storage event with dev tenant key triggers clear and reload', () => {
        window.localStorage.setItem(DEV_TENANT_LOCAL_STORAGE_KEY, 'cafe');
        renderHook(() => useTenantChangeListener(), { wrapper: Wrapper });

        act(() => {
            dispatchStorageChange('bar', 'cafe');
        });

        expect(mockQueryClientClear).toHaveBeenCalledTimes(1);
        expect(mockMessageInfo).toHaveBeenCalledWith('adminShell.tenant.switch.toast:bar');
        expect(mockPersistBootstrap).toHaveBeenCalledWith({ tenantSlug: 'bar' });
        expect(mockReload).toHaveBeenCalledTimes(1);
    });

    it('ignores a second switch while the first is still in progress (handlingRef guard)', () => {
        window.localStorage.setItem(DEV_TENANT_LOCAL_STORAGE_KEY, 'cafe');
        renderHook(() => useTenantChangeListener(), { wrapper: Wrapper });

        act(() => {
            dispatchDevTenantChanged('bar', 'cafe');
            dispatchDevTenantChanged('baz', 'bar');
        });

        expect(mockQueryClientClear).toHaveBeenCalledTimes(1);
        expect(mockBeginTenantSwitch).toHaveBeenCalledTimes(1);
        expect(mockReload).toHaveBeenCalledTimes(1);
        expect(mockPersistBootstrap).toHaveBeenCalledWith({ tenantSlug: 'bar' });
    });

    it('safety timeout resets guard when reload does not navigate away', () => {
        vi.useFakeTimers();
        window.localStorage.setItem(DEV_TENANT_LOCAL_STORAGE_KEY, 'cafe');
        renderHook(() => useTenantChangeListener(), { wrapper: Wrapper });

        act(() => {
            dispatchStorageChange('bar', 'cafe');
        });

        act(() => {
            dispatchStorageChange('baz', 'bar');
        });
        expect(mockReload).toHaveBeenCalledTimes(1);
        expect(mockEndTenantSwitch).not.toHaveBeenCalled();

        act(() => {
            vi.advanceTimersByTime(TENANT_SWITCH_RELOAD_SAFETY_MS);
        });

        expect(mockEndTenantSwitch).toHaveBeenCalledTimes(1);
        expect(consoleWarnSpy).toHaveBeenCalledWith('Tenant switch timeout - resetting state');

        act(() => {
            dispatchStorageChange('baz', 'bar');
        });

        expect(mockReload).toHaveBeenCalledTimes(2);
        expect(mockQueryClientClear).toHaveBeenCalledTimes(2);
    });

    it('releases guard immediately when reload throws synchronously', () => {
        mockReload.mockImplementation(() => {
            throw new Error('reload blocked');
        });
        window.localStorage.setItem(DEV_TENANT_LOCAL_STORAGE_KEY, 'cafe');
        renderHook(() => useTenantChangeListener(), { wrapper: Wrapper });

        act(() => {
            dispatchStorageChange('bar', 'cafe');
        });

        expect(mockEndTenantSwitch).toHaveBeenCalledTimes(1);

        mockReload.mockImplementation(() => undefined);
        act(() => {
            dispatchStorageChange('baz', 'bar');
        });

        expect(mockReload).toHaveBeenCalledTimes(2);
    });

    it('ignores storage events for unrelated keys', () => {
        window.localStorage.setItem(DEV_TENANT_LOCAL_STORAGE_KEY, 'cafe');
        renderHook(() => useTenantChangeListener(), { wrapper: Wrapper });

        act(() => {
            window.dispatchEvent(
                new StorageEvent('storage', {
                    key: 'unrelated_key',
                    newValue: 'bar',
                    oldValue: 'cafe',
                }),
            );
        });

        expect(mockQueryClientClear).not.toHaveBeenCalled();
        expect(mockReload).not.toHaveBeenCalled();
    });
});
