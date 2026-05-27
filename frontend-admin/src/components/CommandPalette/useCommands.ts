'use client';

import { useCallback, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import {
    canManageUsers,
    canShowRksvMenu,
    canViewUsers,
    isSuperAdmin,
} from '@/features/auth/constants/roles';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { useI18n } from '@/i18n';
import { useCommandRegistry } from '@/components/CommandPalette/commandRegistry';
import { fuseSearchCommandItems, sortCommandItems } from '@/features/command-palette/commandPaletteSearch';
import { recreateDynamicCommandItem } from '@/features/command-palette/recreateDynamicCommand';
import {
    readRecentCommandSnapshots,
    resolveRecentCommandItems,
    storeRecentCommand,
} from '@/features/command-palette/recentCommands';
import { useCommandPaletteUserSearch } from '@/features/command-palette/useCommandPaletteUserSearch';
import { useCommandPaletteRegisterSearch } from '@/features/command-palette/useCommandPaletteRegisterSearch';
import { useCommandPaletteReceiptSearch } from '@/features/command-palette/useCommandPaletteReceiptSearch';
import type { CommandItem } from '@/components/CommandPalette/types';

const EMPTY_PERMISSIONS: string[] = [];
const MIN_API_SEARCH_LEN = 2;

export type UseCommandsParams = {
    open: boolean;
    searchTerm: string;
    onClose: () => void;
};

export function useCommands({ open, searchTerm, onClose }: UseCommandsParams) {
    const { t } = useI18n();
    const router = useRouter();
    const { user } = useAuth();
    const [recentTick, setRecentTick] = useState(0);

    const permissions = user?.permissions ?? EMPTY_PERMISSIONS;
    const usePermissionFirst = permissions.length > 0;
    const userRole = user?.role ?? '';
    const superAdmin = isSuperAdmin(userRole);

    const permissionCtx = useMemo(
        () => ({
            usePermissionFirst,
            permissions,
            userRole,
            isMenuItemAllowed,
            canViewUsers,
            canManageUsers,
            canShowRksvMenu,
            canShowPlatformAdminMenu: (role: string) => isSuperAdmin(role),
        }),
        [usePermissionFirst, permissions, userRole],
    );

    const staticCommands = useCommandRegistry({
        t,
        closePalette: onClose,
        permissionCtx,
    });

    const commandById = useMemo(
        () => new Map(staticCommands.map((item) => [item.id, item])),
        [staticCommands],
    );

    const recentCommands = useMemo(() => {
        void recentTick;
        return resolveRecentCommandItems(
            readRecentCommandSnapshots(),
            commandById,
            (snapshot) => recreateDynamicCommandItem(snapshot, router, onClose),
        );
    }, [recentTick, commandById, router, onClose]);

    const canSearchUsers = useMemo(() => {
        if (usePermissionFirst) {
            return permissions.includes(PERMISSIONS.USER_VIEW);
        }
        return canViewUsers(userRole);
    }, [usePermissionFirst, permissions, userRole]);

    const canSearchRegisters = useMemo(() => {
        if (usePermissionFirst) {
            return isMenuItemAllowed('/kassenverwaltung', permissions);
        }
        return true;
    }, [usePermissionFirst, permissions]);

    const canSearchReceipts = useMemo(() => {
        if (usePermissionFirst) {
            return isMenuItemAllowed('/receipts', permissions);
        }
        return true;
    }, [usePermissionFirst, permissions]);

    const apiSearchEnabled = open && searchTerm.trim().length >= MIN_API_SEARCH_LEN;

    const { items: userResults, isLoading: usersLoading } = useCommandPaletteUserSearch(searchTerm, {
        enabled: apiSearchEnabled && canSearchUsers,
        isSuperAdmin: superAdmin,
        closePalette: onClose,
    });

    const { items: registerResults, isLoading: registersLoading } = useCommandPaletteRegisterSearch(
        searchTerm,
        {
            enabled: apiSearchEnabled && canSearchRegisters,
            isSuperAdmin: superAdmin,
            closePalette: onClose,
        },
    );

    const { items: receiptResults, isLoading: receiptsLoading } = useCommandPaletteReceiptSearch(
        searchTerm,
        {
            enabled: apiSearchEnabled && canSearchReceipts,
            closePalette: onClose,
        },
    );

    const trimmedSearch = searchTerm.trim();

    const results: CommandItem[] = useMemo(() => {
        if (!trimmedSearch) {
            return recentCommands;
        }
        const staticMatches = fuseSearchCommandItems(staticCommands, trimmedSearch);
        const dynamic = [...userResults, ...receiptResults, ...registerResults];
        return sortCommandItems([...dynamic, ...staticMatches]);
    }, [
        trimmedSearch,
        recentCommands,
        staticCommands,
        userResults,
        receiptResults,
        registerResults,
    ]);

    const isLoading =
        apiSearchEnabled &&
        ((usersLoading && canSearchUsers) ||
            (registersLoading && canSearchRegisters) ||
            (receiptsLoading && canSearchReceipts));

    const refreshRecent = useCallback(() => {
        setRecentTick((v) => v + 1);
    }, []);

    const runCommand = useCallback(
        (item: CommandItem) => {
            if (item.dynamic) return;
            storeRecentCommand(item);
            refreshRecent();
            item.action();
        },
        [refreshRecent],
    );

    return {
        results: results.filter((item) => !item.dynamic),
        isLoading,
        runCommand,
        refreshRecent,
    };
}
