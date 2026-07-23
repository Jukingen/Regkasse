'use client';

import { DownloadOutlined, EyeOutlined, MailOutlined, PrinterOutlined } from '@ant-design/icons';
import { Button, Descriptions, Input, Modal, Space, Tag, Tooltip } from 'antd';
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import type { InvoiceStatus } from '@/api/generated/model';
import { InvoiceStatus as InvoiceStatusEnum } from '@/api/generated/model';
import { useTenant } from '@/features/tenancy/providers/TenantProvider';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { createIntlFormatters } from '@/i18n/formatting';
import {
  getAxiosResponseDataString,
  getAxiosResponseStatus,
} from '@/shared/contract/httpErrorShape';

import { getInvoicePdf, getInvoicePreview, resendInvoiceEmail } from '../api/invoiceService';
import { buildInvoicePdfFileName } from '../utils/invoiceExportFileName';
import {
  isValidInvoiceRecipientEmail,
  resolveInvoiceRecipientEmail,
} from '../utils/invoiceEmailValidation';

export type InvoiceActionsInvoice = {
  id: string;
  invoiceNumber?: string | null;
  kassenId?: string | null;
  totalAmount?: number;
  status?: InvoiceStatus;
  customerEmail?: string | null;
};

interface InvoiceActionsProps {
  invoice: InvoiceActionsInvoice;
  size?: 'small' | 'middle';
  onSuccess?: () => void;
}

function buildStatusMap(
  t: (key: string) => string
): Record<number, { label: string; color: string }> {
  return {
    0: { label: t('invoices.status.draft'), color: 'default' },
    1: { label: t('invoices.status.sent'), color: 'processing' },
    2: { label: t('invoices.status.paid'), color: 'success' },
    3: { label: t('invoices.status.partiallyPaid'), color: 'warning' },
    4: { label: t('invoices.status.unpaid'), color: 'error' },
    5: { label: t('invoices.status.overdue'), color: 'error' },
    6: { label: t('invoices.status.cancelled'), color: 'default' },
    7: { label: t('invoices.status.creditNote'), color: 'purple' },
  };
}

