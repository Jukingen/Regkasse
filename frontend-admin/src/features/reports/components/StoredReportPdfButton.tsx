'use client';

import { FilePdfOutlined } from '@ant-design/icons';
import type { ButtonProps } from 'antd';
import { Button } from 'antd';
import { useCallback, useState } from 'react';

import {
  type ReportPdfType,
  downloadReportPdf,
  triggerReportPdfBlobDownload,
} from '@/features/reports/api/reportPdfApi';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

const MESSAGE_KEY = 'stored-report-pdf-download';

export interface StoredReportPdfButtonProps {
  reportType: ReportPdfType | string;
  targetId: string | null | undefined;
  fileNameBase?: string | null;
  disabled?: boolean;
  size?: ButtonProps['size'];
  language?: string;
}

export function StoredReportPdfButton({
  reportType,
  targetId,
  fileNameBase,
  disabled,
  size = 'middle',
  language,
}: StoredReportPdfButtonProps) {
  const { message } = useAntdApp();
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const canDownload =
    hasPermission(PERMISSIONS.REPORT_VIEW) || hasPermission(PERMISSIONS.RECEIPT_REPRINT);
  const [loading, setLoading] = useState(false);

  const handleDownload = useCallback(async () => {
    const id = targetId?.trim();
    if (!id) return;

    setLoading(true);
    message.loading({ content: t('reporting.storedPdf.loading'), key: MESSAGE_KEY });
    try {
      const blob = await downloadReportPdf(reportType, id, { language });
      const safeBase = (fileNameBase?.trim() || `${reportType}_${id}`).replace(/[^\w.-]+/g, '_');
      triggerReportPdfBlobDownload(blob, safeBase);
      message.success({ content: t('reporting.storedPdf.success'), key: MESSAGE_KEY });
    } catch (error) {
      message.destroy(MESSAGE_KEY);
      openApiErrorMessage(message.open, t, error, {
        logContext: 'StoredReportPdfButton.download',
        fallbackKey: 'reporting.storedPdf.error',
      });
    } finally {
      setLoading(false);
    }
  }, [fileNameBase, language, message, reportType, t, targetId]);

  if (!canDownload) {
    return null;
  }

  const id = targetId?.trim();
  const isDisabled = disabled || !id || loading;

  return (
    <Button
      size={size}
      icon={<FilePdfOutlined />}
      loading={loading}
      disabled={isDisabled}
      onClick={() => void handleDownload()}
    >
      {t('reporting.storedPdf.button')}
    </Button>
  );
}
