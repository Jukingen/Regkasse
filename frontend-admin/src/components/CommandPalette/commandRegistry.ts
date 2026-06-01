'use client';

import { App } from 'antd';
import { useCallback, useMemo } from 'react';

import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { usePostApiAdminBackupTrigger } from '@/api/generated/admin-backup/admin-backup';
import { buildCommandItems } from '@/features/command-palette/buildCommandItems';
import {
    isCommandItemAllowed,
    type CommandPalettePermissionContext,
} from '@/features/command-palette/commandPalettePermissions';
import { describeBackupTriggerOutcome } from '@/features/backup-dr/logic/backupTriggerOutcome';
import { triggerErrorMessageBackupDashboard } from '@/features/backup-dr/logic/backupManualTriggerMessaging';
import type { CommandItem } from '@/components/CommandPalette/types';

export type UseCommandRegistryParams = {
    t: (key: string) => string;
    closePalette: () => void;
    permissionCtx: CommandPalettePermissionContext;
};

/**
 * Static command catalog (pinned shortcuts + sidebar pages), filtered by permissions.
 */
export function useCommandRegistry({
    t,
    closePalette,
    permissionCtx,
}: UseCommandRegistryParams): CommandItem[] {
    const { message, modal } = App.useApp();
    const router = useRouter();
    const queryClient = useQueryClient();

    const backupTrigger = usePostApiAdminBackupTrigger({
        mutation: {
            onSuccess: async (res) => {
                const fb = describeBackupTriggerOutcome(res);
                const suffix = res.orchestrationState?.trim()
                    ? ` ${t('backupDr.messages.orchestrationStateSuffix', { state: res.orchestrationState })}`
                    : '';
                const text = `${t(fb.messageKey)}${suffix}`;
                if (fb.level === 'success') message.success(text);
                else message.info(text);
                await queryClient.invalidateQueries({ queryKey: ['/api/admin/backup'] });
            },
            onError: (err) => message.error(triggerErrorMessageBackupDashboard(err, t)),
        },
    });

    const triggerBackup = useCallback(() => {
        modal.confirm({
            title: t('commandPalette.backupConfirmTitle'),
            content: t('commandPalette.backupConfirmBody'),
            okText: t('commandPalette.backupConfirmOk'),
            cancelText: t('commandPalette.backupConfirmCancel'),
            onOk: () => backupTrigger.mutateAsync({ data: {} }),
        });
    }, [backupTrigger, t]);

    return useMemo(() => {
        const built = buildCommandItems(t, router, closePalette, triggerBackup);
        return built.filter((item) => isCommandItemAllowed(item, permissionCtx));
    }, [t, router, closePalette, triggerBackup, permissionCtx]);
}
