'use client';

import { DownloadOutlined, InboxOutlined, UploadOutlined } from '@ant-design/icons';
import { Alert, Button, Space, Table, Tag, Typography, Upload } from 'antd';
import type { UploadFile } from 'antd/es/upload';
import { useCallback, useEffect, useState } from 'react';

import type { DemoProductImportResult } from '@/api/admin/products';
import {
  type DemoTemplatePreviewRow,
  type DemoTemplateValidationResult,
  downloadDemoTemplate,
  importDemoTemplateFile,
  isDemoTemplateFile,
  previewDemoTemplateFile,
} from '@/features/tenants/api/demoTemplate';
import { DemoImportPriceAdjustmentSection } from '@/features/tenants/components/demo-import/DemoImportPriceAdjustment';
import type { DemoImportImageMode } from '@/features/tenants/components/demo-import/demoImportImage';
import { toImageModeRequest } from '@/features/tenants/components/demo-import/demoImportImage';
import {
  DEFAULT_PRICE_ADJUSTMENT,
  type DemoImportPriceAdjustmentState,
  toPriceAdjustmentRequest,
} from '@/features/tenants/components/demo-import/priceAdjustment';
import { useAntdApp } from '@/hooks/useAntdApp';

const { Text, Paragraph } = Typography;
const { Dragger } = Upload;

const ACCEPT = '.csv,.xlsx,.xls';

export type DemoTemplateImportTabProps = {
  tenantId?: string;
  overwrite: boolean;
  priceAdjustment: DemoImportPriceAdjustmentState;
  onPriceAdjustmentChange: (value: DemoImportPriceAdjustmentState) => void;
  imageMode: DemoImportImageMode;
  onImportSuccess: (result: DemoProductImportResult) => void;
};

type Step = 'upload' | 'preview';

