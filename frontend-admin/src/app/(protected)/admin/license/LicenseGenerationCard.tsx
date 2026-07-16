'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Admin license issuance card. POST /api/admin/license/generate via TanStack Query mutation;
 * surfaces key + JWT in a success modal and inline summary with copy actions.
 */

import React, { useEffect, useMemo, useState } from 'react';
import axios from 'axios';
import dayjs, { type Dayjs } from 'dayjs';
import { Modal, Alert, Button, Card, Checkbox, DatePicker, Descriptions, Form, Input, Space, Typography } from 'antd';
import { CopyOutlined, KeyOutlined } from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useI18n, formatGermanDateTime } from '@/i18n';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';
import { deploymentLicenseAllows, LICENSE_DEPLOYMENT_FEATURE } from '@/shared/licenseDeploymentFeatures';
import {
    licenseQueryKeys,
    postGenerateLicense,
    type GenerateLicenseRequest,
    type GenerateLicenseResponse,
} from '@/api/manual/adminLicense';
import {
    ADMIN_LICENSE_PAGE_INTENT_EXTEND,
    buildAdminLicensePageHref,
    type AdminLicensePagePrefill,
} from '@/features/license/utils/adminLicenseRoute';

type Props = {
    /** True when the current user has settings.manage permission. */
    canGenerate: boolean;
    /** Local deployment SHA-256 hex from admin status (for deep-link helpers). */
    machineFingerprint: string;
    /** From GET /api/license/status: enabled license feature ids for this deployment. */
    enabledLicenseFeatures?: readonly string[] | null;
    /** Optional query-driven prefill from POS/admin deep links. */
    prefill?: AdminLicensePagePrefill | null;
};

type FormValues = {
    customerName: string;
    expiryDate: Dayjs;
    requireFingerprint: boolean;
    machineHashHex?: string;
};

const MACHINE_HASH_REGEX = /^[0-9a-f]{64}$/i;

function readLicenseGenerateErrorMessage(err: unknown, fallback: string): string {
    if (!axios.isAxiosError(err)) {
        return fallback;
    }
    const raw = err.response?.data;
    if (!raw || typeof raw !== 'object') {
        return fallback;
    }
    const o = raw as Record<string, unknown>;
    const fromMessage = typeof o.message === 'string' ? o.message.trim() : '';
    if (fromMessage) {
        return fromMessage;
    }
    const detail = typeof o.detail === 'string' ? o.detail.trim() : '';
    if (detail) {
        return detail;
    }
    const title = typeof o.title === 'string' ? o.title.trim() : '';
    if (title) {
        return title;
    }
    return fallback;
}

function pickJwt(res: GenerateLicenseResponse): string {
    return (res.licenseJwt ?? res.signedJwt ?? '').trim();
}

export function LicenseGenerationCard({
    canGenerate,
    machineFingerprint,
    enabledLicenseFeatures,
    prefill,
}: Props) {
    const { t } = useI18n();

    if (!canGenerate) {
        return (
            <Card type="inner" title={t('license.generation.title')}>
                <Alert type="info" showIcon title={t('license.generation.noPermission')} />
            </Card>
        );
    }

    if (!deploymentLicenseAllows(enabledLicenseFeatures, LICENSE_DEPLOYMENT_FEATURE.AdminLicenseManage)) {
        return (
            <Card type="inner" title={t('license.generation.title')}>
                <Alert type="warning" showIcon title={t('license.generation.featureNotIncluded')} />
            </Card>
        );
    }

    return (
        <LicenseGenerationFormCard
            machineFingerprint={machineFingerprint}
            prefill={prefill}
        />
    );
}

