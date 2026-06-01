'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Modal, Alert, Progress, Radio, Space, Typography } from 'antd';

import type { AuditLogListParams } from '@/features/audit-logs/hooks/useAuditLogSearchParams';
import {
    downloadAuditExportJob,
    pollAuditExportJob,
    startAuditExport,
    type AuditExportFormat,
} from '@/features/audit/api/auditAdmin';
import { useI18n } from '@/i18n';

type Props = {
    open: boolean;
    params: AuditLogListParams;
    onClose: () => void;
};

function triggerBlobDownload(blob: Blob, fileName: string) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();
    URL.revokeObjectURL(url);
}

export function AuditExportModal({ open, params, onClose }: Props) {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const [format, setFormat] = useState<AuditExportFormat>('csv');
    const [busy, setBusy] = useState(false);
    const [jobId, setJobId] = useState<string | null>(null);
    const [progress, setProgress] = useState(0);
    const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

    const clearPoll = useCallback(() => {
        if (pollRef.current) {
            clearInterval(pollRef.current);
            pollRef.current = null;
        }
    }, []);

    useEffect(() => () => clearPoll(), [clearPoll]);

    const handleExport = async () => {
        setBusy(true);
        setJobId(null);
        setProgress(0);
        try {
            const result = await startAuditExport(format, params);
            if (result.kind === 'immediate') {
                triggerBlobDownload(result.blob, result.fileName);
                message.success(t('common.auditLogs.exportStarted'));
                onClose();
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
                        await downloadAuditExportJob(
                            result.jobId,
                            status.downloadFileName ?? `audit_export.${format === 'json' ? 'json' : 'csv'}`,
                        );
                        message.success(t('common.auditLogs.exportStarted'));
                        onClose();
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
            message.error(e instanceof Error ? e.message : t('common.auditLogs.exportFailed'));
            setBusy(false);
        }
    };

    return (
        <Modal
            open={open}
            title={t('common.auditLogs.exportModalTitle')}
            onCancel={() => {
                clearPoll();
                onClose();
            }}
            onOk={handleExport}
            okText={t('common.auditLogs.exportModalStart')}
            confirmLoading={busy}
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
                        <Progress percent={progress} status={progress >= 100 ? 'success' : 'active'} />
                    </>
                ) : null}
            </Space>
        </Modal>
    );
}
