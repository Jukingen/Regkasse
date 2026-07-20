'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    Checkbox,
    Input,
    Modal,
    Space,
    Table,
    Tag,
    Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { WarningOutlined } from '@ant-design/icons';
import { useMutation } from '@tanstack/react-query';
import { CardSkeleton } from '@/components/Skeleton';
import { deleteApiAdminTenantsTenantIdPermanent } from '@/api/generated/admin/admin';
import type { TenantDeleteDependenciesDto } from '@/api/generated/model';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { useTenantDeleteDependencies } from '@/features/super-admin/hooks/useTenantDeleteDependencies';
import { parseTenantPermanentDeleteError } from '@/features/super-admin/utils/parseTenantPermanentDeleteError';
import {
    getNonZeroTenantDeleteCounts,
    resolveTenantDeleteFailureMessage,
    TENANT_DELETE_BLOCKER_CODE_KEYS,
    type TenantDeleteCountKey,
} from '@/features/super-admin/utils/tenantDeleteDependencyUi';

/** Operator confirmation phrase (German UI); must match exactly after trim. */
export const TENANT_PERMANENT_DELETE_CONFIRM_PHRASE = 'löschen bestätigen';

export type TenantPermanentDeleteModalProps = {
    open: boolean;
    tenantId: string;
    tenantName: string;
    tenantSlug: string;
    onClose: () => void;
    onSuccess: () => void;
};

type DependencyTableRow = {
    key: TenantDeleteCountKey;
    category: string;
    count: number;
    status: 'blocking' | 'compliance' | 'info';
};

function resolveCountRowStatus(
    countKey: TenantDeleteCountKey,
    dependencies: TenantDeleteDependenciesDto,
): DependencyTableRow['status'] {
    const counts = dependencies.dependencies;
    if (countKey === 'cashRegisters' && (counts?.cashRegisters ?? 0) > 0) {
        return 'blocking';
    }
    if (
        (countKey === 'payments' || countKey === 'dailyClosings') &&
        dependencies.hasFiscalFootprint
    ) {
        return 'compliance';
    }
    if (countKey === 'auditLogs' && (counts?.auditLogs ?? 0) > 0) {
        return 'compliance';
    }
    return 'info';
}

