'use client';

import {
    createContext,
    useCallback,
    useContext,
    useMemo,
    useState,
    type ReactNode,
} from 'react';

import { shouldShowHeaderDevTenantSwitch } from '@/features/super-admin/utils/tenantHeaderSwitcher';

export type HeaderTenantSwitcherContextValue = {
    isAvailable: boolean;
    open: boolean;
    setOpen: (open: boolean) => void;
    toggle: () => void;
};

const noop = () => {};

const defaultValue: HeaderTenantSwitcherContextValue = {
    isAvailable: false,
    open: false,
    setOpen: noop,
    toggle: noop,
};

const HeaderTenantSwitcherContext = createContext<HeaderTenantSwitcherContextValue>(defaultValue);

export function HeaderTenantSwitcherProvider({ children }: { children: ReactNode }) {
    const [open, setOpen] = useState(false);
    const isAvailable = shouldShowHeaderDevTenantSwitch();
    const toggle = useCallback(() => setOpen((current) => !current), []);

    const value = useMemo(
        () => ({
            isAvailable,
            open,
            setOpen,
            toggle,
        }),
        [isAvailable, open, toggle],
    );

    return (
        <HeaderTenantSwitcherContext.Provider value={value}>
            {children}
        </HeaderTenantSwitcherContext.Provider>
    );
}

export function useHeaderTenantSwitcher(): HeaderTenantSwitcherContextValue {
    return useContext(HeaderTenantSwitcherContext);
}
