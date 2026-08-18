'use client';

import { InboxOutlined } from '@ant-design/icons';
import { Button, Card, Progress, Typography, Upload } from 'antd';
import { useState } from 'react';

import { activateUnifiedLicense } from '@/features/license/api/activateUnifiedLicense';
import { parseLicenseKeysFromText } from '@/features/license/utils/parseLicenseKeysFromText';
import { runBulkSequential } from '@/features/billing/utils/billingSalesBulk';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type Props = {
  tenantId?: string | null;
  onActivated?: () => void;
};

export function LicenseBulkImportCard({ tenantId, onActivated }: Props) {
  const { t } = useI18n();
  const notify = useNotify();
  const [keys, setKeys] = useState<string[]>([]);
  const [running, setRunning] = useState(false);
  const [progress, setProgress] = useState<{ current: number; total: number } | null>(null);

  const handleFile = async (file: File) => {
    const text = await file.text();
    const parsed = parseLicenseKeysFromText(text);
    setKeys(parsed);
    if (parsed.length === 0) {
      notify.warning(t('license.management.bulkImportEmpty'));
    }
    return false;
  };

  const runImport = async () => {
    if (keys.length === 0) {
      notify.warning(t('license.management.bulkImportEmpty'));
      return;
    }
    setRunning(true);
    const result = await runBulkSequential(
      keys,
      (key) => ({ id: key, label: key }),
      async (key) => {
        await activateUnifiedLicense(key, tenantId);
      },
      (p) => setProgress({ current: p.current, total: p.total })
    );
    setRunning(false);
    setProgress(null);
    if (result.failed === 0) {
      notify.success(t('billing.licenseSales.bulk.result.success', { count: result.success }));
    } else {
      notify.warning(
        t('billing.licenseSales.bulk.result.partial', {
          success: result.success,
          failed: result.failed,
        })
      );
    }
    onActivated?.();
  };

  return (
    <Card title={t('license.management.bulkImportTitle')} style={{ marginBottom: 16 }}>
      <Typography.Paragraph type="secondary">
        {t('license.management.bulkImportHint')}
      </Typography.Paragraph>
      <Upload.Dragger
        accept=".csv,text/csv,text/plain"
        maxCount={1}
        beforeUpload={(file) => {
          void handleFile(file);
          return false;
        }}
        showUploadList={false}
      >
        <p className="ant-upload-drag-icon">
          <InboxOutlined />
        </p>
        <p className="ant-upload-text">{t('license.management.bulkImportTitle')}</p>
      </Upload.Dragger>
      {keys.length > 0 ? (
        <Typography.Paragraph style={{ marginTop: 12 }}>
          {t('license.management.bulkImportProgress', {
            current: keys.length,
            total: keys.length,
          })}
        </Typography.Paragraph>
      ) : null}
      {progress ? (
        <Progress
          percent={Math.round((progress.current / Math.max(1, progress.total)) * 100)}
          status={running ? 'active' : 'normal'}
        />
      ) : null}
      <Button
        type="primary"
        style={{ marginTop: 12 }}
        loading={running}
        onClick={() => void runImport()}
      >
        {t('license.management.bulkImportButton')}
      </Button>
    </Card>
  );
}
