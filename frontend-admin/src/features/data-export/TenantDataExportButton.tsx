'use client';

import { DownloadOutlined } from '@ant-design/icons';
import { Button, Dropdown } from 'antd';
import { useState } from 'react';

import {
  downloadTenantDataExport,
  type TenantExportFormat,
  type TenantExportType,
} from '@/features/data-export/downloadTenantDataExport';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

/** CSV / Excel menu for GET /api/admin/export/{type}. */
export function TenantDataExportButton({ type }: { type: TenantExportType }) {
  const { t } = useI18n();
  const notify = useNotify();
  const [busy, setBusy] = useState(false);

  const run = async (format: TenantExportFormat) => {
    setBusy(true);
    try {
      await downloadTenantDataExport(type, format);
    } catch {
      notify.error(t('common.dataExport.failed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dropdown
      trigger={['click']}
      menu={{
        items: [
          { key: 'csv', label: t('common.dataExport.csv'), onClick: () => void run('csv') },
          { key: 'xlsx', label: t('common.dataExport.xlsx'), onClick: () => void run('xlsx') },
        ],
      }}
    >
      <Button icon={<DownloadOutlined />} loading={busy} data-testid={`tenant-export-${type}`}>
        {t('common.dataExport.button')}
      </Button>
    </Dropdown>
  );
}
