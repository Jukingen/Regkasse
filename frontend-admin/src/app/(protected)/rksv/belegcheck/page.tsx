'use client';

import React, { useCallback, useEffect, useState } from 'react';
import { Alert, Button, Card, Descriptions, Input, List, Space, Tag, Typography } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_GROUP_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { useI18n } from '@/i18n';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';
import { consumeBelegcheckPrefillSession } from '@/features/rksv/belegcheckPrefillStorage';
import { parseRksvAmount } from '@/features/rksv/parseRksvAmount';

type ValidateReceiptParsed = {
    receiptNumber: string;
    timestamp: string;
    totals: { grossTotal: string; secondAmount: string };
    certificateSerial: string;
    previousSignature: string | null;
};

type ValidateReceiptResponse = {
    isValidFormat: boolean;
    parsed: ValidateReceiptParsed | null;
    errors: string[];
};

export default function RksvBelegcheckPage() {
    const { t } = useI18n();
    const [qrPayload, setQrPayload] = useState('');
    const [loading, setLoading] = useState(false);
    const [result, setResult] = useState<ValidateReceiptResponse | null>(null);
    const [requestError, setRequestError] = useState<unknown>(null);

    useEffect(() => {
        const pre = consumeBelegcheckPrefillSession();
        if (pre) {
            setQrPayload(pre);
            setResult(null);
            setRequestError(null);
        }
    }, []);

    const validate = useCallback(async () => {
        const trimmed = qrPayload.trim();
        setRequestError(null);
        setResult(null);
        if (!trimmed) {
            setResult({
                isValidFormat: false,
                parsed: null,
                errors: [t('nav.rksvBelegcheckEmptyPayload')],
            });
            return;
        }
        setLoading(true);
        try {
            const { data } = await AXIOS_INSTANCE.post<ValidateReceiptResponse>('/api/rksv/validate-receipt', {
                qrPayload: trimmed,
            });
            const grossRaw = data.parsed?.totals?.grossTotal ?? '';
            const secondRaw = data.parsed?.totals?.secondAmount ?? '';
            const grossParsed = data.parsed ? parseRksvAmount(grossRaw) : null;
            const secondParsed = data.parsed ? parseRksvAmount(secondRaw) : null;

            const amountErrors: string[] = [];
            if (data.parsed && grossParsed == null) {
                amountErrors.push(`Invalid gross total amount format: "${grossRaw}"`);
            }
            if (data.parsed && secondParsed == null) {
                amountErrors.push(`Invalid second amount format: "${secondRaw}"`);
            }

            const normalizedData: ValidateReceiptResponse =
                data.parsed == null
                    ? data
                    : {
                          ...data,
                          parsed: {
                              ...data.parsed,
                              totals: {
                                  grossTotal: grossParsed == null ? grossRaw : grossParsed.toFixed(2),
                                  secondAmount: secondParsed == null ? secondRaw : secondParsed.toFixed(2),
                              },
                          },
                          isValidFormat: data.isValidFormat && amountErrors.length === 0,
                          errors: amountErrors.length > 0 ? [...data.errors, ...amountErrors] : data.errors,
                      };

            setResult(normalizedData);
        } catch (e) {
            setRequestError(e);
        } finally {
            setLoading(false);
        }
    }, [qrPayload, t]);

    return (
        <>
            <AdminPageHeader
                title={t('nav.rksvLeafBelegcheck')}
                breadcrumbs={[
                    ADMIN_OVERVIEW_CRUMB,
                    { title: ADMIN_NAV_GROUP_LABELS.rksv, href: '/rksv' },
                    { title: t('nav.rksvLeafBelegcheck') },
                ]}
            />

            <Card style={{ maxWidth: 960 }}>
                <Space orientation="vertical" size="large" style={{ width: '100%' }}>
                    <div>
                        <Typography.Text strong>{t('nav.rksvBelegcheckQrLabel')}</Typography.Text>
                        <Input.TextArea
                            aria-label={t('nav.rksvBelegcheckQrLabel')}
                            value={qrPayload}
                            onChange={(e) => setQrPayload(e.target.value)}
                            rows={8}
                            placeholder={t('nav.rksvBelegcheckQrPlaceholder')}
                            style={{ marginTop: 8 }}
                        />
                    </div>
                    <Button type="primary" onClick={() => void validate()} loading={loading}>
                        {t('nav.rksvBelegcheckSubmit')}
                    </Button>

                    {requestError != null ? (
                        <Alert
                            type="error"
                            showIcon
                            title={t('nav.rksvBelegcheckRequestFailed')}
                            description={
                                <ApiErrorAlertDescription
                                    t={t}
                                    error={requestError}
                                    logContext="RksvBelegcheck.validate"
                                    fallbackKey="common.messages.unknownError"
                                />
                            }
                        />
                    ) : null}

                    {result && (
                        <div>
                            <Typography.Title level={5}>{t('nav.rksvBelegcheckResultTitle')}</Typography.Title>
                            <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                                <div>
                                    {result.isValidFormat ? (
                                        <Tag color="success">{t('nav.rksvBelegcheckValid')}</Tag>
                                    ) : (
                                        <Tag color="error">{t('nav.rksvBelegcheckInvalid')}</Tag>
                                    )}
                                </div>
                                {result.errors.length > 0 && (
                                    <Alert
                                        type={result.isValidFormat ? 'info' : 'warning'}
                                        showIcon
                                        title={t('nav.rksvBelegcheckErrorsHeading')}
                                        description={
                                            <List
                                                size="small"
                                                dataSource={result.errors}
                                                renderItem={(item) => <List.Item style={{ paddingLeft: 0 }}>{item}</List.Item>}
                                            />
                                        }
                                    />
                                )}
                                {result.parsed && (
                                    <Descriptions bordered size="small" column={1}>
                                        <Descriptions.Item label={t('nav.rksvBelegcheckFieldReceiptNumber')}>
                                            {result.parsed.receiptNumber}
                                        </Descriptions.Item>
                                        <Descriptions.Item label={t('nav.rksvBelegcheckFieldTimestamp')}>
                                            {result.parsed.timestamp}
                                        </Descriptions.Item>
                                        <Descriptions.Item label={t('nav.rksvBelegcheckFieldGrossTotal')}>
                                            {result.parsed.totals.grossTotal}
                                        </Descriptions.Item>
                                        <Descriptions.Item label={t('nav.rksvBelegcheckFieldSecondAmount')}>
                                            {result.parsed.totals.secondAmount}
                                        </Descriptions.Item>
                                        <Descriptions.Item label={t('nav.rksvBelegcheckFieldCertificate')}>
                                            {result.parsed.certificateSerial}
                                        </Descriptions.Item>
                                        <Descriptions.Item label={t('nav.rksvBelegcheckFieldPrevSignature')}>
                                            {result.parsed.previousSignature ?? t('nav.rksvBelegcheckNoPrev')}
                                        </Descriptions.Item>
                                    </Descriptions>
                                )}
                            </Space>
                        </div>
                    )}
                </Space>
            </Card>
        </>
    );
}
