'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Super-admin wizard that decommissions all tenant registers before archiving the tenant.
 */
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { Alert, Button, Card, Checkbox, Descriptions, Input, List, Progress, Result, Space, Steps, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
    ArrowLeftOutlined,
    CheckCircleOutlined,
    ClockCircleOutlined,
    DeleteOutlined,
    ExclamationCircleOutlined,
    ReloadOutlined,
    SafetyCertificateOutlined,
    WarningOutlined,
} from '@ant-design/icons';
import JSZip from 'jszip';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
    getApiAdminFiscalExportDownloadExportId,
    postApiAdminFiscalExportGenerate,
} from '@/api/generated/admin/admin';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { formatDateTime, useI18n } from '@/i18n';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';
import {
    fiscalExportDisclaimerAckHeaders,
} from '@/api/admin-rksv/client';
import {
    getAdminTenantById,
    softDeleteAdminTenant,
    type AdminTenantDetail,
} from '@/features/super-admin/api/adminTenants';
import {
    decommissionCashRegister,
    listAdminCashRegisters,
    type AdminCashRegisterListItem,
} from '@/features/cash-registers/api/cashRegisters';
import {
    canDecommissionRegister,
    isDecommissionedRegister,
    registerStatusTagColor,
    REGISTER_STATUS,
} from '@/features/cash-registers/utils/registerStatus';
import {
    areTenantDecommissionChecksSatisfied,
    buildTenantDecommissionPreflightChecks,
    buildTenantDecommissionRegisterSummary,
    type TenantDecommissionManualChecks,
    type TenantDecommissionPreflightCheck,
    type TenantDecommissionRegisterSummary,
} from '@/features/super-admin/utils/tenantDecommissionWizard';
import { FiscalExportDisclaimerModal } from '@/features/rksv/components/FiscalExportDisclaimerModal';
import {
    isFiscalExportDisclaimerSkipped,
    setFiscalExportDisclaimerSkip24h,
} from '@/features/rksv/fiscalExportDisclaimerSession';

const TENANT_DETAIL_QUERY_KEY = ['admin', 'tenant-detail'] as const;
const TENANT_DECOMMISSION_REGISTERS_QUERY_KEY = ['admin', 'tenant-decommission-registers'] as const;

type TranslateFn = (key: string, options?: Record<string, string | number>) => string;
type DeferredFiscalExportResponse = {
    exportId?: string;
    format?: string;
    disclaimerUrl?: string | null;
};

function buildTenantAuditLogsHref(tenantId: string): string {
    const qp = new URLSearchParams({ entityType: 'Tenant', entityId: tenantId });
    return `/audit-logs?${qp.toString()}`;
}

function slugifyFilePart(value: string | null | undefined, fallback: string): string {
    const normalized = value?.trim().toLowerCase().replace(/[^a-z0-9_-]+/g, '-').replace(/-+/g, '-');
    return normalized && normalized.length > 0 ? normalized.replace(/^-|-$/g, '') : fallback;
}

function isDeferredFiscalExportResponse(value: unknown): value is DeferredFiscalExportResponse {
    return value !== null && typeof value === 'object' && 'exportId' in value;
}

function formatRegisterStatus(status: number | undefined, t: TranslateFn): string {
    switch (status) {
        case REGISTER_STATUS.closed:
            return t('cashRegisters.status.closed');
        case REGISTER_STATUS.open:
            return t('cashRegisters.status.open');
        case REGISTER_STATUS.maintenance:
            return t('cashRegisters.status.maintenance');
        case REGISTER_STATUS.disabled:
            return t('cashRegisters.status.disabled');
        case REGISTER_STATUS.decommissioned:
            return t('cashRegisters.status.decommissioned');
        default:
            return t('cashRegisters.status.unknown', { status: status ?? '—' });
    }
}

function renderCheckIcon(check: TenantDecommissionPreflightCheck): React.ReactNode {
    if (check.status === 'passed') {
        return <CheckCircleOutlined style={{ color: '#52c41a', fontSize: 18, marginTop: 2 }} />;
    }

    if (check.status === 'warning') {
        return <WarningOutlined style={{ color: '#faad14', fontSize: 18, marginTop: 2 }} />;
    }

    return <ExclamationCircleOutlined style={{ color: '#ff4d4f', fontSize: 18, marginTop: 2 }} />;
}

function getPreflightCopy(
    check: TenantDecommissionPreflightCheck,
    summary: ReturnType<typeof buildTenantDecommissionRegisterSummary>,
    t: TranslateFn,
): { label: string; description: string } {
    if (check.key === 'noOpenPayments') {
        return {
            label: t('tenants.decommission.preflight.items.noOpenPayments.label'),
            description: t('tenants.decommission.preflight.items.noOpenPayments.description'),
        };
    }

    if (check.key === 'dailyClosingDone') {
        return {
            label: t('tenants.decommission.preflight.items.dailyClosingDone.label'),
            description: t('tenants.decommission.preflight.items.dailyClosingDone.description'),
        };
    }

    if (check.key === 'registersPrepared') {
        let description = t('tenants.decommission.preflight.items.registersPrepared.readyDescription');
        if (summary.open > 0) {
            description = t('tenants.decommission.preflight.items.registersPrepared.openDescription', {
                count: summary.open,
            });
        } else if (summary.maintenance + summary.disabled > 0) {
            description = t('tenants.decommission.preflight.items.registersPrepared.blockedDescription');
        }

        return {
            label: t('tenants.decommission.preflight.items.registersPrepared.label'),
            description,
        };
    }

    return {
        label: t('tenants.decommission.preflight.items.fiscalExport.label'),
        description: t('tenants.decommission.preflight.items.fiscalExport.description'),
    };
}

