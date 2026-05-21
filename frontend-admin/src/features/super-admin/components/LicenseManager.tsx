'use client';

import { Button, Card, DatePicker, Descriptions, Form, Input, Select, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import type { AdminTenantDetail } from '@/features/super-admin/api/adminTenants';
import {
    activateAdminTenantTrial,
    extendAdminTenantLicense,
    getAdminTenantLicense,
    setAdminTenantLicenseTier,
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
    const { t } = useI18n();
    const queryClient = useQueryClient();
    const [extendForm] = Form.useForm<ExtendFormValues>();

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

    const status = licenseQuery.data?.status;
    const history = licenseQuery.data?.history ?? [];

    const historyColumns: ColumnsType<TenantLicenseHistoryItem> = [
        {
            title: t('tenants.detail.license.history.date'),
            dataIndex: 'atUtc',
            key: 'atUtc',
            render: (v: string) => formatDateTime(v),
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

    const kindColor =
        status?.kind === 'expired'
            ? 'red'
            : status?.kind === 'trial'
              ? 'blue'
              : status?.kind === 'active'
                ? 'green'
                : 'default';

    return (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <Card title={t('tenants.detail.license.currentTitle')} loading={licenseQuery.isLoading}>
                {status ? (
                    <Descriptions column={{ xs: 1, sm: 2 }} size="small">
                        <Descriptions.Item label={t('tenants.columns.status')}>
                            <Tag color={kindColor}>{status.kind}</Tag>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('tenants.detail.license.type')}>
                            {status.tier
                                ? t(`tenants.detail.license.tiers.${status.tier}`, {
                                      defaultValue: status.tier,
                                  })
                                : '—'}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('tenants.detail.license.validUntil')}>
                            {status.validUntilUtc ? formatDate(status.validUntilUtc) : '—'}
                        </Descriptions.Item>
                        {status.daysRemaining != null ? (
                            <Descriptions.Item label={t('tenants.detail.license.remaining')}>
                                {t('tenants.detail.license.remainingDays', {
                                    count: status.daysRemaining,
                                })}
                            </Descriptions.Item>
                        ) : null}
                        <Descriptions.Item label={t('tenants.detail.license.key')}>
                            <Typography.Text code>{status.licenseKey ?? '—'}</Typography.Text>
                        </Descriptions.Item>
                    </Descriptions>
                ) : null}
                <Space wrap style={{ marginTop: 16 }}>
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
