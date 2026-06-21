'use client';

import React from 'react';
import Link from 'next/link';
import { Alert, Button, Descriptions, List, Modal, Space, Tag, Typography } from 'antd';
import { LinkOutlined, WarningOutlined } from '@ant-design/icons';

import type { TenantDeleteDependenciesDto } from '@/api/generated/model';
import { useI18n } from '@/i18n';
import {
    buildTenantDeletePreparationHref,
    getNonZeroTenantDeleteCounts,
    resolveTenantDeleteFailureMessage,
    TENANT_DELETE_BLOCKER_CODE_KEYS,
    TENANT_DELETE_NEXT_STEP_KEYS,
} from '@/features/super-admin/utils/tenantDeleteDependencyUi';

export type TenantDeleteDependenciesModalProps = {
    open: boolean;
    tenantId: string;
    tenantName?: string;
    dependencies: TenantDeleteDependenciesDto | null;
    failureCode?: string | null;
    onClose: () => void;
};

function blockerSeverityColor(severity: string | null | undefined): string {
    switch (severity) {
        case 'blocking':
            return 'error';
        case 'compliance':
            return 'warning';
        default:
            return 'default';
    }
}

export function TenantDeleteDependenciesModal({
    open,
    tenantId,
    tenantName,
    dependencies,
    failureCode,
    onClose,
}: TenantDeleteDependenciesModalProps) {
    const { t } = useI18n();

    if (!dependencies) return null;

    const counts = getNonZeroTenantDeleteCounts(dependencies.dependencies);
    const blockers = dependencies.blockingDependencies ?? [];
    const nextSteps = dependencies.nextSteps ?? [];
    const resolvedFailureCode = failureCode ?? dependencies.failureCode;
    const failureMessage = resolveTenantDeleteFailureMessage(
        t,
        resolvedFailureCode,
        dependencies.failureMessage,
    );

    return (
        <Modal
            title={
                <Space>
                    <WarningOutlined style={{ color: '#cf1322' }} />
                    {t('tenants.deleteDependencies.modalTitle')}
                </Space>
            }
            open={open}
            onCancel={onClose}
            width={720}
            destroyOnHidden
            footer={[
                <Button key="close" onClick={onClose}>
                    {t('common.close', { defaultValue: 'Schließen' })}
                </Button>,
                <Link key="details" href={buildTenantDeletePreparationHref(tenantId)}>
                    <Button type="primary" icon={<LinkOutlined />}>
                        {t('tenants.deleteDependencies.openPreparationPage')}
                    </Button>
                </Link>,
            ]}
        >
            <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                {tenantName ? (
                    <Typography.Text type="secondary">
                        {t('tenants.deleteDependencies.tenantLine', {
                            name: tenantName,
                            slug: dependencies.tenantSlug ?? '—',
                        })}
                    </Typography.Text>
                ) : null}

                {dependencies.canHardDelete ? (
                    <Alert type="success" showIcon title={t('tenants.deleteDependencies.canHardDelete')} />
                ) : (
                    <Alert type="error" showIcon title={failureMessage} />
                )}

                {dependencies.hasFiscalFootprint ? (
                    <Alert
                        type="warning"
                        showIcon
                        title={t('tenants.deleteDependencies.fiscalFootprintTitle')}
                        description={t('tenants.deleteDependencies.fiscalFootprintBody')}
                    />
                ) : null}

                {counts.length > 0 ? (
                    <>
                        <Typography.Text strong>{t('tenants.deleteDependencies.countsTitle')}</Typography.Text>
                        <Descriptions bordered size="small" column={1}>
                            {counts.map(({ row, value }) => (
                                <Descriptions.Item key={row.key} label={t(row.labelKey)}>
                                    <Space>
                                        <span>{value}</span>
                                        {row.buildHref ? (
                                            <Link href={row.buildHref(tenantId)}>
                                                {t('tenants.deleteDependencies.openRelated')}
                                            </Link>
                                        ) : null}
                                    </Space>
                                </Descriptions.Item>
                            ))}
                        </Descriptions>
                    </>
                ) : null}

                {blockers.length > 0 ? (
                    <>
                        <Typography.Text strong>{t('tenants.deleteDependencies.blockersTitle')}</Typography.Text>
                        <List
                            size="small"
                            dataSource={blockers}
                            renderItem={(blocker) => {
                                const code = blocker.code?.trim();
                                const labelKey = code ? TENANT_DELETE_BLOCKER_CODE_KEYS[code] : undefined;
                                return (
                                    <List.Item>
                                        <Space orientation="vertical" size={2} style={{ width: '100%' }}>
                                            <Space wrap>
                                                {blocker.severity ? (
                                                    <Tag color={blockerSeverityColor(blocker.severity)}>
                                                        {blocker.severity}
                                                    </Tag>
                                                ) : null}
                                                {typeof blocker.count === 'number' && blocker.count > 0 ? (
                                                    <Tag>{blocker.count}</Tag>
                                                ) : null}
                                            </Space>
                                            <Typography.Text>
                                                {labelKey
                                                    ? t(labelKey)
                                                    : blocker.message?.trim() ||
                                                      code ||
                                                      t('tenants.deleteDependencies.blockers.generic')}
                                            </Typography.Text>
                                        </Space>
                                    </List.Item>
                                );
                            }}
                        />
                    </>
                ) : null}

                {nextSteps.length > 0 ? (
                    <>
                        <Typography.Text strong>{t('tenants.deleteDependencies.nextStepsTitle')}</Typography.Text>
                        <List
                            size="small"
                            dataSource={nextSteps}
                            renderItem={(step) => {
                                const labelKey = TENANT_DELETE_NEXT_STEP_KEYS[step];
                                return (
                                    <List.Item>
                                        <Typography.Text>
                                            {labelKey
                                                ? t(labelKey)
                                                : step}
                                        </Typography.Text>
                                    </List.Item>
                                );
                            }}
                        />
                    </>
                ) : null}

                <Alert
                    type="info"
                    showIcon
                    title={t('tenants.deleteDependencies.archiveHintTitle')}
                    description={t('tenants.deleteDependencies.archiveHintBody')}
                />
            </Space>
        </Modal>
    );
}
