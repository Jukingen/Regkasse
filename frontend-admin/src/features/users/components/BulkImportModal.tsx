'use client';

import { DownloadOutlined, InboxOutlined, StopOutlined, UploadOutlined } from '@ant-design/icons';
import { Alert, Button, Modal, Progress, Space, Typography, Upload } from 'antd';
import type { UploadFile } from 'antd/es/upload';
import React, { useCallback, useEffect, useRef, useState } from 'react';

import {
  type BulkImportJobStatusResponse,
  type BulkImportPreviewRow,
  downloadBulkImportTemplate,
  pollBulkImportJobUntilDone,
  previewBulkImportFile,
  startBulkImportJob,
} from '@/features/users/api/bulkImport';
import { BulkImportResultsModal } from '@/features/users/components/BulkImportResultsModal';
import { ImportPreviewTable } from '@/features/users/components/ImportPreviewTable';
import {
  isCsvFile,
  isExcelFile,
  parseCsvBulkImportPreview,
} from '@/features/users/lib/parseBulkImportPreview';
import { useAntdApp } from '@/hooks/useAntdApp';

type Props = {
  open: boolean;
  onClose: () => void;
  onSuccess?: () => void;
};

type Step = 'upload' | 'preview' | 'importing' | 'done';

const ACCEPT = '.csv,.xlsx,.xls';
const PREVIEW_ROW_COUNT = 10;

/**
 * Bulk user import: upload, preview, background job with progress, results modal.
 */