type DecommissionWizardStepProps = {
    t: TranslateFn;
    formatLocale: string;
    tenantId: string;
    tenant?: AdminTenantDetail;
    registers: AdminCashRegisterListItem[];
    columns: ColumnsType<AdminCashRegisterListItem>;
    registerSummary: TenantDecommissionRegisterSummary;
    preflightChecks: TenantDecommissionPreflightCheck[];
    manualChecks: TenantDecommissionManualChecks;
    onTogglePreflightCheck: (check: TenantDecommissionPreflightCheck, next: boolean) => void;
    canContinueFromPreflight: boolean;
    canDecommissionCashRegisters: boolean;
    blockedRegisters: AdminCashRegisterListItem[];
    canProceedToArchive: boolean;
    decommissionReason: string;
    onDecommissionReasonChange: (value: string) => void;
    bulkDecommissionPending: boolean;
    decommissionProgress: number;
    onDecommissionAll: () => void;
    canExportFiscalData: boolean;
    fiscalExportProfile: string;
    fiscalExportFromUtc: string;
    fiscalExportToUtc: string;
    fiscalExportPending: boolean;
    fiscalExportProgress: number;
    fiscalExportUrl: string | null;
    fiscalExportFileName: string | null;
    lastExportedAtUtc: string | null;
    onGenerateFiscalExport: () => void;
    canAdvanceFromFiscalExport: boolean;
    confirmTenantName: string;
    onConfirmTenantNameChange: (value: string) => void;
    tenantNameMatches: boolean;
    archivePending: boolean;
    archiveReady: boolean;
    onArchive: () => void;
    archivedAtUtc: string | null;
    lastDecommissionCount: number;
    auditHref: string;
    onNext: () => void;
    onPrevious: () => void;
};

