'use client';

import { useState } from 'react';
import {
    Alert,
    Button,
    Card,
    DatePicker,
    Descriptions,
    Form,
    Input,
    Select,
    Space,
    Table,
    Tag,
    Typography,
    message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
    getLicenseStatusDayText,
    getLicenseStatusLabel,
    getLicenseStatusMessage,
    getLicenseStatusTagColor,
    resolveTenantLicenseStatus,
} from '@/features/license/utils/licenseStatus';
import type { AdminTenantDetail } from '@/features/super-admin/api/adminTenants';
import {
    activateAdminTenantTrial,
    checkAdminTenantLicenseConsistency,
    extendAdminTenantLicense,
    getAdminTenantLicense,
    issueAdminTenantDeploymentLicense,
    setAdminTenantLicenseTier,
    type TenantLicenseConsistency,
    type TenantLicenseHistoryItem,
} from '@/features/super-admin/api/adminTenantLicense';
import { useI18n, formatDate, formatDateTime } from '@/i18n';

export type LicenseManagerProps = {
    tenant: AdminTenantDetail;
    onUpdated: () => void;
};

type ExtendFormValues = {
    licenseKey?: string;
    validUntilUtc?: dayjs.Dayjs;
};

export function LicenseManager({ tenant, onUpdated }: LicenseManagerProps) {
    const { t, formatLocale } = useI18n();
    const queryClient = useQueryClient();
    const [extendForm] = Form.useForm<ExtendFormValues>();
    const [consistency, setConsistency] = useState<TenantLicenseConsistency | null>(null);

    const licenseQuery = useQuery({
        queryKey: ['admin', 'tenant-license', tenant.id],
        queryFn: () => getAdminTenantLicense(tenant.id),
    });

    const invalidate = () => {
        void queryClient.invalidateQueries({ queryKey: ['admin', 'tenant-license', tenant.id] });
        onUpdated();
    };

    const trialMutation = useMutation({
        mutationFn: () => activateAdminTenantTrial(tenant.id),
        onSuccess: () => {
            message.success(t('tenants.detail.license.trialActivated'));
            invalidate();
        },
        onError: () => message.error(t('tenants.messages.saveFailed')),
    });

    const extendMutation = useMutation({
        mutationFn: (values: ExtendFormValues) =>
            extendAdminTenantLicense(tenant.id, {
                licenseKey: values.licenseKey?.trim() || null,
                validUntilUtc: values.validUntilUtc ? values.validUntilUtc.utc().toISOString() : null,
            }),
        onSuccess: () => {
            message.success(t('tenants.messages.updated'));
            extendForm.resetFields();
            invalidate();
        },
        onError: () => message.error(t('tenants.messages.saveFailed')),
    });

    const tierMutation = useMutation({
        mutationFn: (tier: 'basic' | 'standard' | 'premium') =>
            setAdminTenantLicenseTier(tenant.id, { tier }),
        onSuccess: () => {
            message.success(t('tenants.detail.license.tierUpdated'));
            invalidate();
        },
        onError: () => message.error(t('tenants.messages.saveFailed')),
    });

    const syncCheckMutation = useMutation({
        mutationFn: () => checkAdminTenantLicenseConsistency(tenant.id),
        onSuccess: (result) => {
            setConsistency(result);
            if (result.isConsistent) {
                message.success(t('tenants.detail.license.consistencyOk'));
            } else {
                message.warning(t('tenants.detail.license.consistencyWarning'));
            }
        },
        onError: () => message.error(t('tenants.messages.saveFailed')),
    });

    const issueJwtMutation = useMutation({
        mutationFn: () => issueAdminTenantDeploymentLicense(tenant.id),
        onSuccess: (result) => {
            if (result.success) {
                message.success(result.message ?? t('tenants.detail.license.jwtIssued'));
                setConsistency(null);
                invalidate();
            } else {
                message.error(result.message ?? t('tenants.messages.saveFailed'));
            }
        },
        onError: () => message.error(t('tenants.messages.saveFailed')),
    });

    const status = licenseQuery.data?.status;
    const history = licenseQuery.data?.history ?? [];
    const resolvedStatus = status ? resolveTenantLicenseStatus(status) : null;

    const historyColumns: ColumnsType<TenantLicenseHistoryItem> = [
        {
            title: t('tenants.detail.license.history.date'),
            dataIndex: 'atUtc',
            key: 'atUtc',
            render: (v: string) => formatDateTime(v, formatLocale),
        },
        {
            title: t('tenants.detail.license.history.event'),
            dataIndex: 'eventType',
            key: 'eventType',
            render: (v: string) => <Tag>{v}</Tag>,
        },
        { title: t('tenants.detail.license.history.summary'), dataIndex: 'summary', key: 'summary' },
        {
            title: t('tenants.detail.license.key'),
            dataIndex: 'licenseKey',
            key: 'licenseKey',
            render: (v: string | null | undefined) =>
                v ? <Typography.Text code>{v}</Typography.Text> : '—',
        },
    ];

    return (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <Card title={t('tenants.detail.license.currentTitle')} loading={licenseQuery.isLoading}>
                {status ? (
                    <Descriptions column={{ xs: 1, sm: 2 }} size="small">
                        <Descriptions.Item label={t('tenants.columns.status')}>
                            <Tag color={getLicenseStatusTagColor(resolvedStatus?.kind ?? 'no_license')}>
                                {getLicenseStatusLabel(resolvedStatus?.kind ?? 'no_license', t)}
                            </Tag>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('tenants.detail.license.type')}>
                            {status.tier
                                ? t(`tenants.detail.license.tiers.${status.tier}`, {
                                      defaultValue: status.tier,
                                  })
                                : '—'}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('tenants.detail.license.validUntil')}>
                            {status.validUntilUtc ? formatDate(status.validUntilUtc, formatLocale) : '—'}
                        </Descriptions.Item>
                        {resolvedStatus ? (
                            <Descriptions.Item label={t('tenants.detail.license.remaining')}>
                                {getLicenseStatusDayText(resolvedStatus, t) ?? '—'}
                            </Descriptions.Item>
                        ) : null}
                        <Descriptions.Item label={t('license.phase.capabilities.write')}>
                            <Tag color={resolvedStatus?.canWrite ? 'green' : 'red'}>
                                {t(resolvedStatus?.canWrite ? 'common.buttons.yes' : 'common.buttons.no')}
                            </Tag>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('license.phase.capabilities.manageUsers')}>
                            <Tag color={resolvedStatus?.canManageUsers ? 'green' : 'red'}>
                                {t(resolvedStatus?.canManageUsers ? 'common.buttons.yes' : 'common.buttons.no')}
                            </Tag>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('license.phase.capabilities.access')}>
                            <Tag color={resolvedStatus?.canAccess ? 'green' : 'red'}>
                                {t(resolvedStatus?.canAccess ? 'common.buttons.yes' : 'common.buttons.no')}
                            </Tag>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('tenants.detail.license.key')}>
                            <Typography.Text code>{status.licenseKey ?? '—'}</Typography.Text>
                        </Descriptions.Item>
                    </Descriptions>
                ) : null}
                {resolvedStatus ? (
                    <Alert
                        style={{ marginTop: 16 }}
                        type={resolvedStatus.kind === 'grace_write' ? 'warning' : resolvedStatus.kind === 'active' ? 'success' : 'error'}
                        showIcon
                        message={getLicenseStatusMessage(resolvedStatus, 'tenant', t)}
                    />
                ) : null}
                {consistency && !consistency.isConsistent ? (
                    <Alert
                        type="warning"
                        showIcon
                        style={{ marginTop: 16 }}
                        message={t('tenants.detail.license.consistencyTitle')}
                        description={
                            <ul style={{ margin: 0, paddingLeft: 20 }}>
                                {consistency.warnings.map((w) => (
                                    <li key={w}>{w}</li>
                                ))}
                            </ul>
                        }
                    />
                ) : null}
                {consistency?.isConsistent ? (
                    <Alert
                        type="success"
                        showIcon
                        style={{ marginTop: 16 }}
                        message={t('tenants.detail.license.consistencyOk')}
                    />
                ) : null}
                <Space wrap style={{ marginTop: 16 }}>
                    <Button
                        loading={syncCheckMutation.isPending}
                        onClick={() => syncCheckMutation.mutate()}
                    >
                        {t('tenants.detail.license.consistencyCheck')}
                    </Button>
                    {consistency?.canIssueDeploymentLicense ? (
                        <Button
                            type="primary"
                            loading={issueJwtMutation.isPending}
                            onClick={() => issueJwtMutation.mutate()}
                        >
                            {t('tenants.detail.license.issueJwt')}
                        </Button>
                    ) : null}
                    <Button loading={trialMutation.isPending} onClick={() => trialMutation.mutate()}>
                        {t('tenants.detail.license.activateTrial')}
                    </Button>
                    <Select
                        placeholder={t('tenants.detail.license.changeTier')}
                        style={{ width: 200 }}
                        onChange={(tier) => tierMutation.mutate(tier)}
                        loading={tierMutation.isPending}
                        options={[
                            { value: 'basic', label: t('tenants.detail.license.tiers.basic') },
                            { value: 'standard', label: t('tenants.detail.license.tiers.standard') },
                            { value: 'premium', label: t('tenants.detail.license.tiers.premium') },
                        ]}
                    />
                    <Link href="/admin/license">
                        <Button>{t('tenants.detail.license.openIssuedLicenses')}</Button>
                    </Link>
                </Space>
            </Card>

            <Card title={t('tenants.detail.license.extendTitle')}>
                <Form form={extendForm} layout="vertical" onFinish={(v) => extendMutation.mutate(v)}>
                    <Form.Item name="licenseKey" label={t('tenants.detail.license.key')}>
                        <Input placeholder="REGK-…" />
                    </Form.Item>
                    <Form.Item name="validUntilUtc" label={t('tenants.detail.license.validUntil')}>
                        <DatePicker style={{ width: '100%' }} format="DD.MM.YYYY" />
                    </Form.Item>
                    <Typography.Paragraph type="secondary">
                        {t('tenants.detail.license.extendHint')}
                    </Typography.Paragraph>
                    <Button type="primary" htmlType="submit" loading={extendMutation.isPending}>
                        {t('tenants.detail.license.extend')}
                    </Button>
                </Form>
            </Card>

            <Card title={t('tenants.detail.license.historyTitle')}>
                <Table
                    rowKey={(row) => `${row.atUtc}-${row.eventType}-${row.licenseKey ?? ''}`}
                    loading={licenseQuery.isLoading}
                    dataSource={history}
                    columns={historyColumns}
                    locale={{ emptyText: t('tenants.detail.license.historyEmpty') }}
                    pagination={{ pageSize: 10 }}
                />
            </Card>
        </Space>
    );
}
