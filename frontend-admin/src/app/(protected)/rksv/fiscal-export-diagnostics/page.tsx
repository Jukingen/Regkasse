'use client';

/**
 * Fiscal export ekranı: exportProfile ile tanılama / audit devri / uyum paketi ayrımı (RBAC + açıklayıcı metinler).
 */

import React, { useEffect, useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    Checkbox,
    DatePicker,
    Segmented,
    Select,
    Space,
    Tag,
    Typography,
} from 'antd';
import { SimpleList as List } from '@/components/ui/SimpleList';
import type { SegmentedValue } from 'antd/es/segmented';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { CardSkeleton } from '@/components/Skeleton';
import { ADMIN_NAV_GROUP_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n/I18nProvider';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import {
    downloadFiscalExportJson,
    extractApiErrorMessage,
    getAdminCashRegisters,
    getFiscalExportPreview,
} from '@/api/admin-rksv/client';
import { FiscalExportDisclaimerModal } from '@/features/rksv/components/FiscalExportDisclaimerModal';
import {
    isFiscalExportDisclaimerSkipped,
    setFiscalExportDisclaimerSkip24h,
} from '@/features/rksv/fiscalExportDisclaimerSession';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import type { GetApiAdminFiscalExportParams } from '@/api/generated/model';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';

const { RangePicker } = DatePicker;

type ExportProfileValue = 'diagnostic' | 'audit_handoff' | 'compliance';

type FiscalExportIntegrity = {
    signatureChainValid?: boolean;
    receiptSignatureLinkageOkInExportOrder?: boolean;
    sequenceContinuous?: boolean;
    belegSequenceContiguousInExportedOrderPerDay?: boolean;
    offlineReplayGaps?: number;
    totalOfflineTransactions?: number;
    syncedOfflineTransactions?: number;
    failedOfflineTransactions?: number;
    offlineIntentCoverageTotal?: number;
    offlineIntentCoverageWithDeviceId?: number;
    offlineIntentCoverageWithSequence?: number;
    deviceIdCoveragePercent?: number | null;
    sequenceCoveragePercent?: number | null;
    lowCoverageAlert?: boolean;
    offlineMetricsScopedToPeriod?: boolean;
    legacyDataQualityRiskHigh?: boolean;
    legacyPayloadHashMismatchRatioPercent?: number | null;
    integrityDiagnosticNotes?: string[];
};

type FiscalExportPackage = {
    schemaVersion?: string;
    notLegalProofNotice?: string;
    exportProfile?: string;
    exportProfileIntentNotice?: string;
    generatedAtUtc?: string;
    cashRegisterId?: string;
    registerNumber?: string;
    registerLocation?: string;
    period?: { fromUtc?: string; toUtc?: string };

    receiptsCsv?: string | null;
    closingsCsv?: string | null;
    receiptCount?: number;
    closingCount?: number;
    totalReceiptsMatchingPeriod?: number;
    receiptsTruncated?: boolean;

    exportScopeWarnings?: string[];
    chainContinuityWarnings?: string[];

    integrity?: FiscalExportIntegrity;
};

function downloadBlob(blob: unknown, fileName: string) {
    const url = globalThis.URL.createObjectURL(blob as unknown as globalThis.Blob);
    const a = globalThis.document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();
    globalThis.URL.revokeObjectURL(url);
}

