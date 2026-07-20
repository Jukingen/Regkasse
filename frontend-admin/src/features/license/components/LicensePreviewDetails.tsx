'use client';

import { FileTextOutlined } from '@ant-design/icons';
import { Alert, Card, Descriptions, Space, Tag, Typography } from 'antd';

import { FormSkeleton } from '@/components/Skeleton';
import { formatGermanDateTime } from '@/i18n';
import type {
    ExtendTenantLicenseResult,
    TenantLicensePreviewResult,
} from '@/features/license/api/tenantLicense';
import type { LicenseExtendUiState } from '@/features/license/utils/licenseExtendModalState';
import {
    formatLicensePreviewDurationCombined,
    formatLicensePreviewPlanName,
    getPreviewStatusColor,
    getPreviewStatusLabel,
    mapPreviewErrorMessage,
} from '@/features/license/utils/licensePreviewDisplay';

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

export type LicensePreviewDetailsProps = {
    uiState: LicenseExtendUiState;
    preview: TenantLicensePreviewResult | null;
    extendResult: ExtendTenantLicenseResult | null;
    t: TranslateFn;
    formatLocale: string;
};

export function LicensePreviewDetails({
    uiState,
    preview,
    extendResult,
    t,
    formatLocale,
}: LicensePreviewDetailsProps) {
    const isPreviewLoading = uiState === 'loading' || uiState === 'confirming';

    return (
        <Card
            size="small"
            style={{ marginTop: 16 }}
            title={
                <Space>
                    <FileTextOutlined />
                    <span>{t('license.extendModal.previewTitle')}</span>
                </Space>
            }
        >
            {isPreviewLoading ? <FormSkeleton fields={4} loading /> : null}

            {uiState === 'success' && extendResult ? (
                <>
                    <Alert
                        type="success"
                        showIcon
                        style={{ marginBottom: 12 }}
                        title={t('license.extendModal.success')}
                        description={
                            extendResult.validUntilUtc
                                ? t('license.extendModal.successDetails', {
                                      date: formatGermanDateTime(extendResult.validUntilUtc),
                                  })
                                : undefined
                        }
                    />
                    <Descriptions column={1} size="small" style={{ textAlign: 'left' }}>
                        <Descriptions.Item label={t('license.mandant.licenseKey')}>
                            <Typography.Text code copyable={{ text: extendResult.licenseKey }}>
                                {extendResult.licenseKey}
                            </Typography.Text>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('license.extendModal.validUntilLabel')}>
                            {extendResult.validUntilUtc
                                ? formatGermanDateTime(extendResult.validUntilUtc)
                                : '—'}
                        </Descriptions.Item>
                    </Descriptions>
                </>
            ) : null}

            {!isPreviewLoading && uiState === 'invalid' && preview ? (
                <Alert
                    type="error"
                    showIcon
                    style={{ marginBottom: preview.licenseKey ? 12 : 0 }}
                    title={mapPreviewErrorMessage(preview.errorCode, preview.errorMessage, t)}
                />
            ) : null}

            {!isPreviewLoading && (uiState === 'valid' || uiState === 'invalid') && preview ? (
                <>
                    {preview.licenseKey || preview.validFromUtc || preview.validUntilUtc ? (
                        <Descriptions column={1} size="small" style={{ textAlign: 'left' }}>
                            {preview.licenseKey ? (
                                <Descriptions.Item label={t('license.mandant.licenseKey')}>
                                    <Typography.Text code copyable={{ text: preview.licenseKey }}>
                                        {preview.licenseKey}
                                    </Typography.Text>
                                </Descriptions.Item>
                            ) : null}
                            <Descriptions.Item label={t('license.extendModal.previewValidFrom')}>
                                {preview.validFromUtc
                                    ? formatGermanDateTime(preview.validFromUtc)
                                    : '—'}
                            </Descriptions.Item>
                            <Descriptions.Item label={t('license.extendModal.previewValidUntil')}>
                                {preview.validUntilUtc
                                    ? formatGermanDateTime(preview.validUntilUtc)
                                    : '—'}
                            </Descriptions.Item>
                            <Descriptions.Item label={t('license.extendModal.previewDuration')}>
                                {formatLicensePreviewDurationCombined(preview.durationDays, t)}
                            </Descriptions.Item>
                            <Descriptions.Item label={t('license.extendModal.previewPlan')}>
                                {formatLicensePreviewPlanName(preview.durationDays, t)}
                            </Descriptions.Item>
                            <Descriptions.Item label={t('license.extendModal.previewStatus')}>
                                <Tag color={getPreviewStatusColor(preview.status)}>
                                    {getPreviewStatusLabel(preview.status, t)}
                                </Tag>
                            </Descriptions.Item>
                        </Descriptions>
                    ) : null}
                    {uiState === 'valid' ? (
                        <Alert
                            type="warning"
                            showIcon
                            style={{ marginTop: 12, marginBottom: 0 }}
                            title={t('license.extendModal.previewConfirmMessage')}
                        />
                    ) : null}
                </>
            ) : null}
        </Card>
    );
}
