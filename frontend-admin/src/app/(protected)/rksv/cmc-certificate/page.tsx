'use client';

import React, { useCallback } from 'react';
import { Alert, Button, Card, Descriptions, Divider, Spin, Space, Typography } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { FORMAT_EMPTY_DISPLAY, useI18n } from '@/i18n';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';
import { useGetApiTseDevices, useGetApiTseStatus } from '@/features/rksv/useTseStatusCompat';
import Link from 'next/link';

/**
 * TSE/CMC/Zertifikat — Diagnoseoberfläche.
 * UI: `rksvHub.cmcCertificate`; API-Rohwerte (certificateStatus, memoryStatus, …) unverändert.
 */
export default function RksvCmcCertificatePage() {
    const { t } = useI18n();
    const tc = useCallback((path: string, options?: Record<string, string | number>) => t(`rksvHub.cmcCertificate.${path}`, options), [t]);

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
                title={tc('pageTitle')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t('adminShell.group.rksv'), href: '/rksv' },
                    { title: tc('breadcrumb') },
                ]}
            />

            {statusError && (
                <Alert
                    type="error"
                    showIcon
                    message={tc('errorLoad')}
                    description={
                        <ApiErrorAlertDescription
                            t={t}
                            error={statusError}
                            logContext="RksvCmcCertificate.tseStatus"
                            fallbackKey="rksvHub.cmcCertificate.errorLoad"
                        />
                    }
                    style={{ marginBottom: 16 }}
                />
            )}

            <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 16 }}
                message={tc('scopeBannerTitle')}
                description={
                    <Space direction="vertical" size={8} style={{ width: '100%' }}>
                        <Typography.Paragraph style={{ marginBottom: 0, fontSize: 13 }}>
                            {tc('scopeBannerP1')}
                        </Typography.Paragraph>
                        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                            {tc('scopeBannerP2Before')}{' '}
                            <Link href="/rksv/finanz-online-queue">{tc('scopeBannerP2Link')}</Link> {tc('scopeBannerP2After')}
                        </Typography.Paragraph>
                    </Space>
                }
            />

            <Space wrap style={{ marginBottom: 16 }}>
                <Button type="primary" href="/rksv/status">
                    {tc('btnStatusOverview')}
                </Button>
                <Button href="/rksv/finanz-online-queue">{tc('btnFinanzQueue')}</Button>
                <Button href="/rksv/finanz-online-operations">{tc('btnFinanzOps')}</Button>
                <Button href="/rksv/fiscal-export-diagnostics">{tc('btnFiscalDiag')}</Button>
                <Button href="/rksv/integrity">{tc('btnIntegrity')}</Button>
            </Space>

            <Card size="small" title={tc('cardSnapshotTitle')}>
                <Space direction="vertical" size={12} style={{ width: '100%' }}>
                    <Descriptions
                        title={<Typography.Text strong>{tc('sectionCertCmc')}</Typography.Text>}
                        column={1}
                        bordered
                        size="small"
                    >
                        <Descriptions.Item label={tc('descCertStatus')}>
                            {tseStatus?.certificateStatus ?? FORMAT_EMPTY_DISPLAY}
                        </Descriptions.Item>
                    </Descriptions>

                    <Descriptions
                        title={<Typography.Text strong>{tc('sectionDevice')}</Typography.Text>}
                        column={1}
                        bordered
                        size="small"
                    >
                        <Descriptions.Item label={tc('descSerial')}>{tseStatus?.serialNumber ?? FORMAT_EMPTY_DISPLAY}</Descriptions.Item>
                        <Descriptions.Item label={tc('descKassenId')}>{tseStatus?.kassenId ?? FORMAT_EMPTY_DISPLAY}</Descriptions.Item>
                    </Descriptions>

                    <Descriptions
                        title={<Typography.Text strong>{tc('sectionMemory')}</Typography.Text>}
                        column={1}
                        bordered
                        size="small"
                    >
                        <Descriptions.Item label={tc('descMemoryStatus')}>
                            {tseStatus?.memoryStatus ?? FORMAT_EMPTY_DISPLAY}
                        </Descriptions.Item>
                        <Descriptions.Item label={tc('descLastSig')}>
                            {tseStatus?.lastSignatureTime ?? FORMAT_EMPTY_DISPLAY}
                        </Descriptions.Item>
                    </Descriptions>
                </Space>
            </Card>

            <Card size="small" title={tc('cardDevicesTitle')} style={{ marginTop: 16 }}>
                {devices && devices.length > 0 ? (
                    <Descriptions column={1} bordered size="small">
                        {devices.map((d, i) => (
                            <Descriptions.Item
                                key={d.id ?? i}
                                label={d.serialNumber || tc('deviceFallbackLabel', { index: i + 1 })}
                            >
                                {d.kassenId ?? d.serialNumber ?? d.id ?? FORMAT_EMPTY_DISPLAY}
                            </Descriptions.Item>
                        ))}
                    </Descriptions>
                ) : (
                    <Typography.Text type="secondary">{tc('noDevices')}</Typography.Text>
                )}
            </Card>

            <Divider style={{ margin: '16px 0' }} />

            <Card size="small" title={tc('cardPlannedTitle')}>
                <Space direction="vertical" size={8} style={{ width: '100%' }}>
                    <Alert type="info" showIcon message={tc('plannedValidityTitle')} description={tc('plannedValidityDesc')} />
                    <Alert type="info" showIcon message={tc('plannedChainTitle')} description={tc('plannedChainDesc')} />
                </Space>
            </Card>
        </>
    );
}
