'use client';

/**
 * Mehrstufiger Nachdruck-Flow: Vorschau (read-only API) → Gerät/Begründung → Bestätigung mit Audit (POST reprint-request).
 */
import { useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Descriptions,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Steps,
  Typography,
} from 'antd';
import { isAxiosError } from 'axios';
import Link from 'next/link';
import React, { useEffect, useMemo, useState } from 'react';

import {
  getApiAdminBackofficePrintRoutingOptions,
  postApiAdminBackofficeReceiptsPaymentIdReprintRequest,
} from '@/api/generated/admin/admin';
import type { ReceiptReprintRequest, ReceiptReprintResponse } from '@/api/generated/model';
import { getApiReceiptsByPaymentPaymentId } from '@/api/generated/receipts/receipts';
import { useI18n } from '@/i18n/I18nProvider';
import { formatEUR } from '@/shared/utils/currency';

export const REPRINT_REASON_VALUES = [
  'CUSTOMER_REQUEST',
  'PRINTER_FAILURE',
  'LEGAL_OR_AUDIT_COPY',
  'CORRECTION_REFERENCE',
  'OTHER',
] as const;

export type ReceiptReprintWizardProps = {
  open: boolean;
  onClose: () => void;
  paymentId: string;
  receiptNumberHint?: string;
};

export default function ReceiptReprintWizard(props: ReceiptReprintWizardProps) {
  if (!props.open) {
    return null;
  }
  return <ReceiptReprintWizardContent {...props} />;
}

