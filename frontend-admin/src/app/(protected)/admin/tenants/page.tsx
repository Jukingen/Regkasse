'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Super-admin tenant management (SuperAdmin role / system.critical).
 */
import React, { useCallback, useMemo, useState } from 'react';
import { Modal, Alert, Button, Card, Form, Input, Select, Space, Switch, Empty, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { useI18n, formatDate } from '@/i18n';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';
import { useCanManageTenantDeletion } from '@/features/super-admin/hooks/useCanManageTenantDeletion';
import { CreateTenantWizard } from '@/features/super-admin/components/CreateTenantWizard';
import { ImpersonationRedirectOverlay } from '@/features/super-admin/components/ImpersonationRedirectOverlay';
import { TenantLicenseBadge } from '@/features/super-admin/components/TenantLicenseBadge';
import { TenantStatusBadge } from '@/features/super-admin/components/TenantStatusBadge';
import { TenantTableActions } from '@/features/super-admin/components/TenantTableActions';
import {
    applyTenantImpersonationSession,
    hardDeleteAdminTenant,
    impersonateAdminTenant,
    listAdminTenants,
    restoreAdminTenant,
    softDeleteAdminTenant,
    updateAdminTenant,
    type AdminTenantListItem,
} from '@/features/super-admin/api/adminTenants';
import Link from 'next/link';
import { adminTableScrollXy, shouldUseAdminTableVirtual } from '@/components/ui/adminTableVirtual';

const TENANT_QUERY_KEY = ['admin', 'tenants'] as const;

function isTenantRowDeleted(row: Pick<AdminTenantListItem, 'status' | 'isActive'>): boolean {
    return row.status === 'deleted' || !row.isActive;
}

type TenantFormValues = {
    name: string;
    slug: string;
    email?: string;
    phone?: string;
    address?: string;
    status?: string;
};

export default function SuperAdminTenantsPage() {
  const { message } = useAntdApp();

    const { t, formatLocale } = useI18n();
    const { user } = useAuth();
    const queryClient = useQueryClient();
    const [includeDeleted, setIncludeDeleted] = useState(false);
    const [createOpen, setCreateOpen] = useState(false);
    const [editRow, setEditRow] = useState<AdminTenantListItem | null>(null);
    const [editForm] = Form.useForm<TenantFormValues>();
    const [impersonationRedirecting, setImpersonationRedirecting] = useState(false);
    const [actionTenantId, setActionTenantId] = useState<string | null>(null);

    const canAccess =
        isSuperAdmin(user?.role) || hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);
    const isSuperAdminUser = isSuperAdmin(user?.role);
    const canManageDeletion = useCanManageTenantDeletion();

    const tenantsQuery = useQuery({
        queryKey: [...TENANT_QUERY_KEY, includeDeleted],
        queryFn: () => listAdminTenants(includeDeleted),
        enabled: canAccess,
    });

    const invalidateTenants = useCallback(
        () => void queryClient.invalidateQueries({ queryKey: TENANT_QUERY_KEY }),
        [queryClient],
    );

    const updateMutation = useMutation({
        mutationFn: ({ id, body }: { id: string; body: TenantFormValues }) =>
            updateAdminTenant(id, {
                name: body.name,
                email: body.email,
                phone: body.phone,
                address: body.address,
                status: body.status,
            }),
        onSuccess: () => {
            message.success(t('tenants.messages.updated'));
            setEditRow(null);
            invalidateTenants();
        },
        onError: () => message.error(t('tenants.messages.saveFailed')),
    });

    const suspendMutation = useMutation({
        mutationFn: ({ id, status }: { id: string; status: string }) =>
            updateAdminTenant(id, { status }),
        onMutate: ({ id }) => setActionTenantId(id),
        onSettled: () => setActionTenantId(null),
        onSuccess: () => {
            message.success(t('tenants.messages.updated'));
            invalidateTenants();
        },
        onError: () => message.error(t('tenants.messages.saveFailed')),
    });

    const deleteMutation = useMutation({
        mutationFn: (id: string) => softDeleteAdminTenant(id),
        onMutate: (id) => setActionTenantId(id),
        onSettled: () => setActionTenantId(null),
        onSuccess: () => {
            message.success(t('tenants.messages.deleted'));
            invalidateTenants();
        },
        onError: () => message.error(t('tenants.messages.deleteFailed')),
    });

    const restoreMutation = useMutation({
        mutationFn: (id: string) => restoreAdminTenant(id),
        onMutate: (id) => setActionTenantId(id),
        onSettled: () => setActionTenantId(null),
        onSuccess: () => {
            message.success(t('tenants.messages.restored'));
            invalidateTenants();
        },
        onError: () => message.error(t('tenants.messages.restoreFailed')),
    });

    const hardDeleteMutation = useMutation({
        mutationFn: ({ id, confirmSlug }: { id: string; confirmSlug: string }) =>
            hardDeleteAdminTenant(id, confirmSlug),
        onMutate: ({ id }) => setActionTenantId(id),
        onSettled: () => setActionTenantId(null),
        onSuccess: () => {
            message.success(t('tenants.messages.hardDeleted'));
            invalidateTenants();
        },
        onError: () => message.error(t('tenants.messages.hardDeleteFailed')),
    });

    const impersonateMutation = useMutation({
        mutationFn: (id: string) => impersonateAdminTenant(id),
        onMutate: (id) => setActionTenantId(id),
        onSettled: () => setActionTenantId(null),
        onSuccess: (res) => {
            setImpersonationRedirecting(true);
            applyTenantImpersonationSession(res);
        },
        onError: () => message.error(t('tenants.messages.impersonationFailed')),
    });

    const openEdit = useCallback(
        (row: AdminTenantListItem) => {
            setEditRow(row);
            editForm.setFieldsValue({
                name: row.name,
                slug: row.slug,
                email: row.email ?? undefined,
                phone: row.phone ?? undefined,
                status: row.status,
            });
        },
        [editForm],
    );

    const columns: ColumnsType<AdminTenantListItem> = useMemo(
        () => [
            {
                title: t('tenants.columns.name'),
                dataIndex: 'name',
                key: 'name',
                render: (name: string, row) => {
                    const deleted = isTenantRowDeleted(row);
                    return (
                        <Link
                            href={`/admin/tenants/${row.id}`}
                            className={deleted ? 'tenant-deleted-name' : undefined}
                        >
                            {name}
                        </Link>
                    );
                },
            },
            { title: t('tenants.columns.slug'), dataIndex: 'slug', key: 'slug' },
            {
                title: t('tenants.columns.status'),
                dataIndex: 'status',
                key: 'status',
                render: (status: string) => <TenantStatusBadge status={status} />,
            },
            {
                title: t('tenants.columns.adminUser'),
                dataIndex: 'ownerAdminEmail',
                key: 'ownerAdminEmail',
                render: (v: string | null | undefined) => v ?? '—',
            },
            {
                title: t('tenants.columns.license'),
                key: 'license',
                render: (_, record) => (
                    <TenantLicenseBadge
                        licenseValidUntilUtc={record.licenseValidUntilUtc}
                        licenseKey={record.licenseKey}
                    />
                ),
            },
            {
                title: t('tenants.columns.created'),
                dataIndex: 'createdAt',
                key: 'createdAt',
                render: (v: string) => formatDate(v, formatLocale),
            },
            {
                title: t('tenants.columns.actions'),
                key: 'actions',
                fixed: 'right',
                width: 320,
                render: (_, row) =>
                    canManageDeletion ? (
                        <TenantTableActions
                            tenant={row}
                            softDeletePending={deleteMutation.isPending && actionTenantId === row.id}
                            restorePending={restoreMutation.isPending && actionTenantId === row.id}
                            hardDeletePending={hardDeleteMutation.isPending && actionTenantId === row.id}
                            impersonatePending={
                                impersonateMutation.isPending && actionTenantId === row.id
                            }
                            suspendPending={suspendMutation.isPending && actionTenantId === row.id}
                            onEdit={openEdit}
                            onSuspend={(id, status) => suspendMutation.mutate({ id, status })}
                            onImpersonate={(id) => impersonateMutation.mutate(id)}
                            onSoftDelete={(id) => deleteMutation.mutate(id)}
                            onRestore={(id) => restoreMutation.mutate(id)}
                            onHardDelete={async (id, confirmSlug) => {
                                await hardDeleteMutation.mutateAsync({ id, confirmSlug });
                            }}
                        />
                    ) : (
                        <Typography.Text type="secondary">—</Typography.Text>
                    ),
            },
        ],
        [
            t,
            formatLocale,
            openEdit,
            deleteMutation.isPending,
            restoreMutation.isPending,
            hardDeleteMutation,
            impersonateMutation.isPending,
            suspendMutation.isPending,
            actionTenantId,
            canManageDeletion,
        ],
    );

    const tenantRows = tenantsQuery.data ?? [];

    if (!canAccess) {
        return (
            <AdminPageShell>
                <Alert type="error" title={t('tenants.accessDenied.title')} description={t('tenants.accessDenied.body')} />
            </AdminPageShell>
        );
    }

    return (
        <AdminPageShell>
            {impersonationRedirecting ? <ImpersonationRedirectOverlay /> : null}
            <AdminPageHeader
                title={t('tenants.page.title')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t(ADMIN_NAV_LABEL_KEYS.settingsHub), href: '/settings' },
                    { title: t('tenants.page.title'), href: '/admin/tenants' },
                ]}
                actions={
                    <Space>
                        <Button icon={<ReloadOutlined />} onClick={invalidateTenants}>
                            {t('common.refresh')}
                        </Button>
                        <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
                            {t('tenants.actions.create')}
                        </Button>
                    </Space>
                }
            />

            <Typography.Paragraph type="secondary">{t('tenants.page.subtitle')}</Typography.Paragraph>
            <Typography.Paragraph type="secondary" style={{ marginTop: -8 }}>
                {t('tenants.page.listHelp')}
            </Typography.Paragraph>

            <Card>
                <div
                    style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        flexWrap: 'wrap',
                        gap: 12,
                        marginBottom: 16,
                    }}
                >
                    <Typography.Text strong>{t('tenants.page.title')}</Typography.Text>
                    {isSuperAdminUser ? (
                        <Space>
                            <span>{t('tenants.filters.includeDeleted')}</span>
                            <Switch
                                checked={includeDeleted}
                                onChange={setIncludeDeleted}
                                aria-label={t('tenants.filters.includeDeleted')}
                            />
                        </Space>
                    ) : null}
                </div>
                <Table
                    rowKey="id"
                    loading={tenantsQuery.isLoading}
                    dataSource={tenantRows}
                    columns={columns}
                    rowClassName={(record) =>
                        isTenantRowDeleted(record) ? 'tenant-row-deleted' : ''
                    }
                    locale={{
                        emptyText: (
                            <Empty
                                image={Empty.PRESENTED_IMAGE_SIMPLE}
                                description={t('tenants.page.empty')}
                            />
                        ),
                    }}
                    virtual={shouldUseAdminTableVirtual(tenantRows.length)}
                    scroll={adminTableScrollXy(1320, tenantRows.length)}
                    pagination={{
                        pageSize: 20,
                        showSizeChanger: true,
                        pageSizeOptions: [10, 20, 50, 100],
                    }}
                />
            </Card>

            <CreateTenantWizard
                open={createOpen}
                onClose={() => setCreateOpen(false)}
                onCreated={invalidateTenants}
                onCreateAnother={() => setCreateOpen(true)}
                onSwitchToTenant={(tenantId) => impersonateMutation.mutate(tenantId)}
                switchToTenantLoading={impersonateMutation.isPending}
            />

            <Modal
                title={t('tenants.edit.title')}
                open={!!editRow}
                onCancel={() => setEditRow(null)}
                onOk={() => editForm.submit()}
                confirmLoading={updateMutation.isPending}
                destroyOnHidden
            >
                <Form
                    form={editForm}
                    layout="vertical"
                    onFinish={(values) => editRow && updateMutation.mutate({ id: editRow.id, body: values })}
                >
                    <Form.Item name="name" label={t('tenants.fields.name')} rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                    <Form.Item label={t('tenants.fields.slug')}>
                        <Input value={editRow?.slug} disabled />
                    </Form.Item>
                    <Form.Item name="email" label={t('tenants.fields.email')}>
                        <Input type="email" />
                    </Form.Item>
                    <Form.Item name="phone" label={t('tenants.fields.phone')}>
                        <Input />
                    </Form.Item>
                    <Form.Item name="address" label={t('tenants.fields.address')}>
                        <Input.TextArea rows={2} />
                    </Form.Item>
                    <Form.Item name="status" label={t('tenants.fields.status')}>
                        <Select
                            options={[
                                { value: 'active', label: t('tenants.status.active') },
                                { value: 'suspended', label: t('tenants.status.suspended') },
                            ]}
                        />
                    </Form.Item>
                </Form>
            </Modal>
        </AdminPageShell>
    );
}
