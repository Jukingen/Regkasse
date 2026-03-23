'use client';

import React, { useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    Checkbox,
    DatePicker,
    List,
    Select,
    Space,
    Spin,
    Tag,
    Typography,
} from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import {
    downloadFiscalExportJson,
    extractApiErrorMessage,
    getAdminCashRegisters,
    getFiscalExportPreview,
} from '@/api/admin-rksv/client';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import type { GetApiAdminFiscalExportParams } from '@/api/generated/model';

const { RangePicker } = DatePicker;

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
    const [cashRegisterId, setCashRegisterId] = useState<string | undefined>(undefined);
    const [includeCsv, setIncludeCsv] = useState<boolean>(false);
    const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null]>([
        dayjs().subtract(1, 'day'),
        dayjs(),
    ]);

    const [exportLoading, setExportLoading] = useState(false);
    const [exportError, setExportError] = useState<string | null>(null);
    const [preview, setPreview] = useState<FiscalExportPackage | null>(null);

    const { data: cashRegisters, isLoading: cashLoading } = useQuery({
        queryKey: rksvAdminQueryKeys.cashRegisters,
        queryFn: getAdminCashRegisters,
        staleTime: 60_000,
    });

    const fromUtc = dateRange?.[0]?.toISOString();
    const toUtc = dateRange?.[1]?.toISOString();

    const canRun = Boolean(cashRegisterId && fromUtc && toUtc && !exportLoading);

    const params = useMemo<GetApiAdminFiscalExportParams>(() => {
        return {
            cashRegisterId,
            fromUtc,
            toUtc,
            includeCsv,
        };
    }, [cashRegisterId, fromUtc, toUtc, includeCsv]);

    const runPreview = async () => {
        if (!cashRegisterId || !fromUtc || !toUtc) {
            setExportError('Bitte Kasse und UTC Zeitraum auswählen.');
            return;
        }

        setExportLoading(true);
        setExportError(null);

        try {
            const data = await getFiscalExportPreview(params);
            setPreview(data);
        } catch (e) {
            const msg = extractApiErrorMessage(e, 'Export failed');
            setExportError(msg);
            setPreview(null);
        } finally {
            setExportLoading(false);
        }
    };

    const runDownload = async () => {
        if (!cashRegisterId || !fromUtc || !toUtc) {
            setExportError('Bitte Kasse und UTC Zeitraum auswählen.');
            return;
        }

        setExportLoading(true);
        setExportError(null);

        try {
            const blob = await downloadFiscalExportJson(params);
            const fileName = `fiscal-export-${cashRegisterId}-${fromUtc.slice(0, 10)}-${toUtc.slice(0, 10)}.json`;

            // Avoid downloading backend error payloads (JSON error envelope).
            const maybeText = await blob.text();
            try {
                const parsed = JSON.parse(maybeText);
                if (typeof parsed?.message === 'string' && typeof parsed?.code === 'string') {
                    setExportError(parsed.message);
                    return;
                }
            } catch {
                // Not an error envelope; proceed with download.
            }

            downloadBlob(blob, fileName);
        } catch (e) {
            const msg = extractApiErrorMessage(e, 'Download failed');
            setExportError(msg);
        } finally {
            setExportLoading(false);
        }
    };

    const previewJson = preview ? JSON.stringify(preview, null, 2) : '';

    return (
        <>
            <AdminPageHeader
                title="Fiscal-Export"
                breadcrumbs={[
                    ADMIN_OVERVIEW_CRUMB,
                    { title: 'RKSV', href: '/rksv' },
                    { title: 'Fiscal-Export Diagnose' },
                ]}
            />

            <Alert
                type="info"
                showIcon
                style={{ marginBottom: 16 }}
                message="Ergänzung: globale Integritätsprüfung"
                description={
                    <span>
                        Export-Integrität bezieht sich auf den gewählten Slice. Für datenbankweite Konsistenz (Duplikate,
                        Sequenzen, Refunds, Zahlung ohne Rechnung) siehe{' '}
                        <Link href="/rksv/integrity">Datenintegrität (Support)</Link>.
                    </span>
                }
            />

            {cashLoading ? (
                <div style={{ textAlign: 'center', padding: 80 }}>
                    <Spin size="large" />
                </div>
            ) : null}

            {exportError ? (
                <Alert
                    type="error"
                    showIcon
                    style={{ marginBottom: 16 }}
                    message="Export fehlgeschlagen"
                    description={exportError}
                />
            ) : null}

            <Card size="small" style={{ marginBottom: 16 }}>
                <Space wrap>
                    <div>
                        <Typography.Text strong>Cash Register</Typography.Text>
                        <br />
                        <Select
                            placeholder="Wähle Kasse"
                            style={{ minWidth: 280 }}
                            allowClear
                            value={cashRegisterId}
                            onChange={(v) => setCashRegisterId(v)}
                            options={(cashRegisters ?? [])
                                .filter((r) => typeof r.id === 'string' && r.id.length > 0)
                                .map((r) => ({
                                    value: r.id as string,
                                    label: r.registerNumber ? `${r.registerNumber} (${(r.id as string).slice(0, 8)}…)` : (r.id as string),
                                }))}
                        />
                    </div>

                    <div>
                        <Typography.Text strong>UTC Zeitraum</Typography.Text>
                        <br />
                        <RangePicker
                            showTime
                            value={[dateRange[0] ?? null, dateRange[1] ?? null]}
                            onChange={(dates) => setDateRange(dates as [Dayjs | null, Dayjs | null])}
                        />
                    </div>

                    <div>
                        <Typography.Text strong>Include CSV</Typography.Text>
                        <br />
                        <Checkbox checked={includeCsv} onChange={(e) => setIncludeCsv(e.target.checked)}>
                            CSV Fragmente im JSON-Payload
                        </Checkbox>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'flex-end' }}>
                        <Space>
                            <Button type="primary" onClick={runPreview} loading={exportLoading} disabled={!canRun}>
                                JSON Vorschau
                            </Button>
                            <Button onClick={runDownload} loading={exportLoading} disabled={!canRun}>
                                JSON herunterladen
                            </Button>
                        </Space>
                    </div>
                </Space>
            </Card>

            {preview ? (
                <>
                    {preview.notLegalProofNotice != null ? (
                        <Alert
                            type="warning"
                            showIcon
                            style={{ marginBottom: 16 }}
                            message={preview.notLegalProofNotice || 'NOT LEGAL PROOF'}
                            description="Bitte den Export ausschließlich zur Diagnose/Auswertung verwenden (kein Rechtsnachweis)."
                        />
                    ) : null}

                    <Card size="small" style={{ marginBottom: 16 }} title="Summary">
                        <Space wrap>
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
                        <Card size="small" style={{ marginBottom: 16 }} title="ExportScopeWarnings">
                            <List
                                size="small"
                                dataSource={preview.exportScopeWarnings}
                                renderItem={(w) => <List.Item>{w}</List.Item>}
                            />
                        </Card>
                    ) : null}

                    {preview.chainContinuityWarnings?.length ? (
                        <Card size="small" style={{ marginBottom: 16 }} title="ChainContinuityWarnings">
                            <Alert
                                type="warning"
                                showIcon
                                style={{ marginBottom: 12 }}
                                message={`${preview.chainContinuityWarnings.length} Warnung(en) gefunden`}
                            />
                            <List
                                size="small"
                                dataSource={preview.chainContinuityWarnings}
                                renderItem={(w) => <List.Item>{w}</List.Item>}
                            />
                        </Card>
                    ) : null}

                    {preview.integrity ? (
                        <Card size="small" style={{ marginBottom: 16 }} title="Integrity diagnostics">
                            {preview.integrity.lowCoverageAlert ? (
                                <Alert
                                    type="warning"
                                    showIcon
                                    style={{ marginBottom: 12 }}
                                    message="Low offline-intent coverage"
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
                                    message="Legacy payload-hash quality risk"
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
                                    header="IntegrityDiagnosticNotes"
                                    dataSource={preview.integrity.integrityDiagnosticNotes}
                                    renderItem={(n) => <List.Item>{n}</List.Item>}
                                />
                            ) : null}
                        </Card>
                    ) : null}

                    <Card size="small" title="JSON Preview">
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
                <Card size="small" title="Hinweis" style={{ marginTop: 16 }}>
                    Wähle eine Kasse und einen UTC Zeitraum und starte anschließend „JSON Vorschau“ oder „JSON
                    herunterladen“.
                </Card>
            ) : null}
        </>
    );
}