function ReceiptReprintWizardContent({
  open,
  onClose,
  paymentId,
  receiptNumberHint,
}: ReceiptReprintWizardProps) {
  const { t } = useI18n();
  const [effectivePaymentId, setEffectivePaymentId] = useState(paymentId);
  const [step, setStep] = useState(0);
  const [form] = Form.useForm<{
    reprintReasonCode: string;
    reasonDetail?: string;
    deviceId?: string;
    note?: string;
  }>();
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<ReceiptReprintResponse | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const previewQuery = useQuery({
    queryKey: ['receipt-reprint-preview', effectivePaymentId],
    queryFn: () => getApiReceiptsByPaymentPaymentId(effectivePaymentId),
    enabled: open && !!effectivePaymentId?.trim(),
    staleTime: 0,
  });

  const routingOptionsQuery = useQuery({
    queryKey: ['operations-center', 'print-routing-options'],
    queryFn: () => getApiAdminBackofficePrintRoutingOptions(),
    enabled: open,
    staleTime: 300_000,
  });

  useEffect(() => {
    if (!open) {
      setStep(0);
      setResult(null);
      setSubmitError(null);
      form.resetFields();
    }
  }, [open, form]);

  const reasonOptions = useMemo(() => {
    const key = (v: string) => `adminShell.operationsCenter.reprintReason.${v}`;
    return REPRINT_REASON_VALUES.map((v) => ({
      value: v,
      label: t(key(v)),
    }));
  }, [t]);

  const deviceOptions = (routingOptionsQuery.data?.devices ?? []).map((d) => ({
    value: d.id ?? '',
    label:
      d.isSimulated === false
        ? (d.label ?? d.id)
        : `${d.label ?? d.id} (${t('adminShell.operationsCenter.reprintSimulatedTag')})`,
  }));

  const onConfirm = async () => {
    setSubmitting(true);
    setSubmitError(null);
    setResult(null);
    try {
      const v = await form.validateFields();
      const body: ReceiptReprintRequest = {
        reprintReasonCode: v.reprintReasonCode,
        reasonDetail: v.reasonDetail?.trim() || undefined,
        deviceId: v.deviceId || undefined,
        note: v.note?.trim() || undefined,
      };
      const data = await postApiAdminBackofficeReceiptsPaymentIdReprintRequest(
        effectivePaymentId.trim(),
        body
      );
      setResult(data);
      if (data.outcome === 'Success') {
        setStep(3);
      }
    } catch (e) {
      if (isAxiosError(e) && e.response?.data && typeof e.response.data === 'object') {
        const payload = e.response.data as ReceiptReprintResponse;
        setResult(payload);
        setSubmitError(
          payload.errorMessage ??
            payload.errorCode ??
            t('adminShell.operationsCenter.reprintFailedGeneric')
        );
        if (e.response.status === 400 || e.response.status === 404) {
          setStep(3);
        }
      } else {
        setSubmitError(e instanceof Error ? e.message : String(e));
      }
    } finally {
      setSubmitting(false);
    }
  };

  const receipt = previewQuery.data;
  const hasPaymentId = !!effectivePaymentId?.trim();

  return (
    <Modal
      title={t('adminShell.operationsCenter.reprintWorkflowTitle')}
      open={open}
      onCancel={onClose}
      width={760}
      footer={null}
      destroyOnHidden
    >
      <Steps
        current={step}
        style={{ marginBottom: 24 }}
        items={[
          { title: t('adminShell.operationsCenter.reprintStepPreview') },
          { title: t('adminShell.operationsCenter.reprintStepRouting') },
          { title: t('adminShell.operationsCenter.reprintStepConfirm') },
          { title: t('adminShell.operationsCenter.reprintStepOutcome') },
        ]}
      />

      <Form
        form={form}
        layout="vertical"
        preserve
        style={{ display: step === 1 ? undefined : 'none' }}
        initialValues={{ deviceId: 'default-register', reprintReasonCode: 'CUSTOMER_REQUEST' }}
      >
        <Form.Item
          name="reprintReasonCode"
          label={t('adminShell.operationsCenter.reprintReasonLabel')}
          rules={[
            { required: true, message: t('adminShell.operationsCenter.reprintReasonRequired') },
          ]}
        >
          <Select options={reasonOptions} showSearch optionFilterProp="label" />
        </Form.Item>
        <Form.Item
          name="reasonDetail"
          label={t('adminShell.operationsCenter.reprintReasonDetailLabel')}
        >
          <Input.TextArea rows={2} maxLength={500} showCount />
        </Form.Item>
        <Form.Item name="deviceId" label={t('adminShell.operationsCenter.deviceLabel')}>
          <Select
            allowClear
            loading={routingOptionsQuery.isLoading}
            options={deviceOptions}
            placeholder={t('adminShell.operationsCenter.devicePlaceholder')}
          />
        </Form.Item>
        <Form.Item name="note" label={t('adminShell.operationsCenter.reprintNoteLabel')}>
          <Input.TextArea rows={2} maxLength={500} showCount />
        </Form.Item>
        <Alert
          style={{ marginBottom: 12 }}
          type="warning"
          showIcon
          title={t('adminShell.operationsCenter.reprintRoutingSimulatedHint')}
        />
        <Space>
          <Button onClick={() => setStep(0)}>{t('adminShell.operationsCenter.reprintBack')}</Button>
          <Button type="primary" onClick={() => setStep(2)}>
            {t('adminShell.operationsCenter.reprintNext')}
          </Button>
        </Space>
      </Form>

      {step === 0 ? (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          {!paymentId?.trim() ? (
            <>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t('adminShell.operationsCenter.reprintManualPaymentIntro')}
              </Typography.Paragraph>
              <Input
                placeholder={t('adminShell.operationsCenter.paymentIdLabel')}
                value={effectivePaymentId}
                onChange={(e) => setEffectivePaymentId(e.target.value)}
              />
            </>
          ) : null}
          {hasPaymentId ? (
            <>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t('adminShell.operationsCenter.reprintPreviewIntro')}
              </Typography.Paragraph>
              {previewQuery.isLoading ? (
                <Typography.Text type="secondary">
                  {t('adminShell.operationsCenter.reprintPreviewLoading')}
                </Typography.Text>
              ) : previewQuery.isError ? (
                <Alert type="error" showIcon title={String(previewQuery.error)} />
              ) : receipt ? (
                <>
                  <Descriptions bordered size="small" column={1}>
                    <Descriptions.Item label={t('adminShell.operationsCenter.receiptNumber')}>
                      {receipt.receiptNumber ?? receiptNumberHint ?? '—'}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('adminShell.operationsCenter.issuedAt')}>
                      {receipt.date ?? '—'}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('adminShell.operationsCenter.grandTotalLabel')}>
                      {formatEUR(receipt.grandTotal ?? 0)}
                    </Descriptions.Item>
                  </Descriptions>
                  <Alert
                    type="info"
                    showIcon
                    title={t('adminShell.operationsCenter.reprintPreviewNoAudit')}
                  />
                </>
              ) : (
                <Alert
                  type="warning"
                  showIcon
                  title={t('adminShell.operationsCenter.reprintPreviewEmpty')}
                />
              )}
            </>
          ) : null}
          <Space>
            <Button onClick={onClose}>{t('adminShell.operationsCenter.reprintCancel')}</Button>
            <Button
              type="primary"
              onClick={() => setStep(1)}
              disabled={!hasPaymentId || !receipt || previewQuery.isError || previewQuery.isLoading}
            >
              {t('adminShell.operationsCenter.reprintNext')}
            </Button>
          </Space>
        </Space>
      ) : null}

      {step === 2 ? (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Typography.Paragraph>
            {t('adminShell.operationsCenter.reprintConfirmIntro')}
          </Typography.Paragraph>
          <Descriptions bordered size="small" column={1}>
            <Descriptions.Item label={t('adminShell.operationsCenter.receiptNumber')}>
              {receipt?.receiptNumber ?? receiptNumberHint ?? '—'}
            </Descriptions.Item>
            <Descriptions.Item label={t('adminShell.operationsCenter.reprintReasonLabel')}>
              {form.getFieldValue('reprintReasonCode')}
            </Descriptions.Item>
            <Descriptions.Item label={t('adminShell.operationsCenter.deviceLabel')}>
              {form.getFieldValue('deviceId') ?? '—'}
            </Descriptions.Item>
          </Descriptions>
          <Space wrap>
            <Button onClick={() => setStep(1)}>
              {t('adminShell.operationsCenter.reprintBack')}
            </Button>
            <Button type="primary" loading={submitting} onClick={onConfirm}>
              {t('adminShell.operationsCenter.reprintSubmit')}
            </Button>
          </Space>
        </Space>
      ) : null}

      {step === 3 && result ? (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          {result.outcome === 'Success' ? (
            <Alert
              type="success"
              showIcon
              title={t('adminShell.operationsCenter.reprintSuccessTitle')}
              description={
                <Space orientation="vertical" size="small">
                  <span>
                    {t('adminShell.operationsCenter.reprintAuditId')}:{' '}
                    <Typography.Text code>{result.auditLogId ?? '—'}</Typography.Text>
                  </span>
                  {result.reportableEventType ? (
                    <span>
                      {t('adminShell.operationsCenter.reprintReportableEvent')}:{' '}
                      <Typography.Text code>{result.reportableEventType}</Typography.Text>
                    </span>
                  ) : null}
                  {result.routing?.isSimulated ? (
                    <span>{t('adminShell.operationsCenter.reprintOutcomeSimulated')}</span>
                  ) : null}
                </Space>
              }
            />
          ) : (
            <Alert
              type="error"
              showIcon
              title={t('adminShell.operationsCenter.reprintFailedTitle')}
              description={
                <Space orientation="vertical" size="small">
                  <span>
                    {result.errorCode} — {result.errorMessage ?? submitError}
                  </span>
                  {result.auditLogId ? (
                    <span>
                      {t('adminShell.operationsCenter.reprintAuditId')}:{' '}
                      <Typography.Text code>{result.auditLogId}</Typography.Text>
                    </span>
                  ) : null}
                </Space>
              }
            />
          )}
          {result.receipt?.receiptId ? (
            <Link href={`/receipts/${result.receipt.receiptId}`}>
              {t('adminShell.operationsCenter.openReceipt')}
            </Link>
          ) : null}
          <Space wrap>
            <Link href="/audit-logs">{t('adminShell.operationsCenter.openAuditLogs')}</Link>
            <Link href="/rksv/incident">{t('adminShell.operationsCenter.openIncident')}</Link>
          </Space>
          <Button type="primary" onClick={onClose}>
            {t('adminShell.operationsCenter.reprintClose')}
          </Button>
        </Space>
      ) : null}
    </Modal>
  );
}
