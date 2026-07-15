'use client';

import { useMemo } from 'react';
import { Alert, Button, Card, DatePicker, Descriptions, Form, Select, Space, Spin, Tag } from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { useQuery } from '@tanstack/react-query';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { isDevelopment } from '@/features/auth/services/devTenant';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import {
    fetchLicenseTestSnapshot,
    licenseTestQueryKey,
} from '@/features/license/api/licenseTest';
import {
    LICENSE_TEST_MOCK_SCENARIOS,
    licenseTestScenarioFromDays,
    licenseTestScenarioButtonColor,
    licenseTestScenarioLabelKey,
    licenseTestStatusTagColor,
} from '@/features/license/constants/licenseTestScenarios';
import { useUpdateLicenseTest } from '@/features/license/hooks/useUpdateLicenseTest';

type LicenseTestFormValues = {
    tenantId: string;
    validUntil: Dayjs;
};

export function LicenseTestPanel() {
    const { t } = useI18n();
    const { message } = useAntdApp();
    const [form] = Form.useForm<LicenseTestFormValues>();
    const { updateMutation, scenarioMutation, isPending } = useUpdateLicenseTest();
    const tenantId = Form.useWatch('tenantId', form);

    const tenantsQuery = useQuery({
        queryKey: ['admin', 'tenants', false],
        queryFn: () => listAdminTenants(false),
        enabled: isDevelopment(),
    });

    const snapshotQuery = useQuery({
        queryKey: licenseTestQueryKey(tenantId),
        queryFn: () => fetchLicenseTestSnapshot(tenantId),
        enabled: isDevelopment() && Boolean(tenantId),
    });

    const tenantOptions = useMemo(
        () =>
            (tenantsQuery.data ?? [])
                .filter((row) => row.status === 'active')
                .map((row) => ({
                    value: row.id,
                    label: `${row.name} (${row.slug})`,
                })),
        [tenantsQuery.data],
    );

    const scenarios = LICENSE_TEST_MOCK_SCENARIOS;

    const requireTenantId = (): string | null => {
        const id = form.getFieldValue('tenantId') as string | undefined;
        if (!id) {
            message.warning(t('license.testPanel.noTenantSelected'));
            return null;
        }
        return id;
    };

    const handleScenario = async (days: number) => {
        const id = requireTenantId();
        if (!id) return;

        const scenario = licenseTestScenarioFromDays(days);
        if (!scenario) return;

        try {
            await scenarioMutation.mutateAsync({ tenantId: id, scenario });
        } catch {
            // Toast handled in useUpdateLicenseTest.onError
        }
    };

    const handleSubmit = async (values: LicenseTestFormValues) => {
        try {
            await updateMutation.mutateAsync({
                tenantId: values.tenantId,
                validUntil: values.validUntil.toISOString(),
            });
        } catch {
            // Toast handled in useUpdateLicenseTest.onError
        }
    };

    const tenant = snapshotQuery.data?.tenant;
    const deployment = snapshotQuery.data?.deployment;

    return (
        <Form
            form={form}
            layout="vertical"
            initialValues={{ validUntil: dayjs().add(30, 'day') }}
            onFinish={handleSubmit}
        >
            <Card title={t('license.testPanel.selectTenant')} style={{ marginBottom: 16 }}>
                <Form.Item
                    name="tenantId"
                    label={t('license.testPanel.selectTenant')}
                    rules={[{ required: true, message: t('license.testPanel.noTenantSelected') }]}
                >
                    <Select
                        showSearch
                        optionFilterProp="label"
                        placeholder={t('license.testPanel.selectTenantPlaceholder')}
                        loading={tenantsQuery.isLoading}
                        options={tenantOptions}
                    />
                </Form.Item>
            </Card>

            {tenantId ? (
                <Card title={t('license.testPanel.tenantSection')} size="small" style={{ marginBottom: 16 }}>
                    {snapshotQuery.isLoading ? (
                        <Spin />
                    ) : tenant ? (
                        <Descriptions column={1} size="small">
                            <Descriptions.Item label={t('license.testPanel.status')}>
                                <Tag color={licenseTestStatusTagColor(tenant.status)}>
                                    {t(
                                        `license.testPanel.statusLabels.${tenant.status}` as 'license.testPanel.statusLabels.active',
                                    )}
                                </Tag>
                            </Descriptions.Item>
                            <Descriptions.Item label={t('license.testPanel.validUntil')}>
                                {tenant.validUntilUtc
                                    ? dayjs(tenant.validUntilUtc).format('YYYY-MM-DD HH:mm')
                                    : '—'}
                            </Descriptions.Item>
                            <Descriptions.Item label={t('license.testPanel.daysRemaining')}>
                                {!tenant.validUntilUtc && tenant.daysRemaining >= 999
                                    ? t('license.testPanel.unlimitedNoExpiry')
                                    : tenant.daysRemaining}
                            </Descriptions.Item>
                            <Descriptions.Item label={t('license.testPanel.licenseKey')}>
                                {tenant.licenseKey ?? '—'}
                            </Descriptions.Item>
                        </Descriptions>
                    ) : null}
                </Card>
            ) : null}

            {tenantId && deployment ? (
                <Card title={t('license.testPanel.deploymentSection')} size="small" style={{ marginBottom: 16 }}>
                    <Descriptions column={1} size="small">
                        <Descriptions.Item label={t('license.testPanel.daysRemaining')}>
                            {deployment.daysRemaining}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('license.testPanel.mode')}>
                            {deployment.mode}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('license.testPanel.validUntil')}>
                            {deployment.expiryDateUtc
                                ? dayjs(deployment.expiryDateUtc).format('YYYY-MM-DD HH:mm')
                                : '—'}
                        </Descriptions.Item>
                    </Descriptions>
                </Card>
            ) : null}

            <Card title={t('license.testPanel.simulationTitle')} style={{ marginBottom: 16 }}>
                <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                    {scenarios.map((s) => (
                        <Button
                            key={s.status}
                            block
                            color={licenseTestScenarioButtonColor(s.color)}
                            variant={s.status === 'expired' ? 'solid' : 'outlined'}
                            onClick={() => void handleScenario(s.days)}
                            loading={isPending}
                        >
                            {t(licenseTestScenarioLabelKey(s.status))}
                        </Button>
                    ))}
                </Space>
            </Card>

            <Card title={t('license.testPanel.manualTitle')}>
                <Form.Item
                    name="validUntil"
                    label={t('license.testPanel.validUntil')}
                    rules={[{ required: true }]}
                >
                    <DatePicker showTime style={{ width: '100%' }} />
                </Form.Item>
                <Form.Item>
                    <Button type="primary" htmlType="submit" loading={isPending}>
                        {t('license.testPanel.updateButton')}
                    </Button>
                </Form.Item>
            </Card>
        </Form>
    );
}
