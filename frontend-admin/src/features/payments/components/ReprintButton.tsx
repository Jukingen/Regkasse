'use client';

import { PrinterOutlined } from '@ant-design/icons';
import type { ButtonProps } from 'antd';
import { Button } from 'antd';
import { useCallback, useState } from 'react';

import { reprintReceipt } from '@/api/admin/payments';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

const REPRINT_MESSAGE_KEY = 'admin-payment-reprint-pdf';

export interface ReprintButtonProps {
  paymentId: string | null | undefined;
  receiptNumber?: string | null;
  disabled?: boolean;
  /** Compact control for tables and dense toolbars (default: middle). */
  size?: ButtonProps['size'];
}

export function ReprintButton({
  paymentId,
  receiptNumber,
  disabled,
  size = 'middle',
}: ReprintButtonProps) {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const canReprintPdf = hasPermission(PERMISSIONS.RECEIPT_REPRINT);
  const [loading, setLoading] = useState(false);

  const handleReprint = useCallback(async () => {
    const id = paymentId?.trim();
    if (!id) return;

    setLoading(true);
    message.loading({ content: t('payments.detail.reprintPdfLoading'), key: REPRINT_MESSAGE_KEY });
    try {
      const blob = await reprintReceipt(id);
      const pdfBlob =
        blob instanceof Blob ? blob : new Blob([blob as BlobPart], { type: 'application/pdf' });
      const rn = receiptNumber?.trim();
      const safeBase = rn ? rn.replace(/[^\w.-]+/g, '_') : id;
      const url = window.URL.createObjectURL(pdfBlob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `beleg_${safeBase}.pdf`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      message.success({
        content: t('payments.detail.reprintPdfSuccess'),
        key: REPRINT_MESSAGE_KEY,
      });
    } catch (error) {
      message.destroy(REPRINT_MESSAGE_KEY);
      openApiErrorMessage(message.open, t, error, {
        logContext: 'ReprintButton.reprintReceipt',
        fallbackKey: 'payments.detail.reprintPdfError',
      });
    } finally {
      setLoading(false);
    }
  }, [message, paymentId, receiptNumber, t]);

  if (!canReprintPdf) {
    return null;
  }

  const id = paymentId?.trim();
  const isDisabled = disabled || !id || loading;

  return (
    <Button
      size={size}
      icon={<PrinterOutlined />}
      loading={loading}
      disabled={isDisabled}
      onClick={() => void handleReprint()}
    >
      {t('payments.detail.buttonReprintPdf')}
    </Button>
  );
}
