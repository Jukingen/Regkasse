'use client';

import { useParams, useRouter } from 'next/navigation';
import { Card, Descriptions, Tag, Button, Space, Spin, Form, Input } from 'antd';
import { FilePdfOutlined, DeleteOutlined, ArrowLeftOutlined } from '@ant-design/icons';
import { useState } from 'react';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useBillingSale, useCancelLicenseSale } from '@/features/billing/hooks';
import { BillingAccessGate } from '@/features/billing/components/BillingAccessGate';
import { formatDate } from '@/lib/dateFormatter';
import { downloadLicenseSaleInvoicePdf } from '@/features/billing/utils/downloadInvoicePdf';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';
import { useI18n } from '@/i18n';

const CANCEL_REASON_MIN_LENGTH = 10;

function resolveStatusPresentation(status: string | null | undefined): { color: string; label: string } {
    switch (status) {
        case 'active':
            return { color: 'green', label: 'Aktiv' };
        case 'cancelled':
            return { color: 'red', label: 'Storniert' };
        case 'refunded':
            return { color: 'orange', label: 'Rückerstattet' };
        default:
            return { color: 'default', label: status ?? '—' };
    }
}

function daysUntil(validUntilUtc: string | undefined): number | null {
    if (!validUntilUtc) return null;
    const end = new Date(validUntilUtc).getTime();
    if (Number.isNaN(end)) return null;
    return Math.ceil((end - Date.now()) / (1000 * 60 * 60 * 24));
}