export function TenantPermanentDeleteModal({
    open,
    tenantId,
    tenantName,
    tenantSlug,
    onClose,
    onSuccess,
}: TenantPermanentDeleteModalProps) {
    const { t } = useI18n();
    const { message } = useAntdApp();
    const [confirmSlug, setConfirmSlug] = useState('');
    const [confirmPhrase, setConfirmPhrase] = useState('');
    const [retentionAck, setRetentionAck] = useState(false);
    const [submitSummary, setSubmitSummary] = useState<TenantDeleteDependenciesDto | null>(null);
    const [submitFailureCode, setSubmitFailureCode] = useState<string | null>(null);

    const dependenciesQuery = useTenantDeleteDependencies(tenantId, open);

    const resetForm = useCallback(() => {
        setConfirmSlug('');
        setConfirmPhrase('');
        setRetentionAck(false);
        setSubmitSummary(null);
        setSubmitFailureCode(null);
    }, []);

    useEffect(() => {
        if (!open) {
            resetForm();
        }
    }, [open, resetForm]);

    const dependencies = submitSummary ?? dependenciesQuery.data ?? null;

    const slugMatches =
        confirmSlug.trim().toLowerCase() === tenantSlug.trim().toLowerCase();
    const phraseMatches =
        confirmPhrase.trim() === TENANT_PERMANENT_DELETE_CONFIRM_PHRASE;
    const hardDeleteBlocked =
        dependencies?.hasFiscalFootprint === true || dependencies?.canHardDelete === false;

    const deleteMutation = useMutation({
        mutationFn: () =>
            deleteApiAdminTenantsTenantIdPermanent(tenantId, {
                confirmSlug: confirmSlug.trim(),
            }),
        onSuccess: () => {
            message.success(t('tenants.messages.hardDeleted'));
            resetForm();
            onClose();
            onSuccess();
        },
        onError: (error: unknown) => {
            const structured = parseTenantPermanentDeleteError(error);
            if (structured && (structured.dependencies || structured.code)) {
                if (structured.dependencies) {
                    setSubmitSummary(structured.dependencies);
                }
                setSubmitFailureCode(structured.code ?? null);
                return;
            }
            message.error(t('tenants.messages.hardDeleteFailed'));
        },
    });

    const canSubmit =
        !!dependencies &&
        !dependenciesQuery.isLoading &&
        slugMatches &&
        phraseMatches &&
        retentionAck &&
        !hardDeleteBlocked &&
        !deleteMutation.isPending;

    const tableRows = useMemo((): DependencyTableRow[] => {
        if (!dependencies) return [];
        return getNonZeroTenantDeleteCounts(dependencies.dependencies).map(({ row, value }) => ({
            key: row.key,
            category: t(row.labelKey),
            count: value,
            status: resolveCountRowStatus(row.key, dependencies),
        }));
    }, [dependencies, t]);

    const blockingDependencies = useMemo(() => {
        return (dependencies?.blockingDependencies ?? []).filter(
            (blocker) => blocker.severity === 'blocking' || blocker.severity === 'compliance',
        );
    }, [dependencies?.blockingDependencies]);

    const submitBlockedMessage = submitFailureCode || submitSummary
        ? resolveTenantDeleteFailureMessage(
              t,
              submitFailureCode ?? submitSummary?.failureCode,
              submitSummary?.failureMessage,
          )
        : null;

    const columns: ColumnsType<DependencyTableRow> = [
        {
            title: t('tenants.permanentDeleteModal.columns.category'),
            dataIndex: 'category',
            key: 'category',
        },
        {
            title: t('tenants.permanentDeleteModal.columns.count'),
            dataIndex: 'count',
            key: 'count',
            width: 96,
        },
        {
            title: t('tenants.permanentDeleteModal.columns.status'),
            dataIndex: 'status',
            key: 'status',
            width: 140,
            render: (status: DependencyTableRow['status']) => {
                if (status === 'blocking') {
                    return (
                        <Tag color="error">{t('tenants.permanentDeleteModal.status.blocking')}</Tag>
                    );
                }
                if (status === 'compliance') {
                    return (
                        <Tag color="warning">
                            {t('tenants.permanentDeleteModal.status.compliance')}
                        </Tag>
                    );
                }
                return <Tag>{t('tenants.permanentDeleteModal.status.info')}</Tag>;
            },
        },
    ];

    const handleClose = () => {
        if (deleteMutation.isPending) return;
        resetForm();
        onClose();
    };

    return (
        <Modal
            title={
                <Space>
                    <WarningOutlined style={{ color: '#cf1322' }} />
                    {t('tenants.permanentDeleteModal.title')}
                </Space>
            }
            open={open}
            onCancel={handleClose}
            width={760}
            destroyOnHidden
            footer={
                <Space>
                    <Button onClick={handleClose} disabled={deleteMutation.isPending}>
                        {t('common.cancel', { defaultValue: 'Abbrechen' })}
                    </Button>
                    <Button
                        danger
                        type="primary"
                        loading={deleteMutation.isPending}
                        disabled={!canSubmit}
                        onClick={() => deleteMutation.mutate()}
                    >
                        {t('tenants.actions.hardDelete')}
                    </Button>
                </Space>
            }
        >
            {dependenciesQuery.isLoading && !dependencies ? (
                <CardSkeleton count={2} />
            ) : (
                <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                    <Typography.Text type="secondary">
                        {t('tenants.deleteDependencies.tenantLine', {
                            name: tenantName,
                            slug: tenantSlug,
                        })}
                    </Typography.Text>

                    <Alert
                        type="warning"
                        showIcon
                        title={t('tenants.permanentDeleteModal.complianceWarningTitle')}
                        description={t('tenants.permanentDeleteModal.complianceWarningBody')}
                    />

                    {dependencies?.hasFiscalFootprint ? (
                        <Alert
                            type="error"
                            showIcon
                            title={t('tenants.permanentDeleteModal.fiscalBlockedTitle')}
                            description={t('tenants.permanentDeleteModal.fiscalBlockedBody')}
                        />
                    ) : null}

                    {submitBlockedMessage ? (
                        <Alert type="error" showIcon title={submitBlockedMessage} />
                    ) : null}

                    {blockingDependencies.length > 0 ? (
                        <Space wrap size={[8, 8]}>
                            {blockingDependencies.map((blocker) => {
                                const code = blocker.code?.trim();
                                const labelKey = code
                                    ? TENANT_DELETE_BLOCKER_CODE_KEYS[code]
                                    : undefined;
                                const label = labelKey
                                    ? t(labelKey)
                                    : blocker.message?.trim() ||
                                      code ||
                                      t('tenants.deleteDependencies.blockers.generic');
                                return (
                                    <Tag
                                        key={`${code ?? 'blocker'}-${blocker.count ?? 0}-${label}`}
                                        color={
                                            blocker.severity === 'compliance' ? 'warning' : 'error'
                                        }
                                    >
                                        {label}
                                        {typeof blocker.count === 'number' && blocker.count > 0
                                            ? ` (${blocker.count})`
                                            : ''}
                                    </Tag>
                                );
                            })}
                        </Space>
                    ) : null}

                    <Card size="small" title={t('tenants.permanentDeleteModal.dependenciesCardTitle')}>
                        {tableRows.length === 0 ? (
                            <Typography.Text type="secondary">
                                {t('tenants.deleteDependencies.noCounts')}
                            </Typography.Text>
                        ) : (
                            <Table<DependencyTableRow>
                                size="small"
                                rowKey="key"
                                pagination={false}
                                columns={columns}
                                dataSource={tableRows}
                            />
                        )}
                    </Card>

                    <Card size="small" title={t('tenants.permanentDeleteModal.confirmationCardTitle')}>
                        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                            <div>
                                <Typography.Text>
                                    {t('tenants.confirmHardDelete.confirmSlugLabel', {
                                        slug: tenantSlug,
                                    })}
                                </Typography.Text>
                                <Input
                                    value={confirmSlug}
                                    onChange={(e) => setConfirmSlug(e.target.value)}
                                    placeholder={tenantSlug}
                                    autoComplete="off"
                                    status={
                                        confirmSlug.length > 0 && !slugMatches ? 'error' : undefined
                                    }
                                    style={{ marginTop: 8 }}
                                />
                            </div>
                            <div>
                                <Typography.Text>
                                    {t('tenants.permanentDeleteModal.phraseLabel', {
                                        phrase: TENANT_PERMANENT_DELETE_CONFIRM_PHRASE,
                                    })}
                                </Typography.Text>
                                <Input
                                    value={confirmPhrase}
                                    onChange={(e) => setConfirmPhrase(e.target.value)}
                                    placeholder={TENANT_PERMANENT_DELETE_CONFIRM_PHRASE}
                                    autoComplete="off"
                                    status={
                                        confirmPhrase.length > 0 && !phraseMatches
                                            ? 'error'
                                            : undefined
                                    }
                                    style={{ marginTop: 8 }}
                                />
                            </div>
                            <Checkbox
                                checked={retentionAck}
                                onChange={(e) => setRetentionAck(e.target.checked)}
                            >
                                {t('tenants.permanentDeleteModal.retentionAck')}
                            </Checkbox>
                        </Space>
                    </Card>
                </Space>
            )}
        </Modal>
    );
}
