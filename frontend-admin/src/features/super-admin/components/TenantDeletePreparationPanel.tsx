'use client';

import React, { useMemo, useState } from 'react';
import Link from 'next/link';
import {
    Alert,
    Button,
    Card,
    Space,
    Spin,
    Table,
    Tag,
    Tooltip,
    Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DeleteOutlined, LinkOutlined, ReloadOutlined } from '@ant-design/icons';

import type { TenantDeleteDependenciesDto } from '@/api/generated/model';
import { TenantArchiveConfirmModal } from '@/features/super-admin/components/TenantArchiveConfirmModal';
import { TenantPermanentDeleteModal } from '@/features/super-admin/components/TenantPermanentDeleteModal';
import { useTenantDeleteDependencies } from '@/features/super-admin/hooks/useTenantDeleteDependencies';
import { useI18n } from '@/i18n';
import {
    resolveTenantDeleteFailureMessage,
    TENANT_DELETE_COUNT_ROWS,
    type TenantDeleteCountKey,
} from '@/features/super-admin/utils/tenantDeleteDependencyUi';

export type TenantDeletePreparationPanelProps = {
    tenantId: string;
    tenantName: string;
    tenantSlug: string;
    tenantStatus: string;
    onArchiveSuccess?: () => void;
    onPermanentDeleteSuccess?: () => void;
};

type PreparationTableRow = {
    key: TenantDeleteCountKey;
    category: string;
    count: number;
    status: 'none' | 'present' | 'blocking' | 'compliance';
    href?: string;
};

const DELETE_PREPARATION_CATEGORY_KEYS: Partial<Record<TenantDeleteCountKey, string>> = {
    users: 'tenants.deletePreparation.categories.users',
    memberships: 'tenants.deletePreparation.categories.memberships',
    cashRegisters: 'tenants.deletePreparation.categories.cashRegisters',
    payments: 'tenants.deletePreparation.categories.payments',
    receipts: 'tenants.deletePreparation.categories.receipts',
    vouchers: 'tenants.deletePreparation.categories.vouchers',
    voucherLedgerEntries: 'tenants.deleteDependencies.counts.voucherLedgerEntries',
    dailyClosings: 'tenants.deletePreparation.categories.dailyClosings',
    products: 'tenants.deletePreparation.categories.products',
    categories: 'tenants.deletePreparation.categories.categories',
    finanzOnlineSubmissions: 'tenants.deleteDependencies.counts.finanzOnlineSubmissions',
    auditLogs: 'tenants.deletePreparation.categories.auditLogs',
};

function resolvePreparationRowStatus(
    countKey: TenantDeleteCountKey,
    count: number,
    dependencies: TenantDeleteDependenciesDto,
): PreparationTableRow['status'] {
    if (count === 0) return 'none';
    if (countKey === 'cashRegisters') return 'blocking';
    if (countKey === 'auditLogs') return 'compliance';
    if (
        (countKey === 'payments' || countKey === 'dailyClosings' || countKey === 'receipts') &&
        dependencies.hasFiscalFootprint
    ) {
        return 'compliance';
    }
    return 'present';
}

function buildPreparationTableRows(
    tenantId: string,
    dependencies: TenantDeleteDependenciesDto,
    labelForKey: (key: TenantDeleteCountKey) => string,
): PreparationTableRow[] {
    const counts = dependencies.dependencies ?? {};
    return TENANT_DELETE_COUNT_ROWS.map((row) => {
        const count = counts[row.key] ?? 0;
        return {
            key: row.key,
            category: labelForKey(row.key),
            count,
            status: resolvePreparationRowStatus(row.key, count, dependencies),
            href: row.buildHref?.(tenantId),
        };
    });
}

