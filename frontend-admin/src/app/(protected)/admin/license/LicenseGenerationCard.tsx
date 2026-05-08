'use client';

/**
 * Admin license issuance card. Posts to POST /api/admin/license/generate and renders the result with copy buttons.
 * Visibility is gated by `canGenerate` (settings.manage). Operator copy is provided through the `license.generation.*` i18n namespace.
 */

import React, { useMemo, useState } from 'react';
import axios from 'axios';
import dayjs, { type Dayjs } from 'dayjs';
import {
    Alert,
    Button,
    Card,
    Checkbox,
    DatePicker,
    Descriptions,
    Form,
    Input,
    Space,
    Typography,
    message,
} from 'antd';
import { CopyOutlined, KeyOutlined } from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useI18n, formatDate } from '@/i18n';
import {
    licenseQueryKeys,
    postGenerateLicense,
    type GenerateLicenseRequest,
    type GenerateLicenseResponse,
} from '@/api/manual/adminLicense';

type Props = {
    /** True when the current user has settings.manage permission. */
    canGenerate: boolean;
};

type FormValues = {
    customerName: string;
    expiryDate: Dayjs;
    requireFingerprint: boolean;
    machineHashHex?: string;
};

const MACHINE_HASH_REGEX = /^[0-9a-fA-F]{64}$/;

export function LicenseGenerationCard({ canGenerate }: Props) {
    const { t, formatLocale } = useI18n();
    const queryClient = useQueryClient();
    const [form] = Form.useForm<FormValues>();
    const requireFingerprint = Form.useWatch('requireFingerprint', form) ?? false;
    const [issued, setIssued] = useState<GenerateLicenseResponse | null>(null);

    const tomorrow = useMemo(() => dayjs().add(1, 'day'), []);

    const mutation = useMutation({
        mutationFn: (body: GenerateLicenseRequest) => postGenerateLicense(body),
        onSuccess: (res) => {
            if (!res.success || !res.licenseKey || !res.signedJwt) {
                message.error(res.message || t('license.generation.failed'));
                return;
            }
            setIssued(res);
            message.success(t('license.generation.success'));
            form.resetFields(['customerName', 'machineHashHex']);
            void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.listRoot });
        },
        onError: (err: unknown) => {
            if (axios.isAxiosError(err)) {
                const status = err.response?.status;
                const data = err.response?.data as { message?: string } | undefined;
                if (status === 503) {
                    message.error(data?.message || t('license.generation.unavailable'));
                    return;
                }
                message.error(data?.message || t('license.generation.failed'));
                return;
            }
            message.error(t('license.generation.failed'));
        },
    });

    if (!canGenerate) {
        return (
            <Card type="inner" title={t('license.generation.title')}>
                <Alert type="info" showIcon message={t('license.generation.noPermission')} />
            </Card>
        );
    }

    const onFinish = (values: FormValues) => {
        const customerName = values.customerName?.trim() ?? '';
        if (!customerName) return;

        const expiryIso = values.expiryDate.format('YYYY-MM-DD');
        const machineHashHex = values.requireFingerprint
            ? (values.machineHashHex?.trim().toLowerCase() ?? '')
            : null;

        mutation.mutate({
            customerName,
            expiryDate: expiryIso,
            requireFingerprint: !!values.requireFingerprint,
            machineHashHex,
        });
    };

    return (
        <Card type="inner" title={t('license.generation.title')}>
            <Form<FormValues>
                form={form}
                layout="vertical"
                initialValues={{ requireFingerprint: false, expiryDate: tomorrow }}
                onFinish={onFinish}
            >
                <Form.Item
                    name="customerName"
                    label={t('license.generation.customerName')}
                    rules={[
                        { required: true, message: t('common.validation.fieldRequired') },
                        { max: 256 },
                    ]}
                >
                    <Input placeholder={t('license.generation.customerNamePlaceholder')} autoComplete="off" />
                </Form.Item>

                <Form.Item
                    name="expiryDate"
                    label={t('license.generation.expiryDate')}
                    extra={t('license.generation.expiryDateHelp')}
                    rules={[{ required: true, message: t('common.validation.fieldRequired') }]}
                >
                    <DatePicker
                        style={{ width: '100%' }}
                        format="YYYY-MM-DD"
                        disabledDate={(d) => !d || d.isBefore(dayjs().startOf('day'))}
                    />
                </Form.Item>

                <Form.Item name="requireFingerprint" valuePropName="checked">
                    <Checkbox>{t('license.generation.requireFingerprint')}</Checkbox>
                </Form.Item>

                {requireFingerprint ? (
                    <Form.Item
                        name="machineHashHex"
                        label={t('license.generation.machineHash')}
                        extra={t('license.generation.requireFingerprintHelp')}
                        rules={[
                            { required: true, message: t('common.validation.fieldRequired') },
                            {
                                validator: (_rule, value) =>
                                    !value || MACHINE_HASH_REGEX.test(String(value).trim())
                                        ? Promise.resolve()
                                        : Promise.reject(new Error(t('common.validation.fieldRequired'))),
                            },
                        ]}
                    >
                        <Input
                            placeholder={t('license.generation.machineHashPlaceholder')}
                            autoComplete="off"
                            style={{ fontFamily: 'ui-monospace, monospace' }}
                        />
                    </Form.Item>
                ) : null}

                <Form.Item>
                    <Button
                        type="primary"
                        htmlType="submit"
                        icon={<KeyOutlined />}
                        loading={mutation.isPending}
                    >
                        {t('license.generation.submit')}
                    </Button>
                </Form.Item>
            </Form>

            {issued && issued.success && issued.licenseKey && issued.signedJwt ? (
                <IssuedLicenseResult issued={issued} formatLocale={formatLocale} />
            ) : null}
        </Card>
    );
}

