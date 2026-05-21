'use client';

import { useState } from 'react';
import { Alert, Button, Card, Descriptions, Input, Popconfirm, Space, Tag, Typography } from 'antd';
import Link from 'next/link';

import type { AdminTenantDetail } from '@/features/super-admin/api/adminTenants';
import { resolveTenantLicenseLabel } from '@/features/super-admin/utils/tenantLicenseLabel';
import { tenantStatusColor } from '@/features/super-admin/utils/tenantStatusLabel';
import { useI18n, formatDate, formatDateTime } from '@/i18n';

export type TenantDetailOverviewTabProps = {
    tenant: AdminTenantDetail;
    suspendPending?: boolean;
    deletePending?: boolean;
    hardDeletePending?: boolean;
    onSuspend: () => void;
    onReactivate: () => void;
    onDelete: () => void;
    onHardDelete: (confirmSlug: string) => void;
};

export function TenantDetailOverviewTab({
    tenant,
    suspendPending,
    deletePending,
    hardDeletePending,
    onSuspend,
    onReactivate,
    onDelete,
    onHardDelete,
}: TenantDetailOverviewTabProps) {
    const { t, formatLocale } = useI18n();
    const [confirmSlug, setConfirmSlug] = useState('');
    const license = resolveTenantLicenseLabel(tenant.licenseValidUntilUtc, tenant.licenseKey);
    const slugMatches = confirmSlug.trim().toLowerCase() === tenant.slug.toLowerCase();

    return (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <Card title={t('tenants.detail.overview.statusCard')}>
                <Descriptions column={{ xs: 1, sm: 2 }} size="small">
                    <Descriptions.Item label={t('tenants.columns.status')}>
                        <Tag color={tenantStatusColor(tenant.status)}>{tenant.status}</Tag>
                    </Descriptions.Item>
                    <Descriptions.Item label={t('tenants.detail.overview.created')}>
                        {formatDate(tenant.createdAt, formatLocale)}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('tenants.detail.overview.lastActivity')}>
                        {tenant.lastActivityAtUtc ? formatDateTime(tenant.lastActivityAtUtc, formatLocale) : '—'}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('tenants.columns.adminUser')}>
                        {tenant.ownerAdminEmail ?? '—'}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('tenants.columns.license')}>
                        <Tag
                            color={
                                license.kind === 'expired'
                                    ? 'red'
                                    : license.kind === 'trial'
                                      ? 'blue'
                                      : license.kind === 'valid'
                                        ? 'green'
                                        : 'default'
                            }
                        >
                            {license.label}
                        </Tag>
                    </Descriptions.Item>
                </Descriptions>
                {tenant.status !== 'deleted' ? (
                    <Space wrap style={{ marginTop: 16 }}>
                        {tenant.status === 'active' ? (
                            <Popconfirm
                                title={t('tenants.detail.overview.confirmSuspend.title')}
                                description={t('tenants.detail.overview.confirmSuspend.body')}
                                onConfirm={onSuspend}
                            >
                                <Button loading={suspendPending}>{t('tenants.actions.suspend')}</Button>
                            </Popconfirm>
                        ) : (
                            <Button loading={suspendPending} onClick={onReactivate}>
                                {t('tenants.actions.reactivate')}
                            </Button>
                        )}
                        <Popconfirm
                            title={t('tenants.confirmDelete.title')}
                            description={t('tenants.confirmDelete.body')}
                            onConfirm={onDelete}
                        >
                            <Button danger loading={deletePending}>
                                {t('tenants.actions.delete')}
                            </Button>
                        </Popconfirm>
                        <Link href={`/admin/tenants/${tenant.id}?tab=users`}>
                            <Button>{t('tenants.detail.overview.manageUsers')}</Button>
                        </Link>
                        <Link href={`/admin/tenants/${tenant.id}?tab=license`}>
                            <Button>{t('tenants.actions.manageLicense')}</Button>
                        </Link>
                    </Space>
                ) : null}
            </Card>

            <Card title={t('tenants.detail.overview.statsTitle')}>
                <Descriptions column={{ xs: 1, sm: 3 }} size="small">
                    <Descriptions.Item label={t('tenants.detail.tabs.users')}>
                        {tenant.activeUserCount ?? 0}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('tenants.detail.tabs.registers')}>
                        {tenant.cashRegisterCount ?? 0}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('tenants.fields.slug')}>
                        <Typography.Text code>{tenant.slug}</Typography.Text>
                    </Descriptions.Item>
                </Descriptions>
            </Card>

            {tenant.status === 'deleted' ? (
                <Card title={t('tenants.detail.danger.title')} type="inner">
                    <Alert type="error" showIcon message={t('tenants.detail.danger.hint')} style={{ marginBottom: 16 }} />
                    <Typography.Paragraph>
                        {t('tenants.detail.danger.confirmLabel', { slug: tenant.slug })}
                    </Typography.Paragraph>
                    <Input
                        value={confirmSlug}
                        onChange={(e) => setConfirmSlug(e.target.value)}
                        placeholder={tenant.slug}
                        style={{ maxWidth: 320, marginBottom: 12 }}
                    />
                    <Popconfirm
                        title={t('tenants.detail.danger.confirmTitle')}
                        description={t('tenants.detail.danger.confirmBody')}
                        disabled={!slugMatches}
                        onConfirm={() => onHardDelete(confirmSlug.trim())}
                    >
                        <Button danger disabled={!slugMatches} loading={hardDeletePending}>
                            {t('tenants.detail.danger.hardDelete')}
                        </Button>
                    </Popconfirm>
                </Card>
            ) : null}
        </Space>
    );
}
