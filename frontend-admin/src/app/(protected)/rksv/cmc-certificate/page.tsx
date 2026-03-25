'use client';

import React from 'react';
import { Alert, Button, Card, Descriptions, Divider, Spin, Space, Typography } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_GROUP_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { useGetApiTseStatus } from '@/api/generated/tse/tse';
import { useGetApiTseDevices } from '@/api/generated/tse/tse';
import Link from 'next/link';

export default function RksvCmcCertificatePage() {
    const { data: tseStatus, isLoading: statusLoading, error: statusError } = useGetApiTseStatus();
    const { data: devices, isLoading: devicesLoading } = useGetApiTseDevices();

    const isLoading = statusLoading || devicesLoading;

    if (isLoading) {
        return (
            <div style={{ textAlign: 'center', padding: 80 }}>
                <Spin size="large" />
            </div>
        );
    }

    return (
        <>
            <AdminPageHeader
                title="CMC / Zertifikat (TSE)"
                breadcrumbs={[
                    ADMIN_OVERVIEW_CRUMB,
                    { title: ADMIN_NAV_GROUP_LABELS.rksv, href: '/rksv' },
                    { title: 'CMC / Zertifikat' },
                ]}
            />

            {statusError && (
                <Alert
                    type="error"
                    showIcon
                    message="TSE-Daten nicht verfügbar"
                    description={statusError instanceof Error ? statusError.message : String(statusError)}
                    style={{ marginBottom: 16 }}
                />
            )}

            <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 16 }}
                message="Technischer Überblick (Diagnose) — keine Beleg-/Zahlungswahrheit"
                description={
                    <Space direction="vertical" size={8} style={{ width: '100%' }}>
                        <Typography.Paragraph style={{ marginBottom: 0, fontSize: 13 }}>
                            Diese Seite zeigt nur den technischen Status rund um TSE/CMC/Zertifikat und erkannte Geräte.
                            Sie ist <strong>nicht</strong> die FinanzOnline-Wahrheit je Zahlung und <strong>kein</strong>{' '}
                            Rechtsnachweis für einzelne Belege.
                        </Typography.Paragraph>
                        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                            Für den operativen FinanzOnline-Status je Zahlung (Fehlertext, Retry, Referenz) den{' '}
                            <Link href="/rksv/finanz-online-queue">FinanzOnline-Abgleich</Link> nutzen.
                        </Typography.Paragraph>
                    </Space>
                }
            />

            <Space wrap style={{ marginBottom: 16 }}>
                <Button type="primary" href="/rksv/status">
                    RKSV · Schnittstellen-Übersicht
                </Button>
                <Button href="/rksv/finanz-online-queue">FinanzOnline-Abgleich</Button>
                <Button href="/rksv/finanz-online-operations">FinanzOnline Operations</Button>
                <Button href="/rksv/fiscal-export-diagnostics">Fiscal-Export Diagnose</Button>
                <Button href="/rksv/integrity">Datenintegrität (Support)</Button>
            </Space>

            <Card size="small" title="Technischer Snapshot (Status-API)">
                <Space direction="vertical" size={12} style={{ width: '100%' }}>
                    <Descriptions
                        title={<Typography.Text strong>Sertifikat / CMC</Typography.Text>}
                        column={1}
                        bordered
                        size="small"
                    >
                        <Descriptions.Item label="Zertifikatsstatus (laut API)">
                            {tseStatus?.certificateStatus ?? '—'}
                        </Descriptions.Item>
                    </Descriptions>

                    <Descriptions
                        title={<Typography.Text strong>Gerät & Identität</Typography.Text>}
                        column={1}
                        bordered
                        size="small"
                    >
                        <Descriptions.Item label="Seriennummer">{tseStatus?.serialNumber ?? '—'}</Descriptions.Item>
                        <Descriptions.Item label="Kassen-ID (Anzeige)">{tseStatus?.kassenId ?? '—'}</Descriptions.Item>
                    </Descriptions>

                    <Descriptions
                        title={<Typography.Text strong>Speicher & Signaturen</Typography.Text>}
                        column={1}
                        bordered
                        size="small"
                    >
                        <Descriptions.Item label="Speicherstatus (laut API)">
                            {tseStatus?.memoryStatus ?? '—'}
                        </Descriptions.Item>
                        <Descriptions.Item label="Letzte Signaturzeit (laut API)">
                            {tseStatus?.lastSignatureTime ?? '—'}
                        </Descriptions.Item>
                    </Descriptions>
                </Space>
            </Card>

            <Card size="small" title="Erkannte TSE-Geräte" style={{ marginTop: 16 }}>
                {devices && devices.length > 0 ? (
                    <Descriptions column={1} bordered size="small">
                        {devices.map((d, i) => (
                            <Descriptions.Item key={d.id ?? i} label={d.serialNumber || `Gerät ${i + 1}`}>
                                {d.kassenId ?? d.serialNumber ?? d.id ?? '—'}
                            </Descriptions.Item>
                        ))}
                    </Descriptions>
                ) : (
                    <Typography.Text type="secondary">Keine TSE-Geräte erkannt.</Typography.Text>
                )}
            </Card>

            <Divider style={{ margin: '16px 0' }} />

            <Card size="small" title="Geplante Diagnose-Funktionen (derzeit nicht verfügbar)">
                <Space direction="vertical" size={8} style={{ width: '100%' }}>
                    <Alert
                        type="info"
                        showIcon
                        message="Zertifikats-Gültigkeitsverlauf"
                        description="In der aktuellen API werden keine Felder für Valid-from/to oder Timeline geliefert."
                    />
                    <Alert
                        type="info"
                        showIcon
                        message="Zertifikatskette (Chain Details)"
                        description="In der aktuellen API gibt es keine Daten zur Chain/Issuer/Subject-Hierarchie."
                    />
                </Space>
            </Card>
        </>
    );
}
