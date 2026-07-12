'use client';

import React from 'react';
import { Card, Row, Col, Tag, Spin, Alert, Typography, Button, Space, Divider, Tooltip } from 'antd';
import { CheckCircleOutlined, CloseCircleOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_GROUP_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { useGetApiTseStatus } from '@/features/rksv/useTseStatusCompat';
import { useGetApiFinanzOnlineStatus } from '@/api/generated/finanz-online/finanz-online';
import {
  RksvDeploymentEnvironmentAlert,
  RksvDeploymentEnvironmentBadge,
} from '@/features/rksv/components/RksvDeploymentEnvironmentStatus';
import { useRksvStatus } from '@/features/rksv/hooks/useRksvBackendEnvironment';
import Link from 'next/link';
import {
    OPERATOR_FO_SUMMARY_SCREEN_COPY,
    OPERATOR_RKSV_GENERAL_STATUS_COPY,
} from '@/shared/operatorTruthCopy';
import { useI18n } from '@/i18n';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

export default function RksvStatusPage() {
    const { t } = useI18n();
    const canOpenSonderbelege = useCanAccessPath(RKSV_SONDERBELEGE_PATH);
    const { data: rksvEnv, isLoading: rksvEnvLoading } = useRksvStatus();
    const { data: tseStatus, isLoading: tseLoading, error: tseError } = useGetApiTseStatus();
    const { data: foStatus, isLoading: foLoading, error: foError } = useGetApiFinanzOnlineStatus();

    const diagnosticsLoading = tseLoading || foLoading;

    return (
        <>
            <AdminPageHeader
                title={
                    <Space align="baseline" wrap>
                        <span>{OPERATOR_RKSV_GENERAL_STATUS_COPY.pageTitle}</span>
                        <RksvDeploymentEnvironmentBadge
                            status={rksvEnv}
                            isDemo={rksvEnv?.isSimulated}
                            loading={rksvEnvLoading}
                        />
                    </Space>
                }
                breadcrumbs={[
                    ADMIN_OVERVIEW_CRUMB,
                    { title: ADMIN_NAV_GROUP_LABELS.rksv, href: '/rksv' },
                    { title: OPERATOR_RKSV_GENERAL_STATUS_COPY.breadcrumbLabel },
                ]}
            />

            <RksvDeploymentEnvironmentAlert style={{ marginBottom: 16 }} />

            <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 16 }}
                title={OPERATOR_RKSV_GENERAL_STATUS_COPY.pageScopeAlertMessage}
                description={
                    <Space orientation="vertical" size={10} style={{ width: '100%' }}>
                        <Typography.Paragraph style={{ marginBottom: 0, fontSize: 13 }}>
                            {OPERATOR_RKSV_GENERAL_STATUS_COPY.pageScopeAlertBody}
                        </Typography.Paragraph>
                        <Typography.Paragraph style={{ marginBottom: 0, fontSize: 13 }}>
                            <strong>{OPERATOR_RKSV_GENERAL_STATUS_COPY.pageScopeAlertRowTruthLead}</strong>{' '}
                            <Link href="/rksv/finanz-online-queue">
                                {OPERATOR_FO_SUMMARY_SCREEN_COPY.abgleichPrimaryLinkLabel}
                            </Link>
                            .
                        </Typography.Paragraph>
                        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                            {OPERATOR_RKSV_GENERAL_STATUS_COPY.pageScopeAlertFootnote}
                        </Typography.Paragraph>
                    </Space>
                }
            />

            <Space wrap style={{ marginBottom: 20 }}>
                <Button type="primary" href="/rksv/finanz-online-queue" size="large">
                    {OPERATOR_FO_SUMMARY_SCREEN_COPY.abgleichPrimaryLinkLabel}
                </Button>
                <Typography.Text type="secondary" style={{ fontSize: 12, alignSelf: 'center' }}>
                    {OPERATOR_RKSV_GENERAL_STATUS_COPY.ctaPrimaryHint}
                </Typography.Text>
                <Button href="/rksv/finanz-online-operations" size="large">
                    {OPERATOR_FO_SUMMARY_SCREEN_COPY.operationsSupportingLinkLabel}
                </Button>
                <Typography.Text type="secondary" style={{ fontSize: 12, alignSelf: 'center' }}>
                    {OPERATOR_RKSV_GENERAL_STATUS_COPY.ctaSecondaryHint}
                </Typography.Text>
            </Space>

            {canOpenSonderbelege ? (
                <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 16 }}
                    title="RKSV Sonderbelege"
                    description={
                        <Typography.Paragraph style={{ marginBottom: 0 }}>
                            Nullbeleg, Startbeleg, Monatsbeleg, Jahresbeleg und Endbeleg (Schlussbeleg) finden Sie unter{' '}
                            <Link href="/rksv/sonderbelege">RKSV Sonderbelege</Link>.
                        </Typography.Paragraph>
                    }
                />
            ) : null}

            {tseError && (
                <Alert
                    type="error"
                    title={OPERATOR_RKSV_GENERAL_STATUS_COPY.tseStatusLoadError}
                    description={
                        <ApiErrorAlertDescription
                            t={t}
                            error={tseError}
                            logContext="RksvStatus.tse"
                            fallbackKey="common.messages.unknownError"
                        />
                    }
                    style={{ marginBottom: 16 }}
                />
            )}
            {foError && (
                <Alert
                    type="warning"
                    title={OPERATOR_RKSV_GENERAL_STATUS_COPY.foStatusLoadError}
                    description={
                        <ApiErrorAlertDescription
                            t={t}
                            error={foError}
                            logContext="RksvStatus.finanzOnline"
                            fallbackKey="common.messages.unknownError"
                        />
                    }
                    style={{ marginBottom: 16 }}
                />
            )}

            <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
                {t('rksvHub.rksvStatusPage.sectionDiagnosticsTitle')}
            </Typography.Text>
            {diagnosticsLoading ? (
                <div style={{ textAlign: 'center', padding: 48 }}>
                    <Spin size="large" />
                </div>
            ) : (
            <Row gutter={[16, 16]}>
                <Col xs={24} md={12}>
                    <Card title={OPERATOR_RKSV_GENERAL_STATUS_COPY.tseCardTitle} size="small">
                        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
                            {OPERATOR_RKSV_GENERAL_STATUS_COPY.tseStatisticTitle}
                        </Typography.Text>
                        <Tooltip title={t('rksvHub.rksvStatusPage.tseTagTooltip')}>
                            <Tag
                                color={tseStatus?.isConnected ? 'success' : 'error'}
                                icon={
                                    tseStatus?.isConnected ? <CheckCircleOutlined /> : <CloseCircleOutlined />
                                }
                            >
                                {tseStatus?.isConnected
                                    ? OPERATOR_RKSV_GENERAL_STATUS_COPY.tseReachableTag
                                    : OPERATOR_RKSV_GENERAL_STATUS_COPY.tseUnreachableTag}
                            </Tag>
                        </Tooltip>
                        {tseStatus?.serialNumber && (
                            <p style={{ marginTop: 8 }}>
                                {OPERATOR_RKSV_GENERAL_STATUS_COPY.tseSerialLabel}: {tseStatus.serialNumber}
                            </p>
                        )}
                        {tseStatus?.kassenId && (
                            <p>
                                {OPERATOR_RKSV_GENERAL_STATUS_COPY.tseKassenIdLabel}: {tseStatus.kassenId}
                            </p>
                        )}
                        {tseStatus?.certificateStatus && (
                            <p>
                                {OPERATOR_RKSV_GENERAL_STATUS_COPY.tseCertificateLabel}: {tseStatus.certificateStatus}
                            </p>
                        )}
                        {tseStatus?.canCreateInvoices !== undefined && (
                            <p>
                                {OPERATOR_RKSV_GENERAL_STATUS_COPY.tseCanCreateInvoicesLabel}:{' '}
                                {tseStatus.canCreateInvoices
                                    ? OPERATOR_RKSV_GENERAL_STATUS_COPY.tseCanCreateYes
                                    : OPERATOR_RKSV_GENERAL_STATUS_COPY.tseCanCreateNo}
                            </p>
                        )}
                        <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}>
                            {OPERATOR_RKSV_GENERAL_STATUS_COPY.tseCardFootnote}{' '}
                            <Link href="/rksv/cmc-certificate">CMC / Zertifikat</Link>
                        </Typography.Paragraph>
                    </Card>
                </Col>

                <Col xs={24} md={12}>
                    <Card title={OPERATOR_RKSV_GENERAL_STATUS_COPY.foCardTitle} size="small">
                        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
                            {OPERATOR_RKSV_GENERAL_STATUS_COPY.foStatisticTitle}
                        </Typography.Text>
                        <Tooltip
                            title={
                                !foStatus
                                    ? t('rksvHub.rksvStatusPage.foTooltipNonAuthoritative')
                                    : foStatus.finanzOnlineTransportsSimulated
                                      ? t('rksvHub.rksvStatusPage.foTooltipSimulated')
                                      : foStatus.isAuthoritative === false
                                        ? t('rksvHub.rksvStatusPage.foTooltipNonAuthoritative')
                                        : t('rksvHub.rksvStatusPage.foTooltipAuthoritative')
                            }
                        >
                            <Tag
                                color={
                                    foStatus?.finanzOnlineTransportsSimulated
                                        ? 'orange'
                                        : foStatus?.isAuthoritative === false
                                          ? 'gold'
                                          : foStatus?.isConnected
                                            ? 'success'
                                            : 'error'
                                }
                                icon={
                                    foStatus?.finanzOnlineTransportsSimulated
                                        ? undefined
                                        : foStatus?.isAuthoritative === false
                                          ? undefined
                                          : foStatus?.isConnected
                                            ? <CheckCircleOutlined />
                                            : <CloseCircleOutlined />
                                }
                            >
                                {foStatus?.finanzOnlineTransportsSimulated
                                    ? OPERATOR_RKSV_GENERAL_STATUS_COPY.foSimulatedTransportTag
                                    : foStatus?.isAuthoritative === false
                                      ? t('rksvHub.finanzOnlineOpsPage.connectionPendingLabel')
                                      : foStatus?.isConnected
                                        ? OPERATOR_RKSV_GENERAL_STATUS_COPY.foReachableTag
                                        : OPERATOR_RKSV_GENERAL_STATUS_COPY.foUnreachableTag}
                            </Tag>
                        </Tooltip>
                        {foStatus?.pendingInvoices !== undefined && (
                            <p style={{ marginTop: 8 }}>
                                {OPERATOR_RKSV_GENERAL_STATUS_COPY.foPendingInvoicesLabel}: {foStatus.pendingInvoices}
                            </p>
                        )}
                        {foStatus?.lastSync && (
                            <p>
                                {OPERATOR_RKSV_GENERAL_STATUS_COPY.foLastSyncLabel}: {foStatus.lastSync}
                            </p>
                        )}
                        {foStatus?.transportDiagnostics ? (
                            <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 4, fontSize: 11 }}>
                                <Typography.Text code copyable style={{ fontSize: 11 }}>
                                    {foStatus.transportDiagnostics}
                                </Typography.Text>
                            </Typography.Paragraph>
                        ) : null}
                        <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 4, fontSize: 12 }}>
                            {OPERATOR_FO_SUMMARY_SCREEN_COPY.connectionMetricsNotPaymentRowTruth}
                        </Typography.Paragraph>
                        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                            {OPERATOR_RKSV_GENERAL_STATUS_COPY.foCardFootnote}
                        </Typography.Paragraph>
                    </Card>
                </Col>
            </Row>
            )}

            <Divider style={{ margin: '16px 0' }} />

            <Card
                size="small"
                title={t('rksvHub.rksvStatusPage.paymentTruthCardTitle')}
                style={{ marginBottom: 0 }}
            >
                <Space orientation="vertical" size={10} style={{ width: '100%' }}>
                    <Typography.Paragraph style={{ marginBottom: 0, fontSize: 13 }}>
                        {t('rksvHub.rksvStatusPage.paymentTruthLineBeforeLink')}{' '}
                        <Link href="/rksv/finanz-online-queue">
                            <strong>{OPERATOR_FO_SUMMARY_SCREEN_COPY.abgleichPrimaryLinkLabel}</strong>
                        </Link>
                        {t('rksvHub.rksvStatusPage.paymentTruthLineAfterLink')}
                    </Typography.Paragraph>
                    <Space wrap>
                        <Button type="primary" href="/rksv/finanz-online-queue">
                            {OPERATOR_FO_SUMMARY_SCREEN_COPY.abgleichPrimaryLinkLabel}
                        </Button>
                        <Button href="/rksv/finanz-online-operations">
                            {OPERATOR_FO_SUMMARY_SCREEN_COPY.operationsSupportingLinkLabel}
                        </Button>
                    </Space>
                </Space>
            </Card>
        </>
    );
}
