'use client';

import React, { useCallback, useState } from 'react';
import { Button, Dropdown, Space } from 'antd';
import type { MenuProps } from 'antd';
import {
    BgColorsOutlined,
    DatabaseOutlined,
    DeleteOutlined,
    EyeOutlined,
    GlobalOutlined,
    LoginOutlined,
    MoreOutlined,
    PauseCircleOutlined,
    PlayCircleOutlined,
    KeyOutlined,
    TeamOutlined,
    UndoOutlined,
} from '@ant-design/icons';
import Link from 'next/link';

import { ConfirmDialog } from '@/components/ConfirmDialog';
import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { TenantArchiveConfirmModal } from '@/features/super-admin/components/TenantArchiveConfirmModal';
import { TenantPermanentDeleteModal } from '@/features/super-admin/components/TenantPermanentDeleteModal';
import { buildTenantDeletePreparationHref } from '@/features/super-admin/utils/tenantDeleteDependencyUi';
import { buildAdminUsersPageHref } from '@/features/users/utils/adminUsersPageUrl';
import { useI18n } from '@/i18n';

export type TenantTableActionsProps = {
    tenant: AdminTenantListItem;
    restorePending?: boolean;
    impersonatePending?: boolean;
    suspendPending?: boolean;
    onEdit: (tenant: AdminTenantListItem) => void;
    onSuspend: (id: string, status: string) => void;
    onImpersonate: (id: string) => void;
    onRestore: (id: string) => void;
    onArchiveSuccess?: (id: string) => void;
    onPermanentDeleteSuccess?: (id: string) => void;
};

export function TenantTableActions({
    tenant,
    restorePending,
    impersonatePending,
    suspendPending,
    onEdit,
    onSuspend,
    onImpersonate,
    onRestore,
    onArchiveSuccess,
    onPermanentDeleteSuccess,
}: TenantTableActionsProps) {
    const { t } = useI18n();
    const [archiveOpen, setArchiveOpen] = useState(false);
    const [permanentDeleteOpen, setPermanentDeleteOpen] = useState(false);
    const [restoreConfirmOpen, setRestoreConfirmOpen] = useState(false);

    const closeArchiveModal = useCallback(() => setArchiveOpen(false), []);
    const closePermanentDeleteModal = useCallback(() => setPermanentDeleteOpen(false), []);
    const closeRestoreConfirm = useCallback(() => setRestoreConfirmOpen(false), []);

    if (tenant.status === 'deleted') {
        return (
            <Space size="small" wrap>
                <Link href={`/admin/tenants/${tenant.id}`}>
                    <Button size="small" icon={<EyeOutlined />}>
                        {t('tenants.actions.view')}
                    </Button>
                </Link>
                <Button
                    size="small"
                    icon={<UndoOutlined />}
                    loading={restorePending}
                    onClick={() => setRestoreConfirmOpen(true)}
                >
                    {t('tenants.actions.restore')}
                </Button>
                <Link href={buildTenantDeletePreparationHref(tenant.id)}>
                    <Button size="small">{t('tenants.deleteDependencies.checkDependencies')}</Button>
                </Link>
                <Button
                    size="small"
                    danger
                    icon={<DeleteOutlined />}
                    onClick={() => setPermanentDeleteOpen(true)}
                >
                    {t('tenants.actions.hardDelete')}
                </Button>
                <ConfirmDialog
                    open={restoreConfirmOpen}
                    title={t('tenants.confirmRestore.title')}
                    message={t('tenants.confirmRestore.body')}
                    type="warning"
                    confirmText={t('tenants.actions.restore')}
                    loading={restorePending}
                    onConfirm={() => {
                        onRestore(tenant.id);
                        setRestoreConfirmOpen(false);
                    }}
                    onCancel={closeRestoreConfirm}
                />
                <TenantPermanentDeleteModal
                    open={permanentDeleteOpen}
                    tenantId={tenant.id}
                    tenantName={tenant.name}
                    tenantSlug={tenant.slug}
                    onClose={closePermanentDeleteModal}
                    onSuccess={() => onPermanentDeleteSuccess?.(tenant.id)}
                />
            </Space>
        );
    }

    const moreItems: MenuProps['items'] = [
        {
            key: 'view',
            icon: <EyeOutlined />,
            label: <Link href={`/admin/tenants/${tenant.id}`}>{t('tenants.actions.view')}</Link>,
        },
        {
            key: 'users',
            icon: <TeamOutlined />,
            label: (
                <Link href={buildAdminUsersPageHref(tenant.id)}>
                    {t('tenants.actions.manageUsers')}
                </Link>
            ),
        },
        {
            key: 'license',
            icon: <KeyOutlined />,
            label: (
                <Link href={`/admin/tenants/${tenant.id}?tab=license`}>
                    {t('tenants.actions.manageLicense')}
                </Link>
            ),
        },
        {
            key: 'digital',
            icon: <GlobalOutlined />,
            label: (
                <Link href={`/tenant/${tenant.id}/digital`}>
                    {t('tenants.digitalServices.openAction')}
                </Link>
            ),
        },
        {
            key: 'domain',
            icon: <GlobalOutlined />,
            label: (
                <Link href={`/tenant/${tenant.id}/domain`}>
                    {t('tenants.domainManagement.openAction')}
                </Link>
            ),
        },
        {
            key: 'customize',
            icon: <BgColorsOutlined />,
            label: (
                <Link href={`/tenant/${tenant.id}/customize`}>
                    {t('tenants.customization.openAction')}
                </Link>
            ),
        },
        {
            key: 'data-management',
            icon: <DatabaseOutlined />,
            label: (
                <Link href={`/tenant/${tenant.id}/data-management`}>
                    {t('dataManagement.openAction')}
                </Link>
            ),
        },
        { type: 'divider' },
        {
            key: 'edit',
            label: t('tenants.actions.edit'),
            onClick: () => onEdit(tenant),
        },
        {
            key: 'impersonate',
            icon: <LoginOutlined />,
            label: t('tenants.actions.impersonate'),
            disabled: tenant.status === 'suspended',
            onClick: () => onImpersonate(tenant.id),
        },
    ];

    if (tenant.status === 'active') {
        moreItems.push({
            key: 'suspend',
            icon: <PauseCircleOutlined />,
            label: t('tenants.actions.suspend'),
            onClick: () => onSuspend(tenant.id, 'suspended'),
        });
    } else if (tenant.status === 'suspended') {
        moreItems.push({
            key: 'reactivate',
            icon: <PlayCircleOutlined />,
            label: t('tenants.actions.reactivate'),
            onClick: () => onSuspend(tenant.id, 'active'),
        });
    }

    return (
        <Space size="small" wrap>
            <Link href={`/admin/tenants/${tenant.id}`}>
                <Button size="small" icon={<EyeOutlined />}>
                    {t('tenants.actions.view')}
                </Button>
            </Link>
            <Button size="small" danger icon={<DeleteOutlined />} onClick={() => setArchiveOpen(true)}>
                {t('tenants.actions.archive')}
            </Button>
            <Dropdown menu={{ items: moreItems }} trigger={['click']}>
                <Button
                    size="small"
                    icon={<MoreOutlined />}
                    loading={impersonatePending || suspendPending}
                />
            </Dropdown>
            <TenantArchiveConfirmModal
                open={archiveOpen}
                tenantId={tenant.id}
                tenantName={tenant.name}
                onClose={closeArchiveModal}
                onSuccess={() => onArchiveSuccess?.(tenant.id)}
            />
        </Space>
    );
}