export default function FiscalExportDiagnosticsPage() {
    const { t } = useI18n();
    const { hasPermission } = usePermissions();

    const canDiagnostic = hasPermission(PERMISSIONS.REPORT_EXPORT);
    const canAuditHandoff =
        hasPermission(PERMISSIONS.REPORT_EXPORT) && hasPermission(PERMISSIONS.AUDIT_VIEW);
    const canCompliance =
        hasPermission(PERMISSIONS.REPORT_EXPORT) &&
        hasPermission(PERMISSIONS.AUDIT_VIEW) &&
        hasPermission(PERMISSIONS.FISCAL_EXPORT_COMPLIANCE);

    const [exportProfile, setExportProfile] = useState<ExportProfileValue>('diagnostic');

    useEffect(() => {
        if (exportProfile === 'audit_handoff' && !canAuditHandoff) setExportProfile('diagnostic');
        if (exportProfile === 'compliance' && !canCompliance) setExportProfile('diagnostic');
    }, [exportProfile, canAuditHandoff, canCompliance]);

    const [cashRegisterId, setCashRegisterId] = useState<string | undefined>(undefined);
    const [includeCsv, setIncludeCsv] = useState<boolean>(false);
    const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null]>([
        dayjs().subtract(1, 'day'),
        dayjs(),
    ]);

    const [exportLoading, setExportLoading] = useState(false);
    const [exportError, setExportError] = useState<string | null>(null);
    const [preview, setPreview] = useState<FiscalExportPackage | null>(null);
    const [disclaimerModalOpen, setDisclaimerModalOpen] = useState(false);
    const [pendingExportAction, setPendingExportAction] = useState<'preview' | 'download' | null>(null);

    const { data: cashRegisters, isLoading: cashLoading } = useQuery({
        queryKey: rksvAdminQueryKeys.cashRegisters,
        queryFn: getAdminCashRegisters,
        staleTime: 60_000,
    });

    const fromUtc = dateRange?.[0]?.toISOString();
    const toUtc = dateRange?.[1]?.toISOString();

    const canRun = Boolean(cashRegisterId && fromUtc && toUtc && !exportLoading && canDiagnostic);

    const params = useMemo<GetApiAdminFiscalExportParams>(() => {
        return {
            cashRegisterId,
            fromUtc,
            toUtc,
            includeCsv,
            exportProfile,
        };
    }, [cashRegisterId, fromUtc, toUtc, includeCsv, exportProfile]);

    const profileBlocked =
        (exportProfile === 'audit_handoff' && !canAuditHandoff) ||
        (exportProfile === 'compliance' && !canCompliance);

    const runPreview = async () => {
        if (!cashRegisterId || !fromUtc || !toUtc) {
            setExportError(t('rksvHub.fiscalExportPage.selectRegisterAndUtcError'));
            return;
        }
        if (profileBlocked) {
            setExportError(t('rksvHub.fiscalExportPage.profileForbidden'));
            return;
        }

        setExportLoading(true);
        setExportError(null);

        try {
            const data = (await getFiscalExportPreview(params)) as FiscalExportPackage;
            setPreview(data);
        } catch (e) {
            const msg = extractApiErrorMessage(e, t('rksvHub.fiscalExportPage.exportFailedAlert'));
            setExportError(msg);
            setPreview(null);
        } finally {
            setExportLoading(false);
        }
    };

    const runDownload = async () => {
        if (!cashRegisterId || !fromUtc || !toUtc) {
            setExportError(t('rksvHub.fiscalExportPage.selectRegisterAndUtcError'));
            return;
        }
        if (profileBlocked) {
            setExportError(t('rksvHub.fiscalExportPage.profileForbidden'));
            return;
        }

        setExportLoading(true);
        setExportError(null);

        try {
            const blob = await downloadFiscalExportJson(params);
            const fileName = `fiscal-export-${exportProfile}-${cashRegisterId}-${fromUtc.slice(0, 10)}-${toUtc.slice(0, 10)}.json`;

            const maybeText = await blob.text();
            try {
                const parsed = JSON.parse(maybeText) as { message?: string; code?: string };
                if (typeof parsed?.message === 'string' && typeof parsed?.code === 'string') {
                    setExportError(parsed.message);
                    return;
                }
            } catch {
                // Not an error envelope; proceed with download.
            }

            downloadBlob(new Blob([maybeText], { type: 'application/json' }), fileName);
        } catch (e) {
            const msg = extractApiErrorMessage(e, 'Download failed');
            setExportError(msg);
        } finally {
            setExportLoading(false);
        }
    };

    const requestPreview = () => {
        if (!cashRegisterId || !fromUtc || !toUtc) {
            setExportError(t('rksvHub.fiscalExportPage.selectRegisterAndUtcError'));
            return;
        }
        if (profileBlocked) {
            setExportError(t('rksvHub.fiscalExportPage.profileForbidden'));
            return;
        }
        if (isFiscalExportDisclaimerSkipped()) {
            void runPreview();
            return;
        }
        setPendingExportAction('preview');
        setDisclaimerModalOpen(true);
    };

    const requestDownload = () => {
        if (!cashRegisterId || !fromUtc || !toUtc) {
            setExportError(t('rksvHub.fiscalExportPage.selectRegisterAndUtcError'));
            return;
        }
        if (profileBlocked) {
            setExportError(t('rksvHub.fiscalExportPage.profileForbidden'));
            return;
        }
        if (isFiscalExportDisclaimerSkipped()) {
            void runDownload();
            return;
        }
        setPendingExportAction('download');
        setDisclaimerModalOpen(true);
    };

    const handleDisclaimerConfirm = ({ skip24h }: { skip24h: boolean }) => {
        setDisclaimerModalOpen(false);
        if (skip24h) {
            setFiscalExportDisclaimerSkip24h();
        }
        const next = pendingExportAction;
        setPendingExportAction(null);
        if (next === 'preview') {
            void runPreview();
        } else if (next === 'download') {
            void runDownload();
        }
    };

    const handleDisclaimerCancel = () => {
        setDisclaimerModalOpen(false);
        setPendingExportAction(null);
    };

    const previewJson = preview ? JSON.stringify(preview, null, 2) : '';

    const segmentedOptions = useMemo(
        () => [
            {
                label: t('rksvHub.fiscalExportPage.diagnostic'),
                value: 'diagnostic' as const,
                disabled: !canDiagnostic,
            },
            {
                label: t('rksvHub.fiscalExportPage.auditHandoff'),
                value: 'audit_handoff' as const,
                disabled: !canAuditHandoff,
            },
            {
                label: t('rksvHub.fiscalExportPage.compliance'),
                value: 'compliance' as const,
                disabled: !canCompliance,
            },
        ],
        [t, canDiagnostic, canAuditHandoff, canCompliance],
    );

    const onProfileChange = (v: SegmentedValue) => {
        setExportProfile(v as ExportProfileValue);
    };

    return (
        <>
            <AdminPageHeader
                title={t('rksvHub.fiscalExportPage.title')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t(ADMIN_NAV_GROUP_LABEL_KEYS.rksv), href: '/rksv' },
                    { title: t('rksvHub.fiscalExportPage.breadcrumb') },
                ]}
            />

            <Alert
                type="info"
                showIcon
                style={{ marginBottom: 16 }}
                title={t('rksvHub.fiscalExportPage.sliceVsGlobalTitle')}
                description={
                    <span>
                        {t('rksvHub.fiscalExportPage.globalIntegrityHint')}{' '}
                        <Link href="/rksv/integrity">{t('rksvHub.link.integrity')}</Link>.
                    </span>
                }
            />

            <Alert type="info" showIcon style={{ marginBottom: 16 }} title={t('rksvHub.fiscalExportPage.profileHelp')} />

            {cashLoading ? (
                <CardSkeleton count={1} />
            ) : null}

            {exportError ? (
                <Alert
                    type="error"
                    showIcon
                    style={{ marginBottom: 16 }}
                    title={t('rksvHub.fiscalExportPage.exportFailedAlert')}
                    description={exportError}
                />
            ) : null}

            <Card size="small" style={{ marginBottom: 16 }}>
                <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                    <div>
                        <Typography.Text strong>{t('rksvHub.fiscalExportPage.profileLabel')}</Typography.Text>
                        <div style={{ marginTop: 8 }}>
                            <Segmented
                                options={segmentedOptions}
                                value={exportProfile}
                                onChange={onProfileChange}
                            />
                        </div>
                        <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                            {exportProfile === 'diagnostic' ? t('rksvHub.fiscalExportPage.diagnosticDesc') : null}
                            {exportProfile === 'audit_handoff' ? t('rksvHub.fiscalExportPage.auditHandoffDesc') : null}
                            {exportProfile === 'compliance' ? t('rksvHub.fiscalExportPage.complianceDesc') : null}
                        </Typography.Paragraph>
                    </div>

                    <Space wrap>
                        <div>
                            <Typography.Text strong>{t('rksvHub.fiscalExportPage.cashRegisterLabel')}</Typography.Text>
                            <br />
                            <Select
                                placeholder={t('rksvHub.fiscalExportPage.cashRegisterPlaceholder')}
                                style={{ minWidth: 280 }}
                                allowClear
                                value={cashRegisterId}
                                onChange={(v) => setCashRegisterId(v)}
                                options={(cashRegisters ?? [])
                                    .filter((r) => typeof r.id === 'string' && r.id.length > 0)
                                    .map((r) => ({
                                        value: r.id as string,
                                        label: r.registerNumber
                                            ? `${r.registerNumber} (${(r.id as string).slice(0, 8)}…)`
                                            : (r.id as string),
                                    }))}
                            />
                        </div>

                        <div>
                            <Typography.Text strong>{t('rksvHub.fiscalExportPage.utcRangeLabel')}</Typography.Text>
                            <br />
                            <RangePicker format={DAYJS_DATE_FORMAT}
                                showTime
                                value={[dateRange[0] ?? null, dateRange[1] ?? null]}
                                onChange={(dates) => setDateRange(dates as [Dayjs | null, Dayjs | null])}
                            />
                        </div>

                        <div>
                            <Typography.Text strong>{t('rksvHub.fiscalExportPage.includeCsvLabel')}</Typography.Text>
                            <br />
                            <Checkbox checked={includeCsv} onChange={(e) => setIncludeCsv(e.target.checked)}>
                                {t('rksvHub.fiscalExportPage.csvFragmentsCheckbox')}
                            </Checkbox>
                        </div>

                        <div style={{ display: 'flex', alignItems: 'flex-end' }}>
                            <Space>
                                <Button
                                    type="primary"
                                    onClick={requestPreview}
                                    loading={exportLoading}
                                    disabled={!canRun || profileBlocked}
                                >
                                    {t('rksvHub.fiscalExportPage.previewJsonButton')}
                                </Button>
                                <Button onClick={requestDownload} loading={exportLoading} disabled={!canRun || profileBlocked}>
                                    {t('rksvHub.fiscalExportPage.downloadJsonButton')}
                                </Button>
                            </Space>
                        </div>
                    </Space>
                </Space>
            </Card>

            {preview ? (
                <>
                    {preview.exportProfileIntentNotice != null ? (
                        <Alert
                            type="warning"
                            showIcon
                            style={{ marginBottom: 16 }}
                            title={preview.exportProfile ?? 'export'}
                            description={
                                <span>
                                    <Tag>{preview.exportProfile}</Tag> {preview.exportProfileIntentNotice}
                                </span>
                            }
                        />
                    ) : null}
                    {preview.notLegalProofNotice != null ? (
                        <Alert
                            type="warning"
                            showIcon
                            style={{ marginBottom: 16 }}
                            title={preview.notLegalProofNotice || t('rksvHub.fiscalExportPage.notLegalProofFallback')}
                            description={t('rksvHub.fiscalExportPage.statutoryDisclaimerBody')}
                        />
                    ) : null}

                    <Card size="small" style={{ marginBottom: 16 }} title={t('rksvHub.fiscalExportPage.summaryCardTitle')}>
                        <Space wrap>
                            <Tag color="blue">exportProfile: {preview.exportProfile ?? '—'}</Tag>
                            <Tag color="blue">receiptCount: {preview.receiptCount ?? 0}</Tag>
                            <Tag color="blue">closingCount: {preview.closingCount ?? 0}</Tag>
                            {typeof preview.totalReceiptsMatchingPeriod === 'number' ? (
                                <Tag color={preview.receiptsTruncated ? 'orange' : 'green'}>
                                    totalReceiptsMatchingPeriod: {preview.totalReceiptsMatchingPeriod}
                                </Tag>
                            ) : null}
                            {preview.receiptsTruncated ? (
                                <Tag color="orange">receiptsTruncated=true</Tag>
                            ) : (
                                <Tag color="green">receiptsTruncated=false</Tag>
                            )}
                        </Space>
                    </Card>

                    {preview.exportScopeWarnings?.length ? (
                        <Card size="small" style={{ marginBottom: 16 }} title={t('rksvHub.fiscalExportPage.exportScopeWarningsCardTitle')}>
                            <List
                                size="small"
                                dataSource={preview.exportScopeWarnings}
                                renderItem={(w) => <List.Item>{w}</List.Item>}
                            />
                        </Card>
                    ) : null}

                    {preview.chainContinuityWarnings?.length ? (
                        <Card size="small" style={{ marginBottom: 16 }} title={t('rksvHub.fiscalExportPage.chainContinuityWarningsCardTitle')}>
                            <Alert
                                type="warning"
                                showIcon
                                style={{ marginBottom: 12 }}
                                title={`${preview.chainContinuityWarnings.length} Warnung(en) gefunden`}
                            />
                            <List
                                size="small"
                                dataSource={preview.chainContinuityWarnings}
                                renderItem={(w) => <List.Item>{w}</List.Item>}
                            />
                        </Card>
                    ) : null}

                    {preview.integrity ? (
                        <Card size="small" style={{ marginBottom: 16 }} title={t('rksvHub.fiscalExportPage.integrityDiagnosticsCardTitle')}>
                            {preview.integrity.lowCoverageAlert ? (
                                <Alert
                                    type="warning"
                                    showIcon
                                    style={{ marginBottom: 12 }}
                                    title={t('rksvHub.fiscalExportPage.lowOfflineIntentCoverageTitle')}
                                    description={`DeviceId: ${
                                        preview.integrity.deviceIdCoveragePercent != null
                                            ? preview.integrity.deviceIdCoveragePercent.toFixed(1) + '%'
                                            : '—'
                                    }, Sequence: ${
                                        preview.integrity.sequenceCoveragePercent != null
                                            ? preview.integrity.sequenceCoveragePercent.toFixed(1) + '%'
                                            : '—'
                                    }`}
                                />
                            ) : null}

                            {preview.integrity.legacyDataQualityRiskHigh ? (
                                <Alert
                                    type="warning"
                                    showIcon
                                    style={{ marginBottom: 12 }}
                                    title={t('rksvHub.fiscalExportPage.legacyPayloadHashQualityTitle')}
                                    description={`Mismatch ratio: ${
                                        preview.integrity.legacyPayloadHashMismatchRatioPercent != null
                                            ? preview.integrity.legacyPayloadHashMismatchRatioPercent.toFixed(1) + '%'
                                            : '—'
                                    }`}
                                />
                            ) : null}

                            {preview.integrity.integrityDiagnosticNotes?.length ? (
                                <List
                                    size="small"
                                    header={t('rksvHub.fiscalExportPage.integrityDiagnosticNotesHeader')}
                                    dataSource={preview.integrity.integrityDiagnosticNotes}
                                    renderItem={(n) => <List.Item>{n}</List.Item>}
                                />
                            ) : null}
                        </Card>
                    ) : null}

                    <Card size="small" title={t('rksvHub.fiscalExportPage.jsonPreviewCardTitle')}>
                        <pre
                            style={{
                                fontSize: 11,
                                maxHeight: 520,
                                overflow: 'auto',
                                background: '#f5f5f5',
                                padding: 12,
                                borderRadius: 6,
                            }}
                        >
                            {previewJson}
                        </pre>
                    </Card>
                </>
            ) : null}

            {!exportLoading && !preview ? (
                <Card size="small" title={t('rksvHub.fiscalExportPage.emptyHintCardTitle')} style={{ marginTop: 16 }}>
                    {t('rksvHub.fiscalExportPage.emptyHintCardBody')}
                </Card>
            ) : null}

            <FiscalExportDisclaimerModal
                open={disclaimerModalOpen}
                onCancel={handleDisclaimerCancel}
                onConfirm={handleDisclaimerConfirm}
            />
        </>
    );
}