function DecommissionWizardStep1({
    t,
    tenant,
    tenantId,
    registerSummary,
    preflightChecks,
    manualChecks,
    onTogglePreflightCheck,
    canContinueFromPreflight,
    onNext,
}: DecommissionWizardStepProps) {
    return (
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
            <Descriptions
                bordered
                size="small"
                column={{ xs: 1, md: 2 }}
                title={t('tenants.decommission.summaryTitle')}
            >
                <Descriptions.Item label={t('tenants.fields.name')}>
                    {tenant?.name ?? '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('tenants.fields.slug')}>
                    {tenant?.slug ?? '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('tenants.decommission.summary.activeUsers')}>
                    {tenant?.activeUserCount ?? 0}
                </Descriptions.Item>
                <Descriptions.Item label={t('tenants.decommission.summary.registersTotal')}>
                    {registerSummary.total}
                </Descriptions.Item>
                <Descriptions.Item label={t('tenants.decommission.summary.registersReady')}>
                    {registerSummary.readyForDecommission}
                </Descriptions.Item>
                <Descriptions.Item label={t('tenants.decommission.summary.registersBlocked')}>
                    {registerSummary.blocked}
                </Descriptions.Item>
            </Descriptions>

            <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                {preflightChecks.map((check) => {
                    const copy = getPreflightCopy(check, registerSummary, t);
                    const checkedValue =
                        check.key === 'noOpenPayments'
                            ? manualChecks.noOpenPayments
                            : check.key === 'dailyClosingDone'
                              ? manualChecks.dailyClosingDone
                              : manualChecks.fiscalExportAcknowledged;

                    return (
                        <div
                            key={check.key}
                            style={{
                                border: '1px solid #f0f0f0',
                                borderRadius: 8,
                                padding: 16,
                            }}
                        >
                            <Space align="start" style={{ width: '100%' }}>
                                {renderCheckIcon(check)}
                                <Space orientation="vertical" size="small" style={{ width: '100%' }}>
                                    <Typography.Text strong>{copy.label}</Typography.Text>
                                    <Typography.Text type="secondary">
                                        {copy.description}
                                    </Typography.Text>
                                    {check.kind === 'manual' ? (
                                        <Checkbox
                                            checked={checkedValue}
                                            onChange={(event) => onTogglePreflightCheck(check, event.target.checked)}
                                        >
                                            {t('tenants.decommission.preflight.manualConfirm')}
                                        </Checkbox>
                                    ) : null}
                                    {check.key === 'fiscalExport' ? (
                                        <Link href="/admin/audit/fiscal-exports">
                                            {t('tenants.decommission.preflight.openFiscalExport')}
                                        </Link>
                                    ) : null}
                                </Space>
                            </Space>
                        </div>
                    );
                })}
            </Space>

            <Space wrap>
                <Link href={`/admin/tenants/${tenantId}?tab=settings`}>
                    <Button>{t('common.buttons.cancel')}</Button>
                </Link>
                <Button type="primary" onClick={onNext} disabled={!canContinueFromPreflight}>
                    {t('tenants.decommission.actions.continue')}
                </Button>
            </Space>
        </Space>
    );
}

function DecommissionWizardStep2({
    t,
    registers,
    columns,
    registerSummary,
    canDecommissionCashRegisters,
    blockedRegisters,
    canProceedToArchive,
    decommissionReason,
    onDecommissionReasonChange,
    bulkDecommissionPending,
    decommissionProgress,
    onDecommissionAll,
    onNext,
    onPrevious,
    tenant,
}: DecommissionWizardStepProps) {
    return (
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
            {!canDecommissionCashRegisters ? (
                <Alert
                    type="error"
                    showIcon
                    title={t('tenants.decommission.registers.permissionDenied')}
                />
            ) : null}

            {blockedRegisters.length > 0 ? (
                <Alert
                    type="error"
                    showIcon
                    title={t('tenants.decommission.registers.blockedTitle', {
                        count: blockedRegisters.length,
                    })}
                    description={t('tenants.decommission.registers.blockedBody')}
                />
            ) : null}

            {canProceedToArchive ? (
                <Alert
                    type="success"
                    showIcon
                    title={t('tenants.decommission.registers.readyForArchive')}
                />
            ) : null}

            <Typography.Title level={4} style={{ margin: 0 }}>
                {t('tenants.decommission.registers.title')}
            </Typography.Title>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t('tenants.decommission.registers.description')}
            </Typography.Paragraph>

            <Descriptions bordered size="small" column={{ xs: 1, md: 2 }}>
                <Descriptions.Item label={t('tenants.decommission.summary.registersReady')}>
                    {registerSummary.readyForDecommission}
                </Descriptions.Item>
                <Descriptions.Item label={t('tenants.decommission.summary.registersBlocked')}>
                    {registerSummary.blocked}
                </Descriptions.Item>
                <Descriptions.Item label={t('tenants.decommission.summary.registersDecommissioned')}>
                    {registerSummary.decommissioned}
                </Descriptions.Item>
                <Descriptions.Item label={t('tenants.decommission.summary.registersRemaining')}>
                    {registerSummary.remainingActive}
                </Descriptions.Item>
            </Descriptions>

            <Input.TextArea
                rows={3}
                value={decommissionReason}
                onChange={(event) => onDecommissionReasonChange(event.target.value)}
                placeholder={t('tenants.decommission.registers.reasonPlaceholder')}
                maxLength={450}
            />

            {bulkDecommissionPending ? (
                <Progress
                    percent={decommissionProgress}
                    status="active"
                    format={(percent) =>
                        t('tenants.decommission.registers.progress', { percent: percent ?? 0 })
                    }
                />
            ) : null}

            <Table
                rowKey="id"
                columns={columns}
                dataSource={registers}
                pagination={false}
                locale={{ emptyText: t('tenants.detail.registers.empty') }}
            />

            <Space wrap>
                <Button onClick={onPrevious}>
                    {t('tenants.decommission.actions.back')}
                </Button>
                <Button
                    danger
                    type="primary"
                    loading={bulkDecommissionPending}
                    disabled={
                        !canDecommissionCashRegisters
                        || blockedRegisters.length > 0
                        || registerSummary.readyForDecommission === 0
                        || tenant?.status === 'deleted'
                    }
                    onClick={onDecommissionAll}
                >
                    {t('tenants.decommission.registers.decommissionAll')}
                </Button>
                {canProceedToArchive ? (
                    <Button type="primary" onClick={onNext}>
                        {t('tenants.decommission.actions.continue')}
                    </Button>
                ) : null}
            </Space>
        </Space>
    );
}

function DecommissionWizardStep3({
    t,
    formatLocale,
    registers,
    canExportFiscalData,
    fiscalExportProfile,
    fiscalExportFromUtc,
    fiscalExportToUtc,
    fiscalExportPending,
    fiscalExportProgress,
    fiscalExportUrl,
    fiscalExportFileName,
    lastExportedAtUtc,
    onGenerateFiscalExport,
    canAdvanceFromFiscalExport,
    onNext,
    onPrevious,
}: DecommissionWizardStepProps) {
    return (
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
            <Typography.Title level={4} style={{ margin: 0 }}>
                {t('tenants.decommission.export.title')}
            </Typography.Title>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t('tenants.decommission.export.description')}
            </Typography.Paragraph>

            <Alert
                type="info"
                showIcon
                title={t('tenants.decommission.export.retentionTitle')}
                description={t('tenants.decommission.export.retentionBody')}
            />

            {!canExportFiscalData ? (
                <Alert
                    type="warning"
                    showIcon
                    title={t('tenants.decommission.export.permissionTitle')}
                    description={t('tenants.decommission.export.permissionBody')}
                />
            ) : null}

            <Descriptions bordered size="small" column={1}>
                <Descriptions.Item label={t('tenants.decommission.export.scopeLabel')}>
                    {t('tenants.decommission.export.scopeValue', {
                        count: registers.length,
                    })}
                </Descriptions.Item>
                <Descriptions.Item label={t('tenants.decommission.export.profileLabel')}>
                    {fiscalExportProfile}
                </Descriptions.Item>
                <Descriptions.Item label={t('tenants.decommission.export.periodLabel')}>
                    {`${formatDateTime(fiscalExportFromUtc, formatLocale)} - ${formatDateTime(
                        fiscalExportToUtc,
                        formatLocale,
                    )}`}
                </Descriptions.Item>
            </Descriptions>

            {fiscalExportPending ? (
                <Progress
                    percent={fiscalExportProgress}
                    status="active"
                    format={(percent) =>
                        t('tenants.decommission.export.progress', { percent: percent ?? 0 })
                    }
                />
            ) : null}

            {fiscalExportUrl ? (
                <Alert
                    type="success"
                    showIcon
                    title={t('tenants.decommission.export.readyTitle')}
                    description={
                        <Space orientation="vertical" size="small">
                            <a href={fiscalExportUrl} download={fiscalExportFileName ?? undefined}>
                                {t('tenants.decommission.export.downloadZip')}
                            </a>
                            <Typography.Text type="secondary">
                                {t('tenants.decommission.export.readyAt', {
                                    value: lastExportedAtUtc
                                        ? formatDateTime(lastExportedAtUtc, formatLocale)
                                        : '—',
                                })}
                            </Typography.Text>
                        </Space>
                    }
                />
            ) : null}

            <Space wrap>
                <Button onClick={onPrevious}>
                    {t('tenants.decommission.actions.back')}
                </Button>
                <Button
                    type="primary"
                    loading={fiscalExportPending}
                    disabled={!canExportFiscalData || registers.length === 0}
                    onClick={onGenerateFiscalExport}
                >
                    {t('tenants.decommission.export.generate')}
                </Button>
                <Button
                    onClick={onNext}
                    disabled={!canAdvanceFromFiscalExport || fiscalExportPending}
                >
                    {t('tenants.decommission.export.continue')}
                </Button>
            </Space>
        </Space>
    );
}

