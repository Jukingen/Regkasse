'use client';

import React from 'react';
import { Alert, Card, Typography, List } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';

/**
 * Static support page: explains fiscal export JSON diagnostics (no API call).
 * German UI per project rules.
 */
export default function FiscalExportDiagnosticsPage() {
    return (
        <>
            <AdminPageHeader
                title="Fiscal-Export Diagnosehinweise"
                breadcrumbs={[
                    { title: 'Dashboard', href: '/dashboard' },
                    { title: 'RKSV', href: '/rksv' },
                    { title: 'Fiscal-Export Diagnose' },
                ]}
            />

            <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 24 }}
                message="Nur zur Diagnose — kein Rechtsnachweis (NOT LEGAL PROOF)"
                description={
                    <>
                        Der JSON-Export unter <Typography.Text code>/api/admin/fiscal-export</Typography.Text> ist{' '}
                        <strong>ausschließlich zur Diagnose und Auswertung</strong> gedacht. Er stellt keinen
                        Rechtsnachweis dar und ersetzt keine gesetzliche RKSV-Prüfung. Die Felder{' '}
                        <Typography.Text code>integrity</Typography.Text> und{' '}
                        <Typography.Text code>chainContinuityWarnings</Typography.Text> sind rein diagnostisch.
                        <br />
                        <strong>Pflicht:</strong> <Typography.Text code>exportScopeWarnings</Typography.Text> und{' '}
                        <Typography.Text code>notLegalProofNotice</Typography.Text> im Export-Payload{' '}
                        <strong>müssen immer angezeigt</strong> werden, damit der Export nicht fälschlich als
                        Rechtsnachweis interpretiert wird.
                    </>
                }
            />

            <Card title="Neue bzw. erweiterte JSON-Felder (Schema 1.2+)" size="small" style={{ marginBottom: 16 }}>
                <List
                    size="small"
                    dataSource={[
                        'notLegalProofNotice — immer gesetzt; enthält "NOT LEGAL PROOF". Muss in der UI angezeigt werden.',
                        'exportScopeWarnings — immer mindestens ein Eintrag (u. a. NOT LEGAL PROOF). Zwingend anzeigen; z. B. Fenstergrenzen, Kürzung, Hinweis dass Kettenprüfung nur im Export liegt.',
                        'totalReceiptsMatchingPeriod / receiptsTruncated — ob die Belegliste abgeschnitten wurde (max. 50.000).',
                        'integrity.receiptSignatureLinkageOkInExportOrder — gleiche Bedeutung wie signatureChainValid, klarer Name.',
                        'integrity.belegSequenceContiguousInExportedOrderPerDay — wie sequenceContinuous, klar benannt.',
                        'integrity.integrityDiagnosticNotes — kurze Erläuterung, wie die Booleans zustande kommen.',
                        'integrity.offlineMetricsScopedToPeriod — Offline-Zähler beziehen sich auf OfflineCreatedAtUtc im gleichen UTC-Fenster.',
                    ]}
                    renderItem={(item) => <List.Item>{item}</List.Item>}
                />
            </Card>

            <Card title="Typische Fehlinterpretationen vermeiden" size="small">
                <List
                    size="small"
                    dataSource={[
                        'signatureChainValid = true bedeutet nur: aufeinanderfolgende Belege **in diesem Export** stimmen prev/sig überein. Brüche **vor** FromUtc oder **nach** ToUtc werden nicht erkannt.',
                        'sequenceContinuous = true prüft nur die **exportierte** Reihenfolge je Kalendertag; keine vollständige Kassen-Jahresprüfung.',
                        'signatureChainState ist der **aktuelle** Kettenkopf der Kasse — kann vom letzten Beleg in der Datei abweichen, wenn nach ToUtc noch Belege existieren.',
                    ]}
                    renderItem={(item) => <List.Item>{item}</List.Item>}
                />
            </Card>
        </>
    );
}
