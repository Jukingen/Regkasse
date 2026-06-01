'use client';

import { Button, Card, Descriptions, Popconfirm, Space, Tag, Typography } from 'antd';
import Link from 'next/link';

import type { AdminTenantDetail } from '@/features/super-admin/api/adminTenants';
import { TenantLicenseBadge } from '@/features/super-admin/components/TenantLicenseBadge';
import { tenantStatusColor } from '@/features/super-admin/utils/tenantStatusLabel';
import { buildAdminUsersPageHref } from '@/features/users/utils/adminUsersPageUrl';
import { useI18n, formatDate, formatDateTime } from '@/i18n';

export type TenantDetailOverviewTabProps = {
    tenant: AdminTenantDetail;
    suspendPending?: boolean;
    onSuspend: () => void;
    onReactivate: () => void;
};

export function TenantDetailOverviewTab({
    tenant,
    suspendPending,
    onSuspend,
    onReactivate,
}: TenantDetailOverviewTabProps) {
    const { t, formatLocale } = useI18n();
    return (
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
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
                        <TenantLicenseBadge
                            tenantId={tenant.id}
                            licenseValidUntilUtc={tenant.licenseValidUntilUtc}
                            licenseKey={tenant.licenseKey}
                            licenseDaysRemaining={tenant.licenseDaysRemaining}
                        />
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
                        <Link href={buildAdminUsersPageHref(tenant.id)}>
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
                <Typography.Paragraph type="secondary">
                    {t('tenants.detail.settings.danger.deletedSettingsHint')}
                </Typography.Paragraph>
            ) : null}
        </Space>
    );
}
