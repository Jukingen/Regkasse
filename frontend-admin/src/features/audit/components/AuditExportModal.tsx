'use client';

import { Alert, Modal, Progress, Radio, Space, Typography } from 'antd';
import { useCallback, useEffect, useRef, useState } from 'react';

import { DownloadPreviewModal } from '@/components/ui/DownloadPreviewModal';
import type { AuditLogListParams } from '@/features/audit-logs/hooks/useAuditLogSearchParams';
import {
  type AuditExportFormat,
  downloadAuditExportJob,
  pollAuditExportJob,
  startAuditExport,
} from '@/features/audit/api/auditAdmin';
import { buildAuditExportFileName } from '@/features/audit/utils/auditExportFileName';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { getEffectiveTenantSlug } from '@/features/auth/services/devTenant';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useDownloadPreview } from '@/hooks/useDownloadPreview';
import { useSensitiveExportGate } from '@/hooks/useSensitiveExportGate';
import { useI18n } from '@/i18n';
import { SENSITIVE_EXPORT_KINDS } from '@/lib/download/sensitiveExportSecurity';
import { triggerBlobDownload } from '@/lib/download/exportDownload';

type Props = {
  open: boolean;
  params: AuditLogListParams;
  onClose: () => void;
};

export function AuditExportModal({ open, params, onClose }: Props) {
  const { message } = useAntdApp();
  const downloadPreview = useDownloadPreview();
  const sensitiveGate = useSensitiveExportGate();
  const { user } = useAuth();

  const { t } = useI18n();
  const [format, setFormat] = useState<AuditExportFormat>('csv');
  const [busy, setBusy] = useState(false);
  const [jobId, setJobId] = useState<string | null>(null);
  const [progress, setProgress] = useState(0);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const securityHeadersRef = useRef<Record<string, string>>({});

  const clearPoll = useCallback(() => {
    if (pollRef.current) {
      clearInterval(pollRef.current);
      pollRef.current = null;
    }
  }, []);

  useEffect(() => () => clearPoll(), [clearPoll]);

  const openBlobPreview = (blob: Blob, fileName: string, fileType: string) => {
    downloadPreview.requestPreview({
      fileName,
      fileType,
      sizeBytes: blob.size,
      contentSummary: t('common.exportDownload.contentGeneric'),
      execute: async () => {
        triggerBlobDownload(blob, fileName);
        message.success(t('common.auditLogs.exportStarted'));
        onClose();
      },
    });
  };

  const runExportWithHeaders = useCallback(
    async (headers: Record<string, string>) => {
      securityHeadersRef.current = headers;
      setBusy(true);
      setJobId(null);
      setProgress(0);
      try {
        const result = await startAuditExport(format, params, headers);
        if (result.kind === 'immediate') {
          setBusy(false);
          openBlobPreview(result.blob, result.fileName, format.toUpperCase());
          return;
        }

        setJobId(result.jobId);
        setProgress(10);
        pollRef.current = setInterval(async () => {
          try {
            const status = await pollAuditExportJob(result.jobId);
            const code = typeof status.status === 'number' ? status.status : status.status;
            if (code === 'Completed' || code === 2) {
              clearPoll();
              setProgress(100);
              const fileName =
                status.downloadFileName ??
                buildAuditExportFileName({
                  tenantSlug: getEffectiveTenantSlug(),
                  fromDate: params.startDate,
                  toDate: params.endDate,
                  format,
                });
              downloadPreview.requestPreview({
                fileName,
                fileType: format.toUpperCase(),
                sizeBytes: 0,
                isSizeEstimate: true,
                contentSummary: t('common.exportDownload.contentGeneric'),
                hint: t('common.exportDownload.mayTakeAMoment'),
                execute: async () => {
                  await downloadAuditExportJob(
                    result.jobId,
                    fileName,
                    securityHeadersRef.current
                  );
                  message.success(t('common.auditLogs.exportStarted'));
                  onClose();
                },
              });
              setBusy(false);
            } else if (code === 'Failed' || code === 3) {
              clearPoll();
              message.error(status.message ?? t('common.auditLogs.exportFailed'));
              setBusy(false);
            } else {
              setProgress((p) => Math.min(p + 15, 90));
            }
          } catch {
            clearPoll();
            message.error(t('common.auditLogs.exportFailed'));
            setBusy(false);
          }
        }, 2000);
      } catch (e) {
        setBusy(false);
        throw e;
      }
    },
    [clearPoll, downloadPreview, format, message, onClose, params, t]
  );

  const handleExport = () => {
    sensitiveGate.run({
      kind: SENSITIVE_EXPORT_KINDS.AuditLogExport,
      isSuperAdmin: isSuperAdmin(user?.role),
      execute: async (headers) => {
        await runExportWithHeaders(headers);
      },
    });
  };

  return (
    <>
      <Modal
        open={open}
        title={t('common.auditLogs.exportModalTitle')}
        onCancel={() => {
          clearPoll();
          onClose();
        }}
        onOk={() => handleExport()}
        okText={t('common.auditLogs.exportModalStart')}
        confirmLoading={busy || sensitiveGate.busy}
        destroyOnHidden
      >
        <Space orientation="vertical" style={{ width: '100%' }} size="middle">
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {t('common.auditLogs.exportModalHint')}
          </Typography.Paragraph>
          <Radio.Group value={format} onChange={(e) => setFormat(e.target.value)}>
            <Radio value="csv">CSV</Radio>
            <Radio value="json">JSON</Radio>
            <Radio value="excel">{t('common.auditLogs.exportExcel')}</Radio>
          </Radio.Group>
          {jobId ? (
            <>
              <Alert type="info" title={t('common.auditLogs.exportBackground')} showIcon />
              <Alert type="info" showIcon title={t('common.exportDownload.mayTakeAMoment')} />
              <Progress percent={progress} status={progress >= 100 ? 'success' : 'active'} />
            </>
          ) : null}
        </Space>
      </Modal>
      <DownloadPreviewModal {...downloadPreview.modalProps} />
      {sensitiveGate.modals}
    </>
  );
}