function LicenseGenerationFormCard({
    machineFingerprint,
    prefill,
}: Pick<Props, 'machineFingerprint' | 'prefill'>) {
  const { message } = useAntdApp();

    const { t, formatLocale } = useI18n();
    const queryClient = useQueryClient();
    const [form] = Form.useForm<FormValues>();
    const requireFingerprint = Form.useWatch('requireFingerprint', form) ?? false;
    const [issued, setIssued] = useState<GenerateLicenseResponse | null>(null);
    const [requestError, setRequestError] = useState<string | null>(null);
    const [resultModalOpen, setResultModalOpen] = useState(false);

    const tomorrow = useMemo(() => dayjs().add(1, 'day').startOf('day'), []);

    useEffect(() => {
        if (!prefill) {
            return;
        }
        const nextMachineHash = prefill.machineHashHex?.trim().toLowerCase();
        if (!nextMachineHash) {
            return;
        }

        const currentMachineHash = String(form.getFieldValue('machineHashHex') ?? '')
            .trim()
            .toLowerCase();
        const currentRequireFingerprint = Boolean(form.getFieldValue('requireFingerprint'));

        if (
            currentMachineHash === nextMachineHash &&
            currentRequireFingerprint === prefill.requireFingerprint
        ) {
            return;
        }

        form.setFieldsValue({
            requireFingerprint: prefill.requireFingerprint,
            machineHashHex: nextMachineHash,
        });
    }, [form, prefill]);

    const mutation = useMutation({
        mutationFn: (body: GenerateLicenseRequest) => postGenerateLicense(body),
        onMutate: () => {
            setRequestError(null);
        },
        onSuccess: (res) => {
            const jwt = pickJwt(res);
            if (!res.success || !res.licenseKey || !jwt) {
                const msg = res.message?.trim() || t('license.generation.failed');
                setRequestError(msg);
                message.error(msg);
                return;
            }
            setIssued(res);
            setResultModalOpen(true);
            message.success(t('license.generation.success'));
            form.resetFields(['customerName', 'machineHashHex']);
            void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.listRoot });
            void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.status });
            void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.publicStatus });
        },
        onError: (err: unknown) => {
            const fallback = t('license.generation.failed');
            const serverMsg = readLicenseGenerateErrorMessage(err, fallback);
            setRequestError(serverMsg);
            if (axios.isAxiosError(err) && err.response?.status === 503) {
                message.error(serverMsg || t('license.generation.unavailable'));
                return;
            }
            message.error(serverMsg);
        },
    });

    const onFinish = (values: FormValues) => {
        const customerName = values.customerName?.trim() ?? '';
        if (!customerName) {
            return;
        }

        const expiryIso = values.expiryDate.format('YYYY-MM-DD');
        const machineHashHex = values.requireFingerprint
            ? (values.machineHashHex?.trim().toLowerCase() ?? '')
            : null;

        mutation.mutate({
            customerName,
            expiryDate: expiryIso,
            bindToMachineFingerprint: !!values.requireFingerprint,
            machineHashHex,
        });
    };

    const issuedJwt = issued ? pickJwt(issued) : '';

    return (
        <Card type="inner" title={t('license.generation.title')}>
            {requestError ? (
                <Alert
                    type="error"
                    showIcon
                    closable
                    title={t('license.generation.failed')}
                    description={requestError}
                    style={{ marginBottom: 16 }}
                    onClose={() => setRequestError(null)}
                />
            ) : null}

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
                    rules={[
                        { required: true, message: t('common.validation.fieldRequired') },
                        {
                            validator: (_rule, value: Dayjs | undefined) => {
                                if (!value || !value.isValid()) {
                                    return Promise.reject(new Error(t('common.validation.fieldRequired')));
                                }
                                if (value.startOf('day').isBefore(dayjs().startOf('day'))) {
                                    return Promise.reject(new Error(t('license.generation.expiryInPast')));
                                }
                                return Promise.resolve();
                            },
                        },
                    ]}
                >
                    <DatePicker
                        style={{ width: '100%' }}
                        format={DAYJS_DATE_FORMAT}
                        disabledDate={(d) => !d || d.startOf('day').isBefore(dayjs().startOf('day'))}
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
                                        : Promise.reject(new Error(t('license.generation.invalidMachineHash'))),
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

            <Modal
                title={t('license.generation.modal.title')}
                open={resultModalOpen}
                onCancel={() => setResultModalOpen(false)}
                footer={
                    <Space wrap>
                        <Button onClick={() => setResultModalOpen(false)}>{t('common.buttons.close')}</Button>
                        {issued?.licenseKey ? (
                            <Button
                                onClick={async () => {
                                    const ok = await copyTextToClipboard(issued.licenseKey ?? '');
                                    message[ok ? 'success' : 'error'](
                                        ok
                                            ? t('license.generation.result.licenseKeyCopied')
                                            : t('license.generation.result.copyFailed'),
                                    );
                                }}
                            >
                                {t('license.generation.result.copyLicenseKeyOnly')}
                            </Button>
                        ) : null}
                        <Button
                            type="primary"
                            disabled={!issuedJwt}
                            onClick={async () => {
                                const ok = await copyTextToClipboard(issuedJwt);
                                message[ok ? 'success' : 'error'](
                                    ok
                                        ? t('license.generation.modal.jwtCopied')
                                        : t('license.generation.result.copyFailed'),
                                );
                            }}
                        >
                            {t('license.generation.modal.copyJwt')}
                        </Button>
                    </Space>
                }
                width={720}
                destroyOnHidden
            >
                {issued && issued.success ? (
                    <>
                        <Alert
                            type="warning"
                            showIcon
                            title={t('license.generation.result.title')}
                            description={t('license.generation.result.warning')}
                            style={{ marginBottom: 12 }}
                        />
                        <Typography.Text strong style={{ display: 'block', marginBottom: 4 }}>
                            {t('license.generation.result.licenseKey')}
                        </Typography.Text>
                        <Input
                            readOnly
                            value={issued.licenseKey ?? ''}
                            style={{ fontFamily: 'ui-monospace, monospace', marginBottom: 12 }}
                        />
                        <Typography.Text strong style={{ display: 'block', marginBottom: 4 }}>
                            {t('license.generation.result.signedJwt')}
                        </Typography.Text>
                        <Input.TextArea
                            readOnly
                            value={issuedJwt}
                            autoSize={{ minRows: 4, maxRows: 10 }}
                            style={{ fontFamily: 'ui-monospace, monospace', wordBreak: 'break-all' }}
                        />
                    </>
                ) : null}
            </Modal>

            {issued && issued.success && issued.licenseKey && issuedJwt ? (
                <IssuedLicenseResult
                    issued={issued}
                    formatLocale={formatLocale}
                    machineFingerprint={machineFingerprint}
                />
            ) : null}
        </Card>
    );
}

