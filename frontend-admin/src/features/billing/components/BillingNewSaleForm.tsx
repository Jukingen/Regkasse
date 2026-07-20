'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    Col,
    DatePicker,
    Descriptions,
    Divider,
    Form,
    Input,
    InputNumber,
    Modal,
    Row,
    Select,
    Space,
    Tag,
} from 'antd';
import { CheckCircleOutlined, FilePdfOutlined } from '@ant-design/icons';
import { useRouter } from 'next/navigation';
import dayjs, { type Dayjs } from 'dayjs';
import { useQuery } from '@tanstack/react-query';
import { CardSkeleton } from '@/components/Skeleton';
import { useQueryClient } from '@tanstack/react-query';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n, formatCurrency, formatGermanDateTime } from '@/i18n';
import { billingApi } from '@/features/billing/api/billingApi';
import type { LicenseSalePreviewResponse } from '@/api/generated/model';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import {
    DEFAULT_LICENSE_VAT_RATE,
    LICENSE_SALE_PLAN_OPTIONS,
    LICENSE_SALE_PLAN_VALUES,
} from '@/features/billing/constants/licensePlans';
import { billingQueryKeys } from '@/features/billing/constants/billingQueryKeys';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';
import { fetchLicenseSalePreviewPdf } from '@/features/billing/utils/previewInvoicePdf';

type NewSaleFormValues = {
    tenantId: string;
    licensePlan: string;
    priceNet: number;
    vatRate: number;
    customValidUntilUtc?: Dayjs;
    notes?: string;
};