export function BulkImportModal({ open, onClose, onSuccess }: Props) {
  const { message } = useAntdApp();

  const [step, setStep] = useState<Step>('upload');
  const [fileList, setFileList] = useState<UploadFile[]>([]);
  const [previewRows, setPreviewRows] = useState<BulkImportPreviewRow[]>([]);
  const [totalRows, setTotalRows] = useState(0);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [jobStatus, setJobStatus] = useState<BulkImportJobStatusResponse | null>(null);
  const [resultsOpen, setResultsOpen] = useState(false);
  const abortRef = useRef<AbortController | null>(null);

  const resetState = useCallback(() => {
    setStep('upload');
    setFileList([]);
    setPreviewRows([]);
    setTotalRows(0);
    setPreviewError(null);
    setJobStatus(null);
    setResultsOpen(false);
    abortRef.current?.abort();
    abortRef.current = null;
  }, []);

  useEffect(() => {
    if (!open) resetState();
  }, [open, resetState]);

  const selectedFile = fileList[0]?.originFileObj;

  const loadPreview = async (file: File) => {
    setPreviewLoading(true);
    setPreviewError(null);
    try {
      if (isCsvFile(file)) {
        const text = await file.text();
        const parsed = parseCsvBulkImportPreview(text, PREVIEW_ROW_COUNT);
        if (parsed.parseError) {
          setPreviewError(parsed.parseError);
          setPreviewRows([]);
          setTotalRows(0);
        } else {
          setPreviewRows(parsed.previewRows);
          setTotalRows(parsed.totalRows);
        }
      } else if (isExcelFile(file)) {
        const server = await previewBulkImportFile(file, PREVIEW_ROW_COUNT);
        if (server.parseError) {
          setPreviewError(server.parseError);
          setPreviewRows([]);
          setTotalRows(0);
        } else {
          setPreviewRows(server.previewRows);
          setTotalRows(server.totalRows);
        }
      } else {
        setPreviewError('Nur CSV oder Excel (.xlsx) werden unterstützt.');
      }
      setStep('preview');
    } catch {
      message.error('Vorschau konnte nicht geladen werden');
    } finally {
      setPreviewLoading(false);
    }
  };

  const handleClose = () => {
    if (step === 'importing') {
      abortRef.current?.abort();
    }
    resetState();
    onClose();
  };

  const handleStartImport = async () => {
    if (!selectedFile) return;

    const controller = new AbortController();
    abortRef.current = controller;
    setStep('importing');
    setJobStatus(null);

    try {
      const started = await startBulkImportJob(selectedFile);
      const finalStatus = await pollBulkImportJobUntilDone(started.jobId, {
        signal: controller.signal,
        onProgress: setJobStatus,
      });

      setJobStatus(finalStatus);
      setStep('done');
      setResultsOpen(true);

      if (finalStatus.successCount > 0) {
        message.success(`${finalStatus.successCount} Benutzer importiert`);
        onSuccess?.();
      }
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        message.info('Import abgebrochen');
        setStep('preview');
      } else {
        message.error('Import fehlgeschlagen');
        setStep('preview');
      }
    } finally {
      abortRef.current = null;
    }
  };

  const progressPercent =
    jobStatus && jobStatus.totalRows > 0
      ? Math.round((jobStatus.processedRows / jobStatus.totalRows) * 100)
      : 0;

  const footer =
    step === 'upload'
      ? [
          <Button
            key="template"
            icon={<DownloadOutlined />}
            onClick={() => void downloadBulkImportTemplate()}
          >
            Vorlage (CSV)
          </Button>,
          <Button key="cancel" onClick={handleClose}>
            Schließen
          </Button>,
        ]
      : step === 'preview'
        ? [
            <Button key="back" onClick={() => setStep('upload')}>
              Zurück
            </Button>,
            <Button key="cancel" onClick={handleClose}>
              Schließen
            </Button>,
            <Button
              key="import"
              type="primary"
              icon={<UploadOutlined />}
              disabled={!selectedFile || !!previewError || totalRows === 0}
              onClick={() => void handleStartImport()}
            >
              {totalRows} Benutzer importieren
            </Button>,
          ]
        : step === 'importing'
          ? [
              <Button
                key="abort"
                danger
                icon={<StopOutlined />}
                onClick={() => abortRef.current?.abort()}
              >
                Abbrechen
              </Button>,
            ]
          : [
              <Button key="close" type="primary" onClick={handleClose}>
                Schließen
              </Button>,
            ];

  return (
    <>
      <Modal
        title="Benutzer importieren"
        open={open}
        onCancel={handleClose}
        width={760}
        footer={footer}
        closable={step !== 'importing'}
        mask={{ closable: step !== 'importing' }}
      >
        <Typography.Paragraph type="secondary">
          Pflichtspalten: <code>email</code>, <code>role</code>, <code>tenantSlug</code>. Rollen:
          Manager, Cashier, Accountant. Große Dateien werden im Hintergrund verarbeitet (1000+
          Zeilen).
        </Typography.Paragraph>

        {step === 'upload' ? (
          <Upload.Dragger
            accept={ACCEPT}
            maxCount={1}
            fileList={fileList}
            beforeUpload={(file) => {
              setFileList([{ uid: file.uid, name: file.name, originFileObj: file }]);
              void loadPreview(file);
              return false;
            }}
            onRemove={() => {
              setFileList([]);
              setStep('upload');
            }}
          >
            <p className="ant-upload-drag-icon">
              <InboxOutlined />
            </p>
            <p className="ant-upload-text">Datei hier ablegen oder klicken</p>
            <p className="ant-upload-hint">CSV oder Excel (.xlsx), max. 20 MB</p>
          </Upload.Dragger>
        ) : null}

        {step === 'preview' ? (
          <Space orientation="vertical" style={{ width: '100%' }} size="middle">
            {previewError ? (
              <Alert type="error" showIcon title={previewError} />
            ) : (
              <ImportPreviewTable
                rows={previewRows}
                totalRows={totalRows}
                loading={previewLoading}
              />
            )}
          </Space>
        ) : null}

        {step === 'importing' ? (
          <Space orientation="vertical" style={{ width: '100%' }} size="large">
            <Typography.Text>Import läuft…</Typography.Text>
            <Progress percent={progressPercent} status="active" />
            <Typography.Text type="secondary">
              {jobStatus?.processedRows ?? 0} / {jobStatus?.totalRows ?? totalRows} verarbeitet —{' '}
              {jobStatus?.successCount ?? 0} erfolgreich, {jobStatus?.failedCount ?? 0}{' '}
              fehlgeschlagen
            </Typography.Text>
          </Space>
        ) : null}

        {step === 'done' && !resultsOpen ? (
          <Alert type="success" showIcon title="Import abgeschlossen" />
        ) : null}
      </Modal>

      <BulkImportResultsModal
        open={resultsOpen}
        status={jobStatus}
        onClose={() => {
          setResultsOpen(false);
          handleClose();
        }}
      />
    </>
  );
}
