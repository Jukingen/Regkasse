'use client';

import { HistoryOutlined, RedoOutlined } from '@ant-design/icons';
import { Button, Card, Empty, List, Space, Typography } from 'antd';
import Link from 'next/link';

import {
  fetchRedownloadBlob,
  useDownloadHistory,
} from '@/features/download-history/api/downloadHistoryApi';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { formatBytes, formatDateTime } from '@/i18n/formatting';
import { triggerBlobDownload } from '@/lib/download/exportDownload';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

/**
 * Compact "recent exports" strip from download history (newest first).
 */
export function RecentExportsList({ pageSize = 8 }: { pageSize?: number }) {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const canView = hasPermission(PERMISSIONS.AUDIT_VIEW);
  const query = useDownloadHistory({ page: 1, pageSize }, { enabled: canView });
  const items = query.data?.items ?? [];

  if (!canView) return null;

  const handleRedownload = async (id: string, fileName: string) => {
    try {
      const blob = await fetchRedownloadBlob(id);
      triggerBlobDownload(blob, fileName);
      notify.success(t('common.exportFavorites.redownloadSuccess'));
    } catch {
      notify.error(t('common.exportFavorites.redownloadFailed'));
    }
  };

  return (
    <Card
      title={
        <Space>
          <HistoryOutlined />
          <span>{t('common.exportFavorites.recentTitle')}</span>
        </Space>
      }
      extra={
        <Link href="/admin/download-history">{t('common.exportFavorites.viewAllHistory')}</Link>
      }
    >
      {items.length === 0 && !query.isLoading ? (
        <Empty description={t('common.exportFavorites.recentEmpty')} />
      ) : (
        <List
          loading={query.isLoading}
          dataSource={items}
          renderItem={(item) => (
            <List.Item
              actions={
                item.canRedownload
                  ? [
                      <Button
                        key="redo"
                        size="small"
                        icon={<RedoOutlined />}
                        onClick={() => void handleRedownload(item.id, item.fileName)}
                      >
                        {t('common.exportFavorites.redownload')}
                      </Button>,
                    ]
                  : undefined
              }
            >
              <List.Item.Meta
                title={<Typography.Text ellipsis>{item.fileName}</Typography.Text>}
                description={
                  <Typography.Text type="secondary">
                    {(item.sourceKind || item.fileType).toUpperCase()}
                    {item.fileSize != null
                      ? ` · ${formatBytes(item.fileSize, formatLocale)}`
                      : ''}
                    {` · ${formatDateTime(item.downloadedAt, formatLocale)}`}
                  </Typography.Text>
                }
              />
            </List.Item>
          )}
        />
      )}
    </Card>
  );
}
