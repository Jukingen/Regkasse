'use client';

/**
 * Super-admin tenant management (SuperAdmin role / system.critical).
 */
import React, { useCallback, useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    Form,
    Input,
    Modal,
    Popconfirm,
    Select,
    Space,
    Table,
    Tag,
    Typography,
    message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, ReloadOutlined, LoginOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';
import { CreateTenantModal } from '@/features/super-admin/components/CreateTenantModal';
import { ImpersonationRedirectOverlay } from '@/features/super-admin/components/ImpersonationRedirectOverlay';
import {
    applyTenantImpersonationSession,
    deleteAdminTenant,
    impersonateAdminTenant,
    listAdminTenants,
    updateAdminTenant,
    type AdminTenantListItem,
} from '@/features/super-admin/api/adminTenants';

const TENANT_QUERY_KEY = ['admin', 'tenants'] as const;

type TenantFormValues = {
    name: string;
    slug: string;
    email?: string;
    phone?: string;
    address?: string;
    status?: string;
};

function statusColor(status: string): string {
    if (status === 'active') return 'green';
    if (status === 'suspended') return 'orange';
    if (status === 'deleted') return 'red';
    return 'default';
}

export default function SuperAdminTenantsPage() {
    const { t } = useI18n();
    const { user } = useAuth();
    const queryClient = useQueryClient();
    const [includeDeleted, setIncludeDeleted] = useState(false);
    const [createOpen, setCreateOpen] = useState(false);
    const [editRow, setEditRow] = useState<AdminTenantListItem | null>(null);
    const [editForm] = Form.useForm<TenantFormValues>();
    const [impersonationRedirecting, setImpersonationRedirecting] = useState(false);

    const canAccess =
        isSuperAdmin(user?.role) || hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);

    const tenantsQuery = useQuery({
        queryKey: [...TENANT_QUERY_KEY, includeDeleted],
        queryFn: () => listAdminTenants(includeDeleted),
        enabled: canAccess,
    });

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
            void queryClient.invalidateQueries({ queryKey: TENANT_QUERY_KEY });
        },
        onError: () => message.error(t('tenants.messages.saveFailed')),
    });

    const deleteMutation = useMutation({
        mutationFn: (id: string) => deleteAdminTenant(id),
        onSuccess: () => {
            message.success(t('tenants.messages.deleted'));
            void queryClient.invalidateQueries({ queryKey: TENANT_QUERY_KEY });
        },
        onError: () => message.error(t('tenants.messages.deleteFailed')),
    });

    const impersonateMutation = useMutation({
        mutationFn: (id: string) => impersonateAdminTenant(id),
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
            { title: t('tenants.columns.name'), dataIndex: 'name', key: 'name' },
            { title: t('tenants.columns.slug'), dataIndex: 'slug', key: 'slug' },
            { title: t('tenants.columns.email'), dataIndex: 'email', key: 'email', render: (v) => v ?? '—' },
            {
                title: t('tenants.columns.status'),
                dataIndex: 'status',
                key: 'status',
                render: (status: string) => <Tag color={statusColor(status)}>{status}</Tag>,
            },
            {
                title: t('tenants.columns.actions'),
                key: 'actions',
                render: (_, row) => (
                    <Space size="small">
                        <Button size="small" onClick={() => openEdit(row)}>
                            {t('tenants.actions.edit')}
                        </Button>
                        {row.status !== 'deleted' && (
                            <>
                                <Button
                                    size="small"
                                    icon={<LoginOutlined />}
                                    loading={impersonateMutation.isPending}
                                    onClick={() => impersonateMutation.mutate(row.id)}
                                >
                                    {t('tenants.actions.impersonate')}
                                </Button>
                                <Popconfirm
                                    title={t('tenants.confirmDelete.title')}
                                    description={t('tenants.confirmDelete.body')}
                                    onConfirm={() => deleteMutation.mutate(row.id)}
                                >
                                    <Button size="small" danger loading={deleteMutation.isPending}>
                                        {t('tenants.actions.delete')}
                                    </Button>
                                </Popconfirm>
                            </>
                        )}
                    </Space>
                ),
            },
        ],
        [t, openEdit, deleteMutation.isPending, impersonateMutation.isPending, impersonateMutation],
    );

    if (!canAccess) {
        return (
            <AdminPageShell>
                <Alert type="error" message={t('tenants.accessDenied.title')} description={t('tenants.accessDenied.body')} />
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
                        <Button
                            icon={<ReloadOutlined />}
                            onClick={() => void queryClient.invalidateQueries({ queryKey: TENANT_QUERY_KEY })}
                        >
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
                <Space style={{ marginBottom: 16 }}>
                    <span>{t('tenants.filters.includeDeleted')}</span>
                    <Select
                        value={includeDeleted ? 'yes' : 'no'}
                        style={{ width: 120 }}
                        onChange={(v) => setIncludeDeleted(v === 'yes')}
                        options={[
                            { value: 'no', label: t('common.no', { defaultValue: 'Nein' }) },
                            { value: 'yes', label: t('common.yes', { defaultValue: 'Ja' }) },
                        ]}
                    />
                </Space>
                <Table
                    rowKey="id"
                    loading={tenantsQuery.isLoading}
                    dataSource={tenantsQuery.data ?? []}
                    columns={columns}
                    pagination={{ pageSize: 20 }}
                />
            </Card>

            <CreateTenantModal
                open={createOpen}
                onClose={() => setCreateOpen(false)}
                onCreated={() => void queryClient.invalidateQueries({ queryKey: TENANT_QUERY_KEY })}
            />

            <Modal
                title={t('tenants.edit.title')}
                open={!!editRow}
                onCancel={() => setEditRow(null)}
                onOk={() => editForm.submit()}
                confirmLoading={updateMutation.isPending}
                destroyOnClose
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
                                { value: 'active', label: 'active' },
                                { value: 'suspended', label: 'suspended' },
                            ]}
                        />
                    </Form.Item>
                </Form>
            </Modal>
        </AdminPageShell>
    );
}