function DecommissionWizardStep4({
    t,
    tenant,
    registerSummary,
    confirmTenantName,
    onConfirmTenantNameChange,
    tenantNameMatches,
    archivePending,
    archiveReady,
    onArchive,
    onPrevious,
}: DecommissionWizardStepProps) {
    return (
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
            <Alert
                type="warning"
                showIcon
                title={t('tenants.decommission.archive.warningTitle')}
                description={t('tenants.decommission.archive.warningBody')}
            />

            <Descriptions bordered size="small" column={1}>
                <Descriptions.Item label={t('tenants.fields.name')}>
                    {tenant?.name ?? '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('tenants.fields.slug')}>
                    {tenant?.slug ?? '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('tenants.decommission.summary.registersDecommissioned')}>
                    {registerSummary.decommissioned}
                </Descriptions.Item>
                <Descriptions.Item label={t('tenants.decommission.summary.activeUsers')}>
                    {tenant?.activeUserCount ?? 0}
                </Descriptions.Item>
            </Descriptions>

            <Typography.Title level={4} style={{ margin: 0 }}>
                {t('tenants.decommission.archive.warningTitle')}
            </Typography.Title>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t('tenants.decommission.archive.warningBody')}
            </Typography.Paragraph>

            <Typography.Text>
                {t('tenants.decommission.archive.confirmLabel', {
                    name: tenant?.name ?? '—',
                })}
            </Typography.Text>
            <Input
                value={confirmTenantName}
                onChange={(event) => onConfirmTenantNameChange(event.target.value)}
                placeholder={tenant?.name}
                autoComplete="off"
                style={{ marginTop: 16 }}
                status={confirmTenantName.length > 0 && !tenantNameMatches ? 'error' : undefined}
            />

            <Space wrap style={{ marginTop: 24 }}>
                <Button onClick={onPrevious}>
                    {t('tenants.decommission.actions.back')}
                </Button>
                <Button
                    danger
                    type="primary"
                    loading={archivePending}
                    disabled={!archiveReady}
                    onClick={onArchive}
                >
                    {t('tenants.decommission.archive.submit')}
                </Button>
            </Space>
        </Space>
    );
}

function DecommissionWizardStep5({
    t,
    tenant,
    tenantId,
    archivedAtUtc,
    formatLocale,
    lastDecommissionCount,
    registerSummary,
    auditHref,
}: DecommissionWizardStepProps) {
    return (
        <Result
            status="success"
            title={t('tenants.decommission.result.title')}
            subTitle={t('tenants.decommission.result.subtitle', {
                name: tenant?.name ?? '—',
            })}
            extra={[
                <Link href="/admin/tenants" key="list">
                    <Button type="primary">
                        {t('tenants.decommission.actions.backToList')}
                    </Button>
                </Link>,
                <Link href="/admin/tenants?includeDeleted=true" key="deleted">
                    <Button>{t('tenants.decommission.actions.openDeletedTenants')}</Button>
                </Link>,
            ]}
        >
            <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                <Typography.Text type="secondary">
                    {t('tenants.decommission.result.archivedAt', {
                        value: archivedAtUtc
                            ? formatDateTime(archivedAtUtc, formatLocale)
                            : '—',
                    })}
                </Typography.Text>
                <Card title={t('tenants.decommission.result.nextStepsTitle')} size="small">
                    <List
                        dataSource={[
                            {
                                key: 'restore',
                                icon: <ClockCircleOutlined />,
                                text: t('tenants.decommission.result.nextSteps.restoreWindow'),
                            },
                            {
                                key: 'hardDelete',
                                icon: <DeleteOutlined />,
                                text: t('tenants.decommission.result.nextSteps.hardDelete'),
                            },
                            {
                                key: 'fiscalExport',
                                icon: <SafetyCertificateOutlined />,
                                text: t('tenants.decommission.result.nextSteps.fiscalExportRetention'),
                            },
                        ]}
                        renderItem={(item) => (
                            <List.Item>
                                <Space>
                                    {item.icon}
                                    <span>{item.text}</span>
                                </Space>
                            </List.Item>
                        )}
                    />
                </Card>
                <Space wrap>
                    <Link href={auditHref}>
                        {t('tenants.decommission.actions.openAuditLogs')}
                    </Link>
                    <Typography.Text type="secondary">
                        {t('tenants.decommission.result.registersDecommissioned', {
                            count: lastDecommissionCount || registerSummary.decommissioned,
                        })}
                    </Typography.Text>
                </Space>
            </Space>
        </Result>
    );
}

export function TenantDecommissionWizardPage() {
  const { message } = useAntdApp();

    const { t, formatLocale, textLocale } = useI18n();
    const params = useParams();
    const queryClient = useQueryClient();
    const { user, hasPermission, canDecommissionCashRegisters } = usePermissions();
    const tenantId = typeof params.tenantId === 'string' ? params.tenantId : '';
    const [currentStep, setCurrentStep] = useState(0);
    const [manualChecks, setManualChecks] = useState<TenantDecommissionManualChecks>({
        noOpenPayments: false,
        dailyClosingDone: false,
        fiscalExportAcknowledged: false,
    });
    const [decommissionReason, setDecommissionReason] = useState('');
    const [confirmTenantName, setConfirmTenantName] = useState('');
    const [lastDecommissionCount, setLastDecommissionCount] = useState(0);
    const [archivedAtUtc, setArchivedAtUtc] = useState<string | null>(null);
    const [decommissionProgress, setDecommissionProgress] = useState(0);
    const [disclaimerModalOpen, setDisclaimerModalOpen] = useState(false);
    const [fiscalExportUrl, setFiscalExportUrl] = useState<string | null>(null);
    const [fiscalExportFileName, setFiscalExportFileName] = useState<string | null>(null);
    const [fiscalExportProgress, setFiscalExportProgress] = useState(0);
    const [lastExportedAtUtc, setLastExportedAtUtc] = useState<string | null>(null);

    const canAccess =
        isSuperAdmin(user?.role) || hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
    const canExportFiscalData = hasPermission(PERMISSIONS.REPORT_EXPORT);
    const canExportAuditPackage =
        hasPermission(PERMISSIONS.REPORT_EXPORT) && hasPermission(PERMISSIONS.AUDIT_VIEW);
    const canExportCompliancePackage =
        canExportAuditPackage && hasPermission(PERMISSIONS.FISCAL_EXPORT_COMPLIANCE);

    const tenantQuery = useQuery({
        queryKey: [...TENANT_DETAIL_QUERY_KEY, tenantId],
        queryFn: () => getAdminTenantById(tenantId),
        enabled: canAccess && !!tenantId,
    });

    const registersQuery = useQuery({
        queryKey: [...TENANT_DECOMMISSION_REGISTERS_QUERY_KEY, tenantId],
        queryFn: () => listAdminCashRegisters({ tenantId, page: 1, pageSize: 500 }),
        enabled: canAccess && !!tenantId,
    });

    const tenant = tenantQuery.data;
    const registers = registersQuery.data?.items ?? [];
    const registerSummary = useMemo(
        () => buildTenantDecommissionRegisterSummary(registers),
        [registers],
    );

    const eligibleRegisters = useMemo(
        () =>
            registers.filter((register) =>
                canDecommissionRegister(
                    typeof register.status === 'number' ? register.status : undefined,
                ),
            ),
        [registers],
    );

    const blockedRegisters = useMemo(
        () =>
            registers.filter((register) => {
                const status = typeof register.status === 'number' ? register.status : undefined;
                return !isDecommissionedRegister(status) && !canDecommissionRegister(status);
            }),
        [registers],
    );

    const remainingRegisters = useMemo(
        () =>
            registers.filter((register) => {
                const status = typeof register.status === 'number' ? register.status : undefined;
                return !isDecommissionedRegister(status);
            }),
        [registers],
    );

    const preflightChecks = useMemo(
        () => buildTenantDecommissionPreflightChecks(registerSummary, manualChecks),
        [manualChecks, registerSummary],
    );
    const canContinueFromPreflight =
        areTenantDecommissionChecksSatisfied(preflightChecks)
        && !tenantQuery.isLoading
        && !registersQuery.isLoading
        && !tenantQuery.isError
        && !registersQuery.isError
        && tenant?.status !== 'deleted';

    const canProceedToArchive = remainingRegisters.length === 0 && tenant?.status !== 'deleted';
    const canAdvanceFromFiscalExport = fiscalExportUrl != null || registers.length === 0;
    const tenantNameMatches =
        tenant != null
        && confirmTenantName === tenant.name;
    const archiveReady = canProceedToArchive && tenantNameMatches;
    const fiscalExportProfile = canExportCompliancePackage
        ? 'legal_compliance_export'
        : canExportAuditPackage
          ? 'accounting_report'
          : 'diagnostic_package';
    const fiscalExportFromUtc = tenant?.createdAt ?? new Date(Date.now() - 365 * 24 * 60 * 60 * 1000).toISOString();
    const fiscalExportToUtc = new Date().toISOString();

    useEffect(() => {
        return () => {
            if (fiscalExportUrl) {
                globalThis.URL.revokeObjectURL(fiscalExportUrl);
            }
        };
    }, [fiscalExportUrl]);

    const invalidateData = useCallback(async () => {
        await Promise.all([
            queryClient.invalidateQueries({ queryKey: [...TENANT_DETAIL_QUERY_KEY, tenantId] }),
            queryClient.invalidateQueries({ queryKey: [...TENANT_DECOMMISSION_REGISTERS_QUERY_KEY, tenantId] }),
            queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] }),
            queryClient.invalidateQueries({ queryKey: ['admin', 'cash-registers'] }),
        ]);
    }, [queryClient, tenantId]);

    const bulkDecommissionMutation = useMutation({
        mutationFn: async ({ ids, reason }: { ids: string[]; reason: string }) => {
            const results = [];
            for (let index = 0; index < ids.length; index += 1) {
                const id = ids[index];
                results.push(await decommissionCashRegister(id, { reason: reason || null }));
                setDecommissionProgress(Math.round(((index + 1) / ids.length) * 100));
            }
            return results;
        },
        onMutate: () => {
            setDecommissionProgress(0);
        },
        onSuccess: async (results) => {
            setLastDecommissionCount(results.length);
            setDecommissionProgress(100);
            message.success(t('tenants.decommission.messages.registersDecommissioned', { count: results.length }));
            await invalidateData();
            setCurrentStep(2);
        },
        onError: (error) => {
            message.error(
                getUserFacingApiErrorMessage(t, error, {
                    logContext: 'TenantDecommission.bulkDecommission',
                    fallbackKey: 'common.messages.unknownError',
                }),
            );
        },
    });

    const fiscalExportMutation = useMutation({
        mutationFn: async () => {
            const exportableRegisters = registers
                .filter((register) => typeof register.id === 'string' && register.id.length > 0);

            if (exportableRegisters.length === 0) {
                return null;
            }

            const zip = new JSZip();
            const tenantSlug = slugifyFilePart(tenant?.slug, 'tenant');
            const tenantName = slugifyFilePart(tenant?.name, 'tenant');
            const periodFrom = fiscalExportFromUtc.slice(0, 10);
            const periodTo = fiscalExportToUtc.slice(0, 10);

            for (let index = 0; index < exportableRegisters.length; index += 1) {
                const register = exportableRegisters[index];
                const generateResponse = await postApiAdminFiscalExportGenerate(
                    {
                        cashRegisterId: register.id,
                        fromUtc: fiscalExportFromUtc,
                        toUtc: fiscalExportToUtc,
                        includeCsv: true,
                        format: 'jsonDownload',
                        exportProfile: fiscalExportProfile,
                        lang: textLocale === 'en' ? 'en' : 'de',
                    },
                    {
                        headers: fiscalExportDisclaimerAckHeaders(),
                    },
                );

                if (!isDeferredFiscalExportResponse(generateResponse) || typeof generateResponse.exportId !== 'string') {
                    throw new Error('Fiscal export generation did not return a download ticket.');
                }

                const blob = await getApiAdminFiscalExportDownloadExportId(generateResponse.exportId, {
                    headers: fiscalExportDisclaimerAckHeaders(),
                });

                const registerNumber = slugifyFilePart(register.registerNumber, `register-${index + 1}`);
                zip.file(
                    `${tenantSlug}/${registerNumber}-${periodFrom}-${periodTo}.json`,
                    blob,
                );

                setFiscalExportProgress(Math.round(((index + 1) / exportableRegisters.length) * 90));
            }

            const zipBlob = await zip.generateAsync(
                { type: 'blob' },
                (metadata) => {
                    setFiscalExportProgress(90 + Math.round(metadata.percent * 0.1));
                },
            );

            return {
                blob: zipBlob,
                fileName: `fiscal-export-${tenantName}-${tenantSlug}-${periodFrom}-${periodTo}.zip`,
            };
        },
        onMutate: () => {
            setFiscalExportProgress(0);
        },
        onSuccess: (result) => {
            if (result == null) {
                setLastExportedAtUtc(new Date().toISOString());
                setFiscalExportProgress(100);
                return;
            }

            if (fiscalExportUrl) {
                globalThis.URL.revokeObjectURL(fiscalExportUrl);
            }

            const nextUrl = globalThis.URL.createObjectURL(result.blob);
            setFiscalExportUrl(nextUrl);
            setFiscalExportFileName(result.fileName);
            setLastExportedAtUtc(new Date().toISOString());
            setFiscalExportProgress(100);
            message.success(t('tenants.decommission.export.messages.generated'));
        },
        onError: (error) => {
            message.error(
                getUserFacingApiErrorMessage(t, error, {
                    logContext: 'TenantDecommission.fiscalExport',
                    fallbackKey: 'common.messages.unknownError',
                }),
            );
        },
    });

    const archiveMutation = useMutation({
        mutationFn: () => softDeleteAdminTenant(tenantId),
        onSuccess: async () => {
            setArchivedAtUtc(new Date().toISOString());
            message.success(t('tenants.decommission.messages.archived'));
            await invalidateData();
            setCurrentStep(4);
        },
        onError: (error) => {
            message.error(
                getUserFacingApiErrorMessage(t, error, {
                    logContext: 'TenantDecommission.archiveTenant',
                    fallbackKey: 'tenants.messages.deleteFailed',
                }),
            );
        },
    });

    const columns = useMemo<ColumnsType<AdminCashRegisterListItem>>(
        () => [
            {
                title: t('tenants.decommission.registers.columns.registerNumber'),
                dataIndex: 'registerNumber',
                key: 'registerNumber',
                render: (value?: string | null) => value?.trim() || '—',
            },
            {
                title: t('tenants.decommission.registers.columns.location'),
                dataIndex: 'location',
                key: 'location',
                render: (value?: string | null) => value?.trim() || '—',
            },
            {
                title: t('tenants.decommission.registers.columns.status'),
                dataIndex: 'status',
                key: 'status',
                render: (status?: number) => (
                    <Tag color={registerStatusTagColor(status)}>
                        {formatRegisterStatus(status, t)}
                    </Tag>
                ),
            },
            {
                title: t('tenants.decommission.registers.columns.lastActivity'),
                dataIndex: 'lastBalanceUpdate',
                key: 'lastBalanceUpdate',
                render: (value?: string | null) =>
                    value ? formatDateTime(value, formatLocale) : '—',
            },
            {
                title: t('tenants.decommission.registers.columns.readiness'),
                key: 'readiness',
                render: (_, record) => {
                    const status =
                        typeof record.status === 'number' ? record.status : undefined;
                    if (isDecommissionedRegister(status)) {
                        return (
                            <Typography.Text type="secondary">
                                {t('tenants.decommission.registers.readiness.decommissioned')}
                            </Typography.Text>
                        );
                    }
                    if (canDecommissionRegister(status)) {
                        return (
                            <Typography.Text>
                                {t('tenants.decommission.registers.readiness.ready')}
                            </Typography.Text>
                        );
                    }
                    return (
                        <Typography.Text type="danger">
                            {t('tenants.decommission.registers.readiness.blocked')}
                        </Typography.Text>
                    );
                },
            },
        ],
        [formatLocale, t],
    );
    const auditHref = buildTenantAuditLogsHref(tenantId);

    const goToStep = useCallback((stepIndex: number) => {
        setCurrentStep(Math.max(0, Math.min(stepIndex, 4)));
    }, []);

    const handleTogglePreflightCheck = useCallback(
        (check: TenantDecommissionPreflightCheck, next: boolean) => {
            setManualChecks((current) => {
                if (check.key === 'noOpenPayments') {
                    return {
                        ...current,
                        noOpenPayments: next,
                    };
                }
                if (check.key === 'dailyClosingDone') {
                    return {
                        ...current,
                        dailyClosingDone: next,
                    };
                }
                return {
                    ...current,
                    fiscalExportAcknowledged: next,
                };
            });
        },
        [],
    );

    const handleDecommissionAll = useCallback(() => {
        bulkDecommissionMutation.mutate({
            ids: eligibleRegisters
                .map((register) => register.id)
                .filter((value): value is string => typeof value === 'string' && value.length > 0),
            reason:
                decommissionReason.trim()
                || t('tenants.decommission.registers.defaultReason'),
        });
    }, [bulkDecommissionMutation, decommissionReason, eligibleRegisters, t]);

    const handleGenerateFiscalExport = useCallback(() => {
        if (isFiscalExportDisclaimerSkipped()) {
            fiscalExportMutation.mutate();
            return;
        }
        setDisclaimerModalOpen(true);
    }, [fiscalExportMutation]);

    const steps = useMemo(
        () => [
            { title: t('tenants.decommission.steps.preflight'), component: DecommissionWizardStep1 },
            { title: t('tenants.decommission.steps.registers'), component: DecommissionWizardStep2 },
            { title: t('tenants.decommission.steps.export'), component: DecommissionWizardStep3 },
            { title: t('tenants.decommission.steps.archive'), component: DecommissionWizardStep4 },
            { title: t('tenants.decommission.steps.result'), component: DecommissionWizardStep5 },
        ],
        [t],
    );

    const CurrentStepComponent = steps[currentStep]?.component ?? DecommissionWizardStep1;

    const stepProps: DecommissionWizardStepProps = {
        t,
        formatLocale,
        tenantId,
        tenant,
        registers,
        columns,
        registerSummary,
        preflightChecks,
        manualChecks,
        onTogglePreflightCheck: handleTogglePreflightCheck,
        canContinueFromPreflight,
        canDecommissionCashRegisters,
        blockedRegisters,
        canProceedToArchive,
        decommissionReason,
        onDecommissionReasonChange: setDecommissionReason,
        bulkDecommissionPending: bulkDecommissionMutation.isPending,
        decommissionProgress,
        onDecommissionAll: handleDecommissionAll,
        canExportFiscalData,
        fiscalExportProfile,
        fiscalExportFromUtc,
        fiscalExportToUtc,
        fiscalExportPending: fiscalExportMutation.isPending,
        fiscalExportProgress,
        fiscalExportUrl,
        fiscalExportFileName,
        lastExportedAtUtc,
        onGenerateFiscalExport: handleGenerateFiscalExport,
        canAdvanceFromFiscalExport,
        confirmTenantName,
        onConfirmTenantNameChange: setConfirmTenantName,
        tenantNameMatches,
        archivePending: archiveMutation.isPending,
        archiveReady,
        onArchive: () => archiveMutation.mutate(),
        archivedAtUtc,
        lastDecommissionCount,
        auditHref,
        onNext: () => goToStep(currentStep + 1),
        onPrevious: () => goToStep(currentStep - 1),
    };

    if (!canAccess) {
        return (
            <AdminPageShell>
                <Alert
                    type="error"
                    title={t('tenants.accessDenied.title')}
                    description={t('tenants.accessDenied.body')}
                />
            </AdminPageShell>
        );
    }

    if (!tenantId) {
        return (
            <AdminPageShell>
                <Alert type="error" title={t('tenants.users.errors.invalidTenant')} />
            </AdminPageShell>
        );
    }

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={
                    tenant
                        ? t('tenants.decommission.pageTitleWithTenant', { name: tenant.name })
                        : t('tenants.decommission.pageTitle')
                }
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t(ADMIN_NAV_LABEL_KEYS.settingsHub), href: '/settings' },
                    { title: t('tenants.page.title'), href: '/admin/tenants' },
                    tenant
                        ? { title: `${tenant.name} (${tenant.slug})`, href: `/admin/tenants/${tenantId}` }
                        : { title: tenantId, href: `/admin/tenants/${tenantId}` },
                    {
                        title: t('tenants.decommission.pageTitle'),
                        href: `/admin/tenants/${tenantId}/decommission`,
                    },
                ]}
                actions={
                    <Space wrap>
                        <Link href={`/admin/tenants/${tenantId}?tab=settings`}>
                            <Button icon={<ArrowLeftOutlined />}>
                                {t('tenants.decommission.actions.backToTenant')}
                            </Button>
                        </Link>
                        <Button icon={<ReloadOutlined />} onClick={() => void invalidateData()}>
                            {t('common.refresh')}
                        </Button>
                    </Space>
                }
            />

            <Space orientation="vertical" size="large" style={{ width: '100%' }}>
                <Alert
                    type="error"
                    showIcon
                    title={t('tenants.decommission.criticalTitle')}
                    description={t('tenants.decommission.criticalBody')}
                />

                {tenant?.status === 'deleted' ? (
                    <Alert
                        type="warning"
                        showIcon
                        title={t('tenants.decommission.alreadyArchivedTitle')}
                        description={t('tenants.decommission.alreadyArchivedBody')}
                    />
                ) : null}

                {tenantQuery.isError ? (
                    <Alert
                        type="error"
                        showIcon
                        title={t('tenants.users.errors.tenantNotFound')}
                    />
                ) : null}

                {registersQuery.isError ? (
                    <Alert
                        type="error"
                        showIcon
                        title={t('tenants.decommission.registers.loadFailed')}
                    />
                ) : null}

                <Card loading={(tenantQuery.isLoading && !tenant) || registersQuery.isLoading}>
                    <Steps
                        current={currentStep}
                        style={{ marginBottom: 24 }}
                        items={steps.map((step) => ({ title: step.title }))}
                    />
                    <CurrentStepComponent {...stepProps} />
                </Card>
            </Space>
            <FiscalExportDisclaimerModal
                open={disclaimerModalOpen}
                onCancel={() => setDisclaimerModalOpen(false)}
                onConfirm={({ skip24h }) => {
                    setDisclaimerModalOpen(false);
                    if (skip24h) {
                        setFiscalExportDisclaimerSkip24h();
                    }
                    fiscalExportMutation.mutate();
                }}
            />
        </AdminPageShell>
    );
}