export function DemoTemplateImportTab({
  tenantId,
  overwrite,
  priceAdjustment,
  onPriceAdjustmentChange,
  imageMode,
  onImportSuccess,
}: DemoTemplateImportTabProps) {
  const { message } = useAntdApp();

  const [step, setStep] = useState<Step>('upload');
  const [fileList, setFileList] = useState<UploadFile[]>([]);
  const [validation, setValidation] = useState<DemoTemplateValidationResult | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [importLoading, setImportLoading] = useState(false);
  const [downloadLoading, setDownloadLoading] = useState(false);

  const selectedFile = fileList[0]?.originFileObj;

  const resetUpload = useCallback(() => {
    setStep('upload');
    setFileList([]);
    setValidation(null);
  }, []);

  useEffect(() => {
    resetUpload();
  }, [resetUpload]);

  const handleDownloadTemplate = async () => {
    setDownloadLoading(true);
    try {
      await downloadDemoTemplate();
      message.success('Vorlage heruntergeladen');
    } catch {
      message.error('Vorlage konnte nicht heruntergeladen werden');
    } finally {
      setDownloadLoading(false);
    }
  };

  const handleValidate = async (file: File) => {
    if (!isDemoTemplateFile(file)) {
      message.warning('Nur CSV oder Excel (.xlsx) werden unterstützt.');
      return;
    }

    setPreviewLoading(true);
    try {
      const result = await previewDemoTemplateFile(file, 25);
      setValidation(result);
      setStep('preview');
      if (result.parseError) {
        message.error(result.parseError);
      } else if (!result.isValid) {
        message.warning('Datei enthält Fehler — bitte korrigieren vor dem Import.');
      }
    } catch {
      message.error('Validierung fehlgeschlagen');
      setValidation(null);
    } finally {
      setPreviewLoading(false);
    }
  };

  const handleImport = async () => {
    if (!selectedFile || !validation?.isValid) return;

    setImportLoading(true);
    try {
      const result = await importDemoTemplateFile(
        selectedFile,
        {
          overwriteExisting: overwrite,
          ...toPriceAdjustmentRequest(priceAdjustment),
          ...toImageModeRequest(imageMode),
        },
        tenantId
      );
      if (!result.success) {
        message.error(result.errorMessage ?? 'Import fehlgeschlagen');
        return;
      }
      onImportSuccess(result);
    } catch {
      message.error('Import fehlgeschlagen');
    } finally {
      setImportLoading(false);
    }
  };

  const previewColumns = [
    { title: 'Zeile', dataIndex: 'row', key: 'row', width: 56 },
    {
      title: 'Typ',
      dataIndex: 'rowType',
      key: 'rowType',
      width: 88,
      render: (v: string) => (
        <Tag color={v?.toLowerCase() === 'category' ? 'purple' : 'blue'}>{v}</Tag>
      ),
    },
    { title: 'Name', dataIndex: 'name', key: 'name', ellipsis: true },
    { title: 'Kategorie', dataIndex: 'category', key: 'category', ellipsis: true },
    {
      title: 'Preis',
      dataIndex: 'price',
      key: 'price',
      width: 72,
      align: 'right' as const,
      render: (v: number | null | undefined) => (v != null ? `€${Number(v).toFixed(2)}` : '—'),
    },
    {
      title: 'MwSt %',
      dataIndex: 'taxRate',
      key: 'taxRate',
      width: 72,
      align: 'right' as const,
      render: (v: number | null | undefined) => (v != null ? `${v}%` : '—'),
    },
  ];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Alert
        type="info"
        showIcon
        title="Eigene Demo-Vorlage"
        description={
          <ol style={{ margin: '8px 0 0', paddingLeft: 20 }}>
            <li>Demo-Vorlage herunterladen (CSV, in Excel bearbeitbar)</li>
            <li>Preise, Beschreibungen und Kategorien anpassen</li>
            <li>Datei hochladen und Vorschau prüfen</li>
            <li>Mit Preis-Anpassung importieren</li>
          </ol>
        }
      />

      <Button
        icon={<DownloadOutlined />}
        onClick={handleDownloadTemplate}
        loading={downloadLoading}
      >
        Demo Template herunterladen
      </Button>

      {step === 'upload' ? (
        <Dragger
          accept={ACCEPT}
          multiple={false}
          fileList={fileList}
          beforeUpload={(file) => {
            setFileList([{ uid: file.uid, name: file.name, originFileObj: file } as UploadFile]);
            void handleValidate(file);
            return false;
          }}
          onRemove={() => {
            resetUpload();
            return true;
          }}
          disabled={previewLoading}
        >
          <p className="ant-upload-drag-icon">
            <InboxOutlined />
          </p>
          <p className="ant-upload-text">CSV oder Excel hier ablegen</p>
          <p className="ant-upload-hint">Nach dem Upload wird die Datei automatisch validiert</p>
        </Dragger>
      ) : null}

      {previewLoading ? <Text type="secondary">Validierung läuft…</Text> : null}

      {step === 'preview' && validation ? (
        <>
          {validation.parseError ? (
            <Alert type="error" showIcon title={validation.parseError} />
          ) : (
            <>
              <Space wrap>
                <Tag color="blue">{validation.categoryCount} Kategorien</Tag>
                <Tag color="green">{validation.productCount} Produkte</Tag>
                <Tag>{validation.totalRows} Zeilen</Tag>
                {validation.isValid ? (
                  <Tag color="success">Bereit zum Import</Tag>
                ) : (
                  <Tag color="error">Fehler beheben</Tag>
                )}
              </Space>

              {validation.issues.length > 0 ? (
                <Alert
                  type={validation.isValid ? 'warning' : 'error'}
                  showIcon
                  title={validation.isValid ? 'Hinweise zur Datei' : 'Validierungsfehler'}
                  description={
                    <ul style={{ margin: '8px 0 0', paddingLeft: 20 }}>
                      {validation.issues.slice(0, 15).map((issue, idx) => (
                        <li key={`${issue.row}-${idx}`}>
                          {issue.row != null ? `Zeile ${issue.row}: ` : ''}
                          {issue.message}
                        </li>
                      ))}
                    </ul>
                  }
                />
              ) : null}

              <Paragraph type="secondary" style={{ marginBottom: 0 }}>
                Vorschau: {validation.productCount} Produkte werden importiert
                {validation.isValid ? '' : ' (nach Fehlerbehebung)'}
              </Paragraph>

              <Table<DemoTemplatePreviewRow>
                size="small"
                rowKey={(r) => `${r.row}-${r.name}`}
                dataSource={validation.previewRows}
                columns={previewColumns}
                pagination={false}
                scroll={{ y: 220 }}
              />

              <DemoImportPriceAdjustmentSection
                value={priceAdjustment}
                onChange={onPriceAdjustmentChange}
                selectedProductCount={validation.productCount}
              />

              <Space>
                <Button onClick={resetUpload}>Andere Datei</Button>
                <Button
                  type="primary"
                  icon={<UploadOutlined />}
                  loading={importLoading}
                  disabled={!validation.isValid || !selectedFile}
                  onClick={handleImport}
                >
                  {validation.productCount} Produkte importieren
                </Button>
              </Space>
            </>
          )}
        </>
      ) : null}
    </Space>
  );
}