function buildCashRegisterActivateUrl(jwt: string): string {
    return `cashregister://license/activate?token=${encodeURIComponent(jwt)}`;
}

function buildAdminLicenseDeepLink(machineFingerprint: string): string {
    if (typeof window === 'undefined') {
        return '';
    }
    const origin = window.location.origin;
    return `${origin}${buildAdminLicensePageHref({
        machineHash: machineFingerprint,
        intent: ADMIN_LICENSE_PAGE_INTENT_EXTEND,
    })}`;
}

function IssuedLicenseResult({
    issued,
    formatLocale,
    machineFingerprint,
}: {
    issued: GenerateLicenseResponse;
    formatLocale: string;
    machineFingerprint: string;
}) {
    const { message } = useAntdApp();
    const { t } = useI18n();
    const jwt = pickJwt(issued);

    const formattedExpiry = issued.expiryAtUtc
        ? formatGermanDateTime(issued.expiryAtUtc)
        : '—';

    return (
        <div style={{ marginTop: 16 }}>
            <Alert
                type="info"
                showIcon
                title={t('license.generation.result.title')}
                description={t('license.generation.result.warning')}
                style={{ marginBottom: 12 }}
            />
            <Descriptions bordered column={1} size="small">
                <Descriptions.Item label={t('license.generation.result.licenseKey')}>
                    <CopyableMono value={issued.licenseKey ?? ''} />
                </Descriptions.Item>
                <Descriptions.Item label={t('license.generation.result.signedJwt')}>
                    <CopyableMono value={jwt} multiline />
                </Descriptions.Item>
                <Descriptions.Item label={t('license.generation.result.expiry')}>
                    {formattedExpiry}
                </Descriptions.Item>
            </Descriptions>
            <Space wrap style={{ marginTop: 12 }}>
                {jwt ? (
                    <Button
                        type="default"
                        onClick={async () => {
                            const url = buildCashRegisterActivateUrl(jwt);
                            const ok = await copyTextToClipboard(url);
                            message[ok ? 'success' : 'error'](
                                ok
                                    ? t('license.generation.result.posDeepLinkCopied')
                                    : t('license.generation.result.copyFailed'),
                            );
                        }}
                    >
                        {t('license.generation.result.posDeepLinkCopy')}
                    </Button>
                ) : null}
                {issued.licenseKey ? (
                    <Button
                        type="default"
                        onClick={async () => {
                            const ok = await copyTextToClipboard(issued.licenseKey ?? '');
                            message[ok ? 'success' : 'error'](
                                ok
                                    ? t('license.generation.result.licenseKeyCopied')
                                    : t('license.generation.result.copyFailed'),
                            );
                        }}
                    >
                        {t('license.generation.result.copyLicenseKeyOnly')}
                    </Button>
                ) : null}
                {jwt ? (
                    <Button
                        type="primary"
                        onClick={async () => {
                            const ok = await copyTextToClipboard(jwt);
                            message[ok ? 'success' : 'error'](
                                ok
                                    ? t('license.generation.modal.jwtCopied')
                                    : t('license.generation.result.copyFailed'),
                            );
                        }}
                    >
                        {t('license.generation.modal.copyJwt')}
                    </Button>
                ) : null}
                {machineFingerprint.trim() ? (
                    <Button
                        type="default"
                        onClick={async () => {
                            const url = buildAdminLicenseDeepLink(machineFingerprint.trim());
                            if (!url) {
                                message.error(t('license.generation.result.copyFailed'));
                                return;
                            }
                            const ok = await copyTextToClipboard(url);
                            message[ok ? 'success' : 'error'](
                                ok
                                    ? t('license.generation.result.adminPageLinkCopied')
                                    : t('license.generation.result.copyFailed'),
                            );
                        }}
                    >
                        {t('license.generation.result.adminPageLinkCopy')}
                    </Button>
                ) : null}
            </Space>
            {jwt ? (
                <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                    {t('license.generation.result.posDeepLinkHelp')}
                </Typography.Paragraph>
            ) : null}
        </div>
    );
}

async function copyTextToClipboard(value: string): Promise<boolean> {
    try {
        await navigator.clipboard.writeText(value);
        return true;
    } catch {
        const ta = document.createElement('textarea');
        ta.value = value;
        ta.style.position = 'fixed';
        ta.style.opacity = '0';
        document.body.appendChild(ta);
        ta.select();
        try {
            document.execCommand('copy');
            return true;
        } catch {
            return false;
        } finally {
            ta.remove();
        }
    }
}

function CopyableMono({ value, multiline = false }: { value: string; multiline?: boolean }) {
    const { message } = useAntdApp();
    const { t } = useI18n();

    const onCopy = async () => {
        try {
            await navigator.clipboard.writeText(value);
            message.success(t('license.generation.result.copied'));
        } catch {
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