function DependencyOverviewBody({
    tenantId,
    tenantName,
    tenantSlug,
    tenantStatus,
    dependencies,
    onArchiveSuccess,
    onPermanentDeleteSuccess,
}: TenantDeletePreparationPanelProps & { dependencies: TenantDeleteDependenciesDto }) {
    const { t } = useI18n();
    const [archiveOpen, setArchiveOpen] = useState(false);
    const [permanentDeleteOpen, setPermanentDeleteOpen] = useState(false);

    const isDeleted = tenantStatus === 'deleted';
    const canHardDelete = dependencies.canHardDelete === true;
    const hardDeleteBlockedReason = resolveTenantDeleteFailureMessage(
        t,
        dependencies.failureCode,
        dependencies.failureMessage,
    );

    const hardDeleteButton = (
        <Button
            danger
            icon={<DeleteOutlined />}
            onClick={() => setPermanentDeleteOpen(true)}
        >
            {t('tenants.deletePreparation.hardDeleteButton')}
        </Button>
    );

    const hardDeleteControl =
        !canHardDelete ? (
            <Tooltip title={hardDeleteBlockedReason}>{hardDeleteButton}</Tooltip>
        ) : (
            hardDeleteButton
        );

    const labelForKey = (key: TenantDeleteCountKey): string => {
        const prepKey = DELETE_PREPARATION_CATEGORY_KEYS[key];
        return prepKey ? t(prepKey) : t(`tenants.deleteDependencies.counts.${key}`);
    };

    const tableRows = useMemo(
        () => buildPreparationTableRows(tenantId, dependencies, labelForKey),
        [tenantId, dependencies, t],
    );

    const columns: ColumnsType<PreparationTableRow> = [
        {
            title: t('tenants.deletePreparation.tableCategory'),
            dataIndex: 'category',
            key: 'category',
            render: (category: string, row) =>
                row.href ? (
                    <Space>
                        <span>{category}</span>
                        <Link href={row.href}>{t('tenants.deleteDependencies.openRelated')}</Link>
                    </Space>
                ) : (
                    category
                ),
        },
        {
            title: t('tenants.deletePreparation.tableCount'),
            dataIndex: 'count',
            key: 'count',
            width: 96,
        },
        {
            title: t('tenants.deletePreparation.tableStatus'),
            dataIndex: 'status',
            key: 'status',
            width: 140,
            render: (status: PreparationTableRow['status']) => {
                if (status === 'blocking') {
                    return (
                        <Tag color="error">{t('tenants.deletePreparation.statusBlocking')}</Tag>
                    );
                }
                if (status === 'compliance') {
                    return (
                        <Tag color="warning">{t('tenants.deletePreparation.statusCompliance')}</Tag>
                    );
                }
                if (status === 'present') {
                    return <Tag color="processing">{t('tenants.deletePreparation.statusPresent')}</Tag>;
                }
                return <Tag>{t('tenants.deletePreparation.statusNone')}</Tag>;
            },
        },
    ];

    return (
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {tenantName} ({dependencies.tenantSlug ?? tenantSlug})
            </Typography.Paragraph>

            <Alert type="info" showIcon message={t('tenants.deletePreparation.complianceInfo')} />

            {!canHardDelete || dependencies.hasFiscalFootprint ? (
                <Alert
                    type="warning"
                    showIcon
                    title={t('tenants.deletePreparation.blockedTitle')}
                    description={t('tenants.deletePreparation.blockedBody')}
                />
            ) : null}

            <Card size="small" title={t('tenants.deletePreparation.dependenciesTitle')}>
                <Table<PreparationTableRow>
                    size="small"
                    rowKey="key"
                    pagination={false}
                    columns={columns}
                    dataSource={tableRows}
                />
            </Card>

            <Space wrap>
                {!isDeleted ? (
                    <Button type="primary" danger onClick={() => setArchiveOpen(true)}>
                        {t('tenants.deletePreparation.archiveButton')}
                    </Button>
                ) : null}
                {isDeleted ? hardDeleteControl : null}
                <Link href={`/admin/tenants/${tenantId}/decommission`}>
                    <Button icon={<LinkOutlined />}>
                        {t('tenants.detail.settings.danger.decommissionWizardButton')}
                    </Button>
                </Link>
                <Link href={`/admin/tenants/${tenantId}?tab=settings#danger-zone`}>
                    <Button>{t('tenants.deleteDependencies.openDangerZone')}</Button>
                </Link>
            </Space>

            <TenantArchiveConfirmModal
                open={archiveOpen}
                tenantId={tenantId}
                tenantName={tenantName}
                onClose={() => setArchiveOpen(false)}
                onSuccess={() => onArchiveSuccess?.()}
            />

            <TenantPermanentDeleteModal
                open={permanentDeleteOpen}
                tenantId={tenantId}
                tenantName={tenantName}
                tenantSlug={tenantSlug}
                onClose={() => setPermanentDeleteOpen(false)}
                onSuccess={() => onPermanentDeleteSuccess?.()}
            />
        </Space>
    );
}

export function TenantDeletePreparationPanel(props: TenantDeletePreparationPanelProps) {
    const { t } = useI18n();
    const query = useTenantDeleteDependencies(props.tenantId);

    if (query.isLoading) {
        return (
            <div style={{ textAlign: 'center', padding: 48 }}>
                <Spin />
            </div>
        );
    }

    if (query.isError || !query.data) {
        return (
            <Alert
                type="error"
                showIcon
                title={t('tenants.deleteDependencies.loadFailed')}
                action={
                    <Button icon={<ReloadOutlined />} onClick={() => void query.refetch()}>
                        {t('common.retry', { defaultValue: 'Erneut versuchen' })}
                    </Button>
                }
            />
        );
    }

    return (
        <DependencyOverviewBody
            {...props}
            dependencies={{
                ...query.data,
                tenantSlug: query.data.tenantSlug ?? props.tenantSlug,
            }}
        />
    );
}