export function BillingNewSaleForm({ initialTenantId }: { initialTenantId?: string } = {}) {
    const { t, formatLocale } = useI18n();
    const { message } = useAntdApp();
    const router = useRouter();
    const queryClient = useQueryClient();
    const canAccess = useBillingAccess();
    const [form] = Form.useForm<NewSaleFormValues>();
    const [preview, setPreview] = useState<LicenseSalePreviewResponse | null>(null);
    const [showPdfModal, setShowPdfModal] = useState(false);
    const [pdfUrl, setPdfUrl] = useState<string>();
    const [pdfLoading, setPdfLoading] = useState(false);

    const selectedTenantId = Form.useWatch('tenantId', form);
    const selectedPlan = Form.useWatch('licensePlan', form);
    const isCustomPlan = selectedPlan === LICENSE_SALE_PLAN_VALUES.custom;

    const tenantsQuery = useQuery({
        queryKey: ['admin-tenants', 'billing-new-sale'],
        queryFn: () => listAdminTenants(false),
        enabled: canAccess,
    });

    const tenantLicenseQuery = billingApi.useTenantLicense(selectedTenantId ?? '', {
        query: {
            enabled: canAccess && !!selectedTenantId,
            queryKey: selectedTenantId
                ? billingQueryKeys.tenantLicense(selectedTenantId)
                : undefined,
        },
    });

    const tenantOptions = useMemo(
        () =>
            (tenantsQuery.data ?? []).map((tenant) => ({
                value: tenant.id,
                label: `${tenant.name} (${tenant.slug})`,
            })),
        [tenantsQuery.data],
    );

    const planOptions = useMemo(
        () => LICENSE_SALE_PLAN_OPTIONS.map((opt) => ({ value: opt.value, label: t(opt.labelKey) })),
        [t],
    );

    const previewMutation = billingApi.usePreview({
        mutation: {
            onSuccess: (data) => {
                setPreview(data);
                message.success(t('billing.new.previewSuccess'));
            },
            onError: (err) =>
                openApiErrorMessage(message.open, t, err, { logContext: 'BillingNewSaleForm.preview' }),
        },
    });

    const createMutation = billingApi.useCreate({
        mutation: {
            onSuccess: async (sale) => {
                if (sale.invoiceNumber) {
                    message.success(
                        t('billing.new.createSuccess', { invoiceNumber: sale.invoiceNumber }),
                    );
                } else {
                    message.success(t('billing.new.createSuccessGeneric'));
                }
                await queryClient.invalidateQueries({ queryKey: billingQueryKeys.all });
                if (sale.id) {
                    router.push(`/admin/billing/sales/${sale.id}`);
                }
            },
            onError: (err) =>
                openApiErrorMessage(message.open, t, err, { logContext: 'BillingNewSaleForm.create' }),
        },
    });

    const buildPayload = useCallback(
        (values: NewSaleFormValues) => ({
            tenantId: values.tenantId,
            licensePlan: values.licensePlan,
            priceNet: values.priceNet,
            vatRate: values.vatRate,
            notes: values.notes?.trim() || undefined,
            customValidUntilUtc: isCustomPlan ? values.customValidUntilUtc?.endOf('day').toISOString() : undefined,
        }),
        [isCustomPlan],
    );

    const handleTenantChange = () => {
        setPreview(null);
    };

    const handlePreview = async () => {
        const values = await form.validateFields();
        await previewMutation.mutateAsync({ data: buildPayload(values) });
    };

    const handleCreate = async () => {
        const values = await form.validateFields();
        await createMutation.mutateAsync({ data: buildPayload(values) });
    };

    const closePdfModal = useCallback(() => {
        setShowPdfModal(false);
        setPdfUrl((current) => {
            if (current) {
                globalThis.URL.revokeObjectURL(current);
            }
            return undefined;
        });
    }, []);

    useEffect(() => {
        return () => {
            if (pdfUrl) {
                globalThis.URL.revokeObjectURL(pdfUrl);
            }
        };
    }, [pdfUrl]);

    useEffect(() => {
        const tenantId = initialTenantId?.trim();
        if (!tenantId) return;
        form.setFieldValue('tenantId', tenantId);
    }, [form, initialTenantId]);

    const handlePdfPreview = async () => {
        if (!preview) return;

        setPdfLoading(true);
        try {
            const values = await form.validateFields();
            const blob = await fetchLicenseSalePreviewPdf(buildPayload(values));
            const url = globalThis.URL.createObjectURL(blob);
            setPdfUrl(url);
            setShowPdfModal(true);
        } catch (err) {
            openApiErrorMessage(message.open, t, err, {
                logContext: 'BillingNewSaleForm.previewPdf',
                fallbackKey: 'billing.new.pdfPreviewError',
            });
        } finally {
            setPdfLoading(false);
        }
    };

    const handlePdfDownload = () => {
        if (!pdfUrl) return;
        const anchor = document.createElement('a');
        anchor.href = pdfUrl;
        anchor.download = preview?.tenantSlug
            ? `Vorschau-${preview.tenantSlug}.pdf`
            : 'license-preview.pdf';
        anchor.click();
    };

    const tenantLicense = tenantLicenseQuery.data;
    const licenseStatus = tenantLicense?.status;

    return (
        <>
            <Row gutter={[24, 24]}>
                <Col xs={24} lg={12}>
                    <Card title={t('billing.new.licenseDetails')} variant="borderless">
                        <Form
                            form={form}
                            layout="vertical"
                            initialValues={{
                                vatRate: DEFAULT_LICENSE_VAT_RATE,
                                licensePlan: LICENSE_SALE_PLAN_VALUES.twelveMonths,
                            }}
                        >
                            <Form.Item
                                name="tenantId"
                                label={t('billing.new.tenantLabel')}
                                rules={[{ required: true, message: t('billing.new.tenantRequired') }]}
                            >
                                <Select
                                    showSearch
                                    optionFilterProp="label"
                                    options={tenantOptions}
                                    loading={tenantsQuery.isLoading}
                                    onChange={handleTenantChange}
                                    placeholder={t('billing.new.selectTenant')}
                                />
                            </Form.Item>

                            {selectedTenantId && tenantLicense ? (
                                <Alert
                                    title={t('billing.new.currentLicense')}
                                    description={
                                        <div>
                                            <p style={{ marginBottom: 4 }}>
                                                <strong>{t('billing.new.status')}:</strong>{' '}
                                                <Tag color={licenseStatus?.isValid ? 'green' : 'red'}>
                                                    {licenseStatus?.isValid
                                                        ? t('billing.new.active')
                                                        : t('billing.new.inactive')}
                                                </Tag>
                                            </p>
                                            {licenseStatus?.validUntilUtc ? (
                                                <p style={{ marginBottom: 0 }}>
                                                    <strong>{t('billing.new.validUntil')}:</strong>{' '}
                                                    {formatGermanDateTime(licenseStatus.validUntilUtc)}
                                                    {licenseStatus.daysRemaining != null ? (
                                                        <span style={{ marginLeft: 8 }}>
                                                            (
                                                            {t('billing.new.daysRemaining', {
                                                                days: licenseStatus.daysRemaining,
                                                            })}
                                                            )
                                                        </span>
                                                    ) : null}
                                                </p>
                                            ) : null}
                                        </div>
                                    }
                                    type={licenseStatus?.isValid ? 'info' : 'warning'}
                                    style={{ marginBottom: 16 }}
                                />
                            ) : null}

                            <Form.Item
                                name="licensePlan"
                                label={t('billing.new.planLabel')}
                                rules={[{ required: true, message: t('billing.new.planRequired') }]}
                            >
                                <Select options={planOptions} placeholder={t('billing.new.selectPlan')} />
                            </Form.Item>

                            {isCustomPlan ? (
                                <Form.Item
                                    name="customValidUntilUtc"
                                    label={t('billing.new.customEndDate')}
                                    rules={[
                                        { required: true, message: t('billing.new.customEndDateRequired') },
                                    ]}
                                >
                                    <DatePicker
                                        style={{ width: '100%' }}
                                        disabledDate={(d) => d.isBefore(dayjs(), 'day')}
                                    />
                                </Form.Item>
                            ) : null}

                            <Row gutter={16}>
                                <Col span={12}>
                                    <Form.Item
                                        name="priceNet"
                                        label={t('billing.new.priceNet')}
                                        rules={[
                                            { required: true, message: t('billing.new.priceNetRequired') },
                                        ]}
                                    >
                                        <InputNumber
                                            min={0.01}
                                            max={999999.99}
                                            step={0.01}
                                            style={{ width: '100%' }}
                                        />
                                    </Form.Item>
                                </Col>
                                <Col span={12}>
                                    <Form.Item name="vatRate" label={t('billing.new.vatRate')}>
                                        <InputNumber
                                            min={0}
                                            max={100}
                                            step={1}
                                            style={{ width: '100%' }}
                                            addonAfter="%"
                                        />
                                    </Form.Item>
                                </Col>
                            </Row>

                            <Form.Item name="notes" label={t('billing.new.notes')}>
                                <Input.TextArea rows={3} />
                            </Form.Item>

                            <Form.Item>
                                <Space wrap>
                                    <Button
                                        type="primary"
                                        icon={<FilePdfOutlined />}
                                        onClick={() => void handlePreview()}
                                        loading={previewMutation.isPending}
                                    >
                                        {t('billing.new.preview')}
                                    </Button>
                                    <Button
                                        type="primary"
                                        icon={<CheckCircleOutlined />}
                                        onClick={() => void handleCreate()}
                                        loading={createMutation.isPending}
                                        disabled={!preview}
                                        style={
                                            preview
                                                ? { backgroundColor: '#16a34a', borderColor: '#16a34a' }
                                                : undefined
                                        }
                                    >
                                        {t('billing.new.complete')}
                                    </Button>
                                </Space>
                            </Form.Item>
                        </Form>
                    </Card>
                </Col>

                <Col xs={24} lg={12}>
                    <Card title={t('billing.new.previewTitle')} variant="borderless">
                        {previewMutation.isPending ? (
                            <CardSkeleton count={1} />
                        ) : preview ? (
                            <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                                <Descriptions column={1} size="small" bordered>
                                    <Descriptions.Item label={t('billing.new.licenseKey')}>
                                        <code>{preview.licenseKey ?? '—'}</code>
                                    </Descriptions.Item>
                                    <Descriptions.Item label={t('billing.new.invoiceNumber')}>
                                        {preview.invoiceNumber ?? '—'}
                                    </Descriptions.Item>
                                    <Descriptions.Item label={t('billing.new.validFrom')}>
                                        {preview.validFromUtc
                                            ? formatGermanDateTime(preview.validFromUtc)
                                            : '—'}
                                    </Descriptions.Item>
                                    <Descriptions.Item label={t('billing.new.validUntil')}>
                                        <strong>
                                            {preview.validUntilUtc
                                                ? formatGermanDateTime(preview.validUntilUtc)
                                                : '—'}
                                        </strong>
                                    </Descriptions.Item>
                                    <Descriptions.Item label={t('billing.new.duration')}>
                                        {preview.durationDisplay ?? '—'}
                                        {preview.durationDays != null ? ` (${preview.durationDays})` : ''}
                                    </Descriptions.Item>
                                </Descriptions>

                                <Divider />

                                <Descriptions column={1} size="small" bordered>
                                    <Descriptions.Item label={t('billing.new.net')}>
                                        {preview.priceNet != null
                                            ? formatCurrency(preview.priceNet, formatLocale, { currency: 'EUR' })
                                            : '—'}
                                    </Descriptions.Item>
                                    <Descriptions.Item
                                        label={t('billing.new.vat', { rate: preview.vatRate ?? 0 })}
                                    >
                                        {preview.vatAmount != null
                                            ? formatCurrency(preview.vatAmount, formatLocale, { currency: 'EUR' })
                                            : '—'}
                                    </Descriptions.Item>
                                    <Descriptions.Item label={t('billing.new.gross')}>
                                        <span style={{ fontSize: 18, fontWeight: 700, color: '#1a56db' }}>
                                            {preview.priceGross != null
                                                ? formatCurrency(preview.priceGross, formatLocale, {
                                                      currency: 'EUR',
                                                  })
                                                : '—'}
                                        </span>
                                    </Descriptions.Item>
                                </Descriptions>

                                <Divider />

                                <Alert
                                    title={t('billing.new.tenant')}
                                    description={
                                        <div>
                                            <p style={{ marginBottom: 4 }}>
                                                <strong>{preview.tenantName ?? '—'}</strong>
                                            </p>
                                            <p style={{ fontSize: 12, color: '#64748b', marginBottom: 0 }}>
                                                {preview.tenantSlug ?? '—'}
                                                {preview.tenantEmail ? ` · ${preview.tenantEmail}` : ''}
                                            </p>
                                        </div>
                                    }
                                    type="info"
                                />

                                <Button
                                    block
                                    icon={<FilePdfOutlined />}
                                    onClick={() => void handlePdfPreview()}
                                    loading={pdfLoading}
                                >
                                    {t('billing.new.pdfPreview')}
                                </Button>
                            </Space>
                        ) : (
                            <div style={{ textAlign: 'center', padding: 48, color: '#94a3b8' }}>
                                <FilePdfOutlined style={{ fontSize: 48, marginBottom: 16 }} />
                                <p style={{ marginBottom: 0 }}>{t('billing.new.noPreview')}</p>
                            </div>
                        )}
                    </Card>
                </Col>
            </Row>

            <Modal
                title={t('billing.new.pdfModalTitle')}
                open={showPdfModal}
                onCancel={closePdfModal}
                destroyOnHidden
                footer={[
                    <Button key="close" onClick={closePdfModal}>
                        {t('billing.new.close')}
                    </Button>,
                    <Button
                        key="download"
                        type="primary"
                        icon={<FilePdfOutlined />}
                        onClick={handlePdfDownload}
                    >
                        {t('billing.new.pdfDownload')}
                    </Button>,
                ]}
                width="90%"
                style={{ maxWidth: 900 }}
                styles={{ body: { height: '70vh', padding: 0 } }}
            >
                {pdfUrl ? (
                    <iframe
                        src={pdfUrl}
                        style={{ width: '100%', height: '100%', border: 'none' }}
                        title={t('billing.new.pdfModalTitle')}
                    />
                ) : null}
            </Modal>
        </>
    );
}
