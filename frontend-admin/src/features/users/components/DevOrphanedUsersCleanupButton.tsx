'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { DeleteOutlined } from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from 'antd';
import { cleanupOrphanedDevUsers } from '@/features/users/api/devCleanup';
import { listQueryKey } from '@/features/users/api/usersGateway';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n/I18nProvider';
import { technicalConsole } from '@/shared/dev/technicalConsole';

const isDevelopmentBuild = process.env.NODE_ENV === 'development';

type DevOrphanedUsersCleanupButtonProps = {
    /** Super-admin unified users view (invalidates platform user queries). */
    invalidatePlatformUsers?: boolean;
    onTenantListRefetch?: () => void;
};

export function DevOrphanedUsersCleanupButton({
    invalidatePlatformUsers = false,
    onTenantListRefetch,
}: DevOrphanedUsersCleanupButtonProps) {
  const { message, modal } = useAntdApp();
    const { isSuperAdmin } = usePermissions();

    const { t } = useI18n();
    const queryClient = useQueryClient();

    const cleanupMutation = useMutation({
        mutationFn: cleanupOrphanedDevUsers,
        onSuccess: async (result) => {
            message.success(
                t('users.dev.cleanupSuccess', {
                    users: result.deletedUsers,
                    memberships: result.deletedMemberships,
                }),
            );
            if (invalidatePlatformUsers) {
                await queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
            } else {
                await queryClient.invalidateQueries({ queryKey: listQueryKey });
            }
            onTenantListRefetch?.();
        },
        onError: (error) => {
            technicalConsole.error('Dev orphaned-user cleanup failed', error);
            message.error(t('users.dev.cleanupError'));
        },
    });

    if (!isDevelopmentBuild || !isSuperAdmin) {
        return null;
    }

    const handleClick = () => {
        modal.confirm({
            title: t('users.dev.cleanupConfirmTitle'),
            content: t('users.dev.cleanupConfirmContent'),
            okText: t('users.dev.cleanupConfirmOk'),
            cancelText: t('users.dev.cleanupConfirmCancel'),
            okButtonProps: { danger: true },
            onOk: () => cleanupMutation.mutateAsync(),
        });
    };

    return (
        <Button
            danger
            icon={<DeleteOutlined />}
            loading={cleanupMutation.isPending}
            onClick={handleClick}
        >
            {t('users.dev.cleanupButton')}
        </Button>
    );
}