export default function BillingSaleDetailPage() {
    const params = useParams<{ id: string }>();
    const id = typeof params.id === 'string' ? params.id : '';
    const router = useRouter();
    const { message, modal } = useAntdApp();
    const { t } = useI18n();
    const [cancelForm] = Form.useForm<{ cancellationReason: string }>();
    const [isCancelling, setIsCancelling] = useState(false);
    const [pdfLoading, setPdfLoading] = useState(false);

    const { data: sale, isLoading, refetch } = useBillingSale(id);
    const cancelMutation = useCancelLicenseSale();

    const handlePdfDownload = async () => {
        if (!sale?.id) return;
        setPdfLoading(true);
        try {
            await downloadLicenseSaleInvoicePdf(
                sale.id,
                sale.invoiceNumber ? `${sale.invoiceNumber}.pdf` : undefined,
            );
        } catch (err) {
            openApiErrorMessage(message.open, t, err, { logContext: 'BillingSaleDetailPage.downloadPdf' });
        } finally {
            setPdfLoading(false);
        }
    };

    const handleCancel = () => {
        if (!id) return;
        cancelForm.resetFields();
        modal.confirm({
            title: 'Lizenzverkauf stornieren',
            content: (
                <div>
                    <p>Möchten Sie diesen Lizenzverkauf wirklich stornieren?</p>
                    <p style={{ color: '#dc2626', fontSize: 12 }}>
                        Die Lizenz wird deaktiviert und der Mandant verliert den Zugang.
                    </p>
                    <Form form={cancelForm} layout="vertical" style={{ marginTop: 16 }}>
                        <Form.Item
                            name="cancellationReason"
                            label="Stornierungsgrund"
                            rules={[
                                { required: true, message: 'Bitte einen Grund angeben (mind. 10 Zeichen).' },
                                { min: CANCEL_REASON_MIN_LENGTH, message: 'Bitte mindestens 10 Zeichen eingeben.' },
                            ]}
                            initialValue="Storniert durch Administrator"
                        >
                            <Input.TextArea rows={3} />
                        </Form.Item>
                    </Form>
                </div>
            ),
            okText: 'Ja, stornieren',
            okType: 'danger',
            cancelText: 'Abbrechen',
            onOk: async () => {
                const values = await cancelForm.validateFields();
                setIsCancelling(true);
                try {
                    await cancelMutation.mutateAsync({
                        id,
                        data: { cancellationReason: values.cancellationReason },
                    });
                    message.success('Lizenzverkauf wurde storniert');
                    await refetch();
                } catch (err) {
                    openApiErrorMessage(message.open, t, err, { logContext: 'BillingSaleDetailPage.cancelSale' });
                }
            },
        });
    };

    if (isLoading) {
        return (
            <BillingAccessGate>
                <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
                    <Spin size="large" />
                </div>
            </BillingAccessGate>
        );
    }

    if (!sale) {
        return (
            <BillingAccessGate>
                <div style={{ padding: 24 }}>Verkauf nicht gefunden</div>
            </BillingAccessGate>
        );
    }

    const { color: statusColor, label: statusLabel } = resolveStatusPresentation(sale.status);
    const remainingDays = daysUntil(sale.validUntilUtc);

    return (
        <BillingAccessGate>
            <div style={{ padding: 24 }}>
                <Space orientation="vertical" size="large" style={{ width: '100%' }}>
                    <div
                        style={{
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center',
                            flexWrap: 'wrap',
                            gap: 16,
                        }}
                    >
                        <Space wrap>
                            <Button icon={<ArrowLeftOutlined />} onClick={() => router.back()}>
                                Zurück
                            </Button>
                            <h1 style={{ margin: 0 }}>Lizenzverkauf {sale.invoiceNumber ?? sale.id}</h1>
                            <Tag color={statusColor}>{statusLabel}</Tag>
                        </Space>
                        <Space wrap>
                            <Button
                                icon={<FilePdfOutlined />}
                                loading={pdfLoading}
                                onClick={() => void handlePdfDownload()}
                            >
                                PDF Herunterladen
                            </Button>
                            {sale.status === 'active' ? (
                                <Button
                                    danger
                                    icon={<DeleteOutlined />}
                                    onClick={handleCancel}
                                    loading={isCancelling}
                                >
                                    Stornieren
                                </Button>
                            ) : null}
                        </Space>
                    </div>

                    <Card>
                        <Descriptions column={{ xs: 1, sm: 2, md: 3 }} bordered size="small">
                            <Descriptions.Item label="Rechnungsnummer">{sale.invoiceNumber ?? '—'}</Descriptions.Item>
                            <Descriptions.Item label="Datum">
                                {sale.soldAtUtc ? formatDate(sale.soldAtUtc) : '—'}
                            </Descriptions.Item>
                            <Descriptions.Item label="Bearbeiter">{sale.soldBy ?? 'System'}</Descriptions.Item>

                            <Descriptions.Item label="Mandant">{sale.tenantName ?? '—'}</Descriptions.Item>
                            <Descriptions.Item label="Slug">{sale.tenantSlug ?? '—'}</Descriptions.Item>
                            <Descriptions.Item label="Lizenzplan">{sale.licensePlan ?? '—'}</Descriptions.Item>

                            <Descriptions.Item label="Lizenzschlüssel" span={2}>
                                <code>{sale.licenseKey ?? '—'}</code>
                            </Descriptions.Item>
                            <Descriptions.Item label="Status">
                                <Tag color={statusColor}>{statusLabel}</Tag>
                            </Descriptions.Item>

                            <Descriptions.Item label="Gültig ab">
                                {sale.validFromUtc ? formatDate(sale.validFromUtc) : '—'}
                            </Descriptions.Item>
                            <Descriptions.Item label="Gültig bis">
                                <span style={{ fontWeight: 600 }}>
                                    {sale.validUntilUtc ? formatDate(sale.validUntilUtc) : '—'}
                                </span>
                            </Descriptions.Item>
                            <Descriptions.Item label="Tage">
                                {remainingDays != null ? `${remainingDays} Tage` : '—'}
                            </Descriptions.Item>

                            <Descriptions.Item label="Preis (Netto)">
                                {sale.priceNet != null ? `€ ${sale.priceNet.toFixed(2)}` : '—'}
                            </Descriptions.Item>
                            <Descriptions.Item label={`MwSt. (${sale.vatRate ?? 0}%)`}>
                                {sale.vatAmount != null ? `€ ${sale.vatAmount.toFixed(2)}` : '—'}
                            </Descriptions.Item>
                            <Descriptions.Item label="Preis (Brutto)">
                                <span style={{ fontSize: 18, fontWeight: 700, color: '#1a56db' }}>
                                    {sale.priceGross != null ? `€ ${sale.priceGross.toFixed(2)}` : '—'}
                                </span>
                            </Descriptions.Item>
                        </Descriptions>
                    </Card>

                    {sale.notes ? (
                        <Card title="Notizen">
                            <p style={{ whiteSpace: 'pre-wrap', margin: 0 }}>{sale.notes}</p>
                        </Card>
                    ) : null}

                    {sale.status === 'cancelled' && sale.cancellationReason ? (
                        <Card title="Stornierungsinformationen" style={{ borderColor: '#dc2626' }}>
                            <p>
                                <strong>Grund:</strong> {sale.cancellationReason}
                            </p>
                            <p style={{ marginBottom: 0 }}>
                                <strong>Datum:</strong>{' '}
                                {sale.cancelledAtUtc ? formatDate(sale.cancelledAtUtc) : '—'}
                            </p>
                        </Card>
                    ) : null}
                </Space>
            </div>
        </BillingAccessGate>
    );
}