export const InvoiceActions: React.FC<InvoiceActionsProps> = ({
  invoice,
  size = 'middle',
  onSuccess,
}) => {
  const { message } = useAntdApp();
  const { t, formatLocale } = useI18n();
  const { tenant } = useTenant();
  const fmt = useMemo(() => createIntlFormatters(formatLocale), [formatLocale]);
  const statusMap = useMemo(() => buildStatusMap(t), [t]);

  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [resendModalOpen, setResendModalOpen] = useState(false);
  const [recipientEmail, setRecipientEmail] = useState('');
  const [emailError, setEmailError] = useState<string | null>(null);
  const [resending, setResending] = useState(false);
  const previewIframeRef = useRef<HTMLIFrameElement>(null);

  const displayNumber = invoice.invoiceNumber?.trim() || invoice.id;
  const statusCode = invoice.status ?? InvoiceStatusEnum.NUMBER_0;
  const statusInfo = statusMap[statusCode] ?? {
    label: t('invoices.status.unknown'),
    color: 'default',
  };

  const revokePdfUrl = useCallback(() => {
    setPdfUrl((current) => {
      if (current) URL.revokeObjectURL(current);
      return null;
    });
  }, []);

  useEffect(() => () => revokePdfUrl(), [revokePdfUrl]);

  useEffect(() => {
    if (resendModalOpen) {
      setRecipientEmail(invoice.customerEmail?.trim() ?? '');
      setEmailError(null);
    }
  }, [resendModalOpen, invoice.customerEmail]);

  const handlePreview = async () => {
    setPreviewOpen(true);
    setPreviewLoading(true);
    revokePdfUrl();
    try {
      const blob = await getInvoicePreview(invoice.id);
      setPdfUrl(URL.createObjectURL(new Blob([blob], { type: 'application/pdf' })));
    } catch (err: unknown) {
      const status = getAxiosResponseStatus(err);
      if (status === 401) {
        message.error(t('invoices.messages.pdfSessionExpired'));
      } else if (status === 404) {
        message.error(t('invoices.messages.pdfNotFound'));
      } else {
        message.error(t('invoices.messages.pdfFailed'));
      }
      setPreviewOpen(false);
    } finally {
      setPreviewLoading(false);
    }
  };

  const handleDownload = async () => {
    try {
      const blob = await getInvoicePdf(invoice.id);
      const url = URL.createObjectURL(new Blob([blob], { type: 'application/pdf' }));
      const link = document.createElement('a');
      link.href = url;
      link.download = buildInvoicePdfFileName(
        tenant?.slug,
        invoice.kassenId,
        invoice.invoiceNumber ?? displayNumber
      );
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (err: unknown) {
      const status = getAxiosResponseStatus(err);
      if (status === 401) {
        message.error(t('invoices.messages.pdfSessionExpired'));
      } else if (status === 404) {
        message.error(t('invoices.messages.pdfNotFound'));
      } else {
        message.error(t('invoices.messages.pdfFailed'));
      }
    }
  };

  const handlePrint = async () => {
    if (previewOpen && previewIframeRef.current?.contentWindow) {
      previewIframeRef.current.contentWindow.focus();
      previewIframeRef.current.contentWindow.print();
      return;
    }

    try {
      const blob = await getInvoicePdf(invoice.id);
      const url = URL.createObjectURL(new Blob([blob], { type: 'application/pdf' }));
      const printWindow = window.open(url, '_blank');
      if (!printWindow) {
        URL.revokeObjectURL(url);
        message.error(t('invoices.messages.pdfFailed'));
        return;
      }
      printWindow.addEventListener('load', () => {
        printWindow.print();
      });
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (err: unknown) {
      const status = getAxiosResponseStatus(err);
      if (status === 401) {
        message.error(t('invoices.messages.pdfSessionExpired'));
      } else if (status === 404) {
        message.error(t('invoices.messages.pdfNotFound'));
      } else {
        message.error(t('invoices.messages.pdfFailed'));
      }
    }
  };

  const handleResend = async () => {
    const recipient = resolveInvoiceRecipientEmail(recipientEmail, invoice.customerEmail);
    if (!recipient) {
      setEmailError(t('invoices.invoiceActions.emailRequired'));
      message.error(t('invoices.invoiceActions.emailRequired'));
      return;
    }
    if (!isValidInvoiceRecipientEmail(recipient)) {
      setEmailError(t('invoices.resend.formEmailInvalid'));
      message.error(t('invoices.resend.formEmailInvalid'));
      return;
    }

    setEmailError(null);
    setResending(true);
    try {
      const result = await resendInvoiceEmail(invoice.id, recipientEmail.trim() || undefined);
      if (result.message) {
        message.success(t('invoices.messages.resendOk', { recipient }));
        setResendModalOpen(false);
        onSuccess?.();
      } else {
        message.error(result.error ?? t('invoices.messages.resendFailed'));
      }
    } catch (err: unknown) {
      message.error(getAxiosResponseDataString(err) ?? t('invoices.messages.resendFailed'));
    } finally {
      setResending(false);
    }
  };

  const closePreview = () => {
    setPreviewOpen(false);
    revokePdfUrl();
  };

  return (
    <>
      <Space size={4} wrap>
        <Tooltip title={t('invoices.invoiceActions.previewTooltip')}>
          <Button
            size={size}
            icon={<EyeOutlined />}
            aria-label={t('invoices.invoiceActions.previewTooltip')}
            onClick={() => void handlePreview()}
          />
        </Tooltip>
        <Tooltip title={t('invoices.invoiceActions.downloadTooltip')}>
          <Button
            size={size}
            icon={<DownloadOutlined />}
            aria-label={t('invoices.invoiceActions.downloadTooltip')}
            onClick={() => void handleDownload()}
          />
        </Tooltip>
        <Tooltip title={t('invoices.invoiceActions.printTooltip')}>
          <Button
            size={size}
            icon={<PrinterOutlined />}
            aria-label={t('invoices.invoiceActions.printTooltip')}
            onClick={() => void handlePrint()}
          />
        </Tooltip>
        <Tooltip title={t('invoices.invoiceActions.mailTooltip')}>
          <Button
            size={size}
            icon={<MailOutlined />}
            aria-label={t('invoices.invoiceActions.mailTooltip')}
            onClick={() => setResendModalOpen(true)}
          />
        </Tooltip>
      </Space>

      <Modal
        title={t('invoices.invoiceActions.previewModalTitle', { invoiceNumber: displayNumber })}
        open={previewOpen}
        onCancel={closePreview}
        width="90%"
        style={{ top: 20 }}
        footer={[
          <Button key="download" icon={<DownloadOutlined />} onClick={() => void handleDownload()}>
            {t('invoices.invoiceActions.download')}
          </Button>,
          <Button key="print" icon={<PrinterOutlined />} onClick={() => void handlePrint()}>
            {t('invoices.invoiceActions.print')}
          </Button>,
          <Button key="close" onClick={closePreview}>
            {t('invoices.invoiceActions.close')}
          </Button>,
        ]}
      >
        {previewLoading ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
            {t('invoices.detail.pdfPreviewLoading')}
          </div>
        ) : pdfUrl ? (
          <iframe
            ref={previewIframeRef}
            src={pdfUrl}
            style={{ width: '100%', height: '80vh', border: 'none' }}
            title={t('invoices.invoiceActions.previewTooltip')}
          />
        ) : (
          <div style={{ textAlign: 'center', padding: 24 }}>
            {t('invoices.detail.pdfPreviewFailed')}
          </div>
        )}
      </Modal>

      <Modal
        title={t('invoices.resend.modalTitle', { invoiceNumber: displayNumber })}
        open={resendModalOpen}
        onCancel={() => setResendModalOpen(false)}
        onOk={() => void handleResend()}
        confirmLoading={resending}
        okText={t('invoices.resend.modalOk')}
        cancelText={t('invoices.resend.modalCancel')}
      >
        <Descriptions bordered column={1} size="small" style={{ marginBottom: 16 }}>
          <Descriptions.Item label={t('invoices.invoiceActions.resendInvoiceNumber')}>
            {displayNumber}
          </Descriptions.Item>
          <Descriptions.Item label={t('invoices.invoiceActions.resendAmount')}>
            {fmt.formatCurrency(invoice.totalAmount ?? 0)}
          </Descriptions.Item>
          <Descriptions.Item label={t('invoices.detail.statusLabel')}>
            <Tag color={statusInfo.color}>{statusInfo.label}</Tag>
          </Descriptions.Item>
        </Descriptions>

        <div>
          <div style={{ fontWeight: 500, marginBottom: 8 }}>
            {t('invoices.resend.formEmailLabel')}
          </div>
          <Input
            type="email"
            value={recipientEmail}
            onChange={(e) => {
              setRecipientEmail(e.target.value);
              if (emailError) setEmailError(null);
            }}
            onBlur={() => {
              const candidate = resolveInvoiceRecipientEmail(recipientEmail, invoice.customerEmail);
              if (candidate && !isValidInvoiceRecipientEmail(candidate)) {
                setEmailError(t('invoices.resend.formEmailInvalid'));
              }
            }}
            status={emailError ? 'error' : undefined}
            placeholder={t('invoices.resend.formEmailPlaceholder')}
            prefix={<MailOutlined />}
          />
          {emailError ? (
            <div style={{ fontSize: 12, color: '#ff4d4f', marginTop: 8 }}>{emailError}</div>
          ) : null}
          <div style={{ fontSize: 12, color: 'rgba(0,0,0,0.45)', marginTop: 8 }}>
            {t('invoices.resend.modalDescription')}
          </div>
        </div>
      </Modal>
    </>
  );
};