function IssuedLicenseResult({
    issued,
    formatLocale,
}: {
    issued: GenerateLicenseResponse;
    formatLocale: string;
}) {
    const { t } = useI18n();

    const formattedExpiry = issued.expiryAtUtc
        ? formatDate(issued.expiryAtUtc, formatLocale, {
              year: 'numeric',
              month: '2-digit',
              day: '2-digit',
          })
        : '—';

    return (
        <div style={{ marginTop: 16 }}>
            <Alert
                type="warning"
                showIcon
                message={t('license.generation.result.title')}
                description={t('license.generation.result.warning')}
                style={{ marginBottom: 12 }}
            />
            <Descriptions bordered column={1} size="small">
                <Descriptions.Item label={t('license.generation.result.licenseKey')}>
                    <CopyableMono value={issued.licenseKey ?? ''} />
                </Descriptions.Item>
                <Descriptions.Item label={t('license.generation.result.signedJwt')}>
                    <CopyableMono value={issued.signedJwt ?? ''} multiline />
                </Descriptions.Item>
                <Descriptions.Item label={t('license.generation.result.expiry')}>
                    {formattedExpiry}
                </Descriptions.Item>
            </Descriptions>
        </div>
    );
}

function CopyableMono({ value, multiline = false }: { value: string; multiline?: boolean }) {
    const { t } = useI18n();

    const onCopy = async () => {
        try {
            await navigator.clipboard.writeText(value);
            message.success(t('license.generation.result.copied'));
        } catch {
            // Clipboard API requires secure context; fall back to legacy execCommand if available.
            const ta = document.createElement('textarea');
            ta.value = value;
            ta.style.position = 'fixed';
            ta.style.opacity = '0';
            document.body.appendChild(ta);
            ta.select();
            try {
                document.execCommand('copy');
                message.success(t('license.generation.result.copied'));
            } catch {
                message.error(t('license.generation.failed'));
            } finally {
                ta.remove();
            }
        }
    };

    return (
        <Space.Compact style={{ width: '100%' }}>
            {multiline ? (
                <Input.TextArea
                    value={value}
                    autoSize={{ minRows: 2, maxRows: 6 }}
                    readOnly
                    style={{ fontFamily: 'ui-monospace, monospace', wordBreak: 'break-all' }}
                />
            ) : (
                <Input
                    value={value}
                    readOnly
                    style={{ fontFamily: 'ui-monospace, monospace' }}
                />
            )}
            <Button icon={<CopyOutlined />} onClick={onCopy}>
                <Typography.Text>{t('license.generation.result.copy')}</Typography.Text>
            </Button>
        </Space.Compact>
    );
}
