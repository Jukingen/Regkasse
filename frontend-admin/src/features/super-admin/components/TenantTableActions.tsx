'use client';

import React, { useCallback, useState } from 'react';
import {
    Button,
    Dropdown,
    Input,
    Modal,
    Popconfirm,
    Space,
    Typography,
} from 'antd';
import type { MenuProps } from 'antd';
import {
    DeleteOutlined,
    EyeOutlined,
    LoginOutlined,
    MoreOutlined,
    PauseCircleOutlined,
    PlayCircleOutlined,
    KeyOutlined,
    TeamOutlined,
    UndoOutlined,
    WarningOutlined,
} from '@ant-design/icons';
import Link from 'next/link';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { buildAdminUsersPageHref } from '@/features/users/utils/adminUsersPageUrl';
import { useI18n } from '@/i18n';

export type TenantTableActionsProps = {
    tenant: AdminTenantListItem;
    softDeletePending?: boolean;
    restorePending?: boolean;
    hardDeletePending?: boolean;
    impersonatePending?: boolean;
    suspendPending?: boolean;
    onEdit: (tenant: AdminTenantListItem) => void;
    onSuspend: (id: string, status: string) => void;
    onImpersonate: (id: string) => void;
    onSoftDelete: (id: string) => void;
    onRestore: (id: string) => void;
    onHardDelete: (id: string, confirmSlug: string) => void | Promise<void>;
};

export function TenantTableActions({
    tenant,
    softDeletePending,
    restorePending,
    hardDeletePending,
    impersonatePending,
    suspendPending,
    onEdit,
    onSuspend,
    onImpersonate,
    onSoftDelete,
    onRestore,
    onHardDelete,
}: TenantTableActionsProps) {
    const { t } = useI18n();
    const [hardDeleteOpen, setHardDeleteOpen] = useState(false);
    const [confirmSlug, setConfirmSlug] = useState('');

    const slugMatches =
        confirmSlug.trim().toLowerCase() === tenant.slug.trim().toLowerCase();

    const closeHardDeleteModal = useCallback(() => {
        setHardDeleteOpen(false);
        setConfirmSlug('');
    }, []);

    const openHardDeleteModal = useCallback(() => {
        setConfirmSlug('');
        setHardDeleteOpen(true);
    }, []);

    if (tenant.status === 'deleted') {
        return (
            <Space size="small" wrap>
                <Link href={`/admin/tenants/${tenant.id}`}>
                    <Button size="small" icon={<EyeOutlined />}>
                        {t('tenants.actions.view')}
                    </Button>
                </Link>
                <Popconfirm
                    title={t('tenants.confirmRestore.title')}
                    description={t('tenants.confirmRestore.body')}
                    onConfirm={() => onRestore(tenant.id)}
                    okText={t('common.yes', { defaultValue: 'Ja' })}
                    cancelText={t('common.no', { defaultValue: 'Nein' })}
                >
                    <Button
                        size="small"
                        icon={<UndoOutlined />}
                        loading={restorePending}
                    >
                        {t('tenants.actions.restore')}
                    </Button>
                </Popconfirm>
                <Button
                    size="small"
                    danger
                    icon={<DeleteOutlined />}
                    loading={hardDeletePending}
                    onClick={openHardDeleteModal}
                >
                    {t('tenants.actions.hardDelete')}
                </Button>
                <Modal
                    title={
                        <Space>
                            <WarningOutlined style={{ color: '#cf1322' }} />
                            {t('tenants.confirmHardDelete.title')}
                        </Space>
                    }
                    open={hardDeleteOpen}
                    onCancel={closeHardDeleteModal}
                    okText={t('tenants.actions.hardDelete')}
                    okButtonProps={{
                        danger: true,
                        disabled: !slugMatches,
                        loading: hardDeletePending,
                    }}
                    cancelText={t('common.cancel', { defaultValue: 'Abbrechen' })}
                    onOk={async () => {
                        try {
                            await onHardDelete(tenant.id, confirmSlug.trim());
                            closeHardDeleteModal();
                        } catch {
                            /* keep modal open; parent shows error toast */
                        }
                    }}
                    destroyOnClose
                >
                    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                        <Typography.Text strong type="danger">
                            {t('tenants.confirmHardDelete.irreversible')}
                        </Typography.Text>
                        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                            {t('tenants.confirmHardDelete.dataLoss', { name: tenant.name })}
                        </Typography.Paragraph>
                        <Typography.Text>
                            {t('tenants.confirmHardDelete.confirmSlugLabel', { slug: tenant.slug })}
                        </Typography.Text>
                        <Input
                            value={confirmSlug}
                            onChange={(e) => setConfirmSlug(e.target.value)}
                            placeholder={tenant.slug}
                            autoComplete="off"
                            status={confirmSlug.length > 0 && !slugMatches ? 'error' : undefined}
                        />
                    </Space>
                </Modal>
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
            <Popconfirm
                title={t('tenants.confirmDelete.title')}
                description={t('tenants.confirmDelete.body')}
                onConfirm={() => onSoftDelete(tenant.id)}
                okText={t('tenants.actions.delete')}
                okButtonProps={{ danger: true, loading: softDeletePending }}
                cancelText={t('common.cancel', { defaultValue: 'Abbrechen' })}
            >
                <Button size="small" danger icon={<DeleteOutlined />} loading={softDeletePending}>
                    {t('tenants.actions.delete')}
                </Button>
            </Popconfirm>
            <Dropdown menu={{ items: moreItems }} trigger={['click']}>
                <Button
                    size="small"
                    icon={<MoreOutlined />}
                    loading={impersonatePending || suspendPending}
                />
            </Dropdown>
        </Space>
    );
}
