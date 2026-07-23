'use client';

import {
  CopyOutlined,
  DeleteOutlined,
  EyeOutlined,
  FileOutlined,
  RedoOutlined,
  ReloadOutlined,
  SearchOutlined,
} from '@ant-design/icons';
import { Alert, Button, Card, DatePicker, Empty, Input, List, Select, Space, Typography } from 'antd';
import type { Dayjs } from 'dayjs';
import Link from 'next/link';
import { useMemo, useState } from 'react';

import { DownloadProgressModal } from '@/components/ui/DownloadProgressModal';
import { FilePreviewModal } from '@/components/ui/FilePreviewModal';
import {
  useCleanupOldDownloadHistory,
  useDownloadHistory,
  useDownloadHistoryStats,
  fetchRedownloadBlob,
  redownloadFromHistory,
} from '@/features/download-history/api/downloadHistoryApi';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useProgressiveDownload } from '@/hooks/useProgressiveDownload';
import { useI18n } from '@/i18n/I18nProvider';
import { formatBytes, formatDate, formatDateTime } from '@/i18n/formatting';
import { shouldShowDownloadProgress } from '@/lib/download/downloadProgressMath';
import { canPreviewFile } from '@/lib/preview/filePreview';

const { RangePicker } = DatePicker;

type Props = {
  /** Optional default filter by extension/type (e.g. json). */
  fileType?: string;
};

function sourceKindLabel(
  sourceKind: string | null | undefined,
  fileType: string,
  t: (key: string) => string
): string {
  const kind = sourceKind?.trim().toLowerCase();
  if (kind === 'dep-export' || kind === 'dep-export-live') {
    return t('common.downloadHistory.kinds.depExport');
  }
  if (kind === 'invoice') return t('common.downloadHistory.kinds.invoice');
  if (kind === 'backup' || kind === 'tenant-backup' || kind === 'system-backup') {
    return t('common.downloadHistory.kinds.backup');
  }
  if (kind) return kind;
  return fileType.toUpperCase();
}

/**
 * Download-history list with search, date/type filters, stats, and retention cleanup.
 */
export function DownloadHistoryPanel({ fileType: lockedFileType }: Props) {
  const { t, formatLocale } = useI18n();
  const { modal } = useAntdApp();
  const notify = useNotify();
  const progressiveDownload = useProgressiveDownload();
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [sourceKind, setSourceKind] = useState<string | undefined>();
  const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [statsOpen, setStatsOpen] = useState(true);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewBusy, setPreviewBusy] = useState(false);
  const [previewMeta, setPreviewMeta] = useState<{
    id: string;
    fileName: string;
    fileType: string;
    blob: Blob;
  } | null>(null);

  const listParams = useMemo(
    () => ({
      page,
      pageSize: 20,
      fileType: lockedFileType,
      sourceKind,
      q: search || undefined,
      fromUtc: dateRange?.[0]?.startOf('day').toISOString(),
      toUtc: dateRange?.[1]?.endOf('day').toISOString(),
    }),
    [page, lockedFileType, sourceKind, search, dateRange]
  );

  const { data, isLoading, isError, refetch, isFetching } = useDownloadHistory(listParams);
  const statsQuery = useDownloadHistoryStats();
  const cleanupMutation = useCleanupOldDownloadHistory();

  const handleRedownload = async (
    id: string,
    fileName: string,
    canRedownload: boolean,
    fileSize?: number | null,
    sourceKind?: string | null
  ) => {
    if (!canRedownload) {
      notify.info(t('common.downloadHistory.redownloadUnavailable'));
      return;
    }
    setBusyId(id);
    const isBackup =
      sourceKind?.toLowerCase().includes('backup') === true ||
      fileName.toLowerCase().includes('backup');
    const label = isBackup
      ? t('common.downloadProgress.labelBackup')
      : t('common.downloadProgress.labelFile');
    try {
      if (shouldShowDownloadProgress(fileSize, isBackup)) {
        await progressiveDownload.run({
          url: `/api/admin/download-history/${id}/redownload`,
          fileName,
          label,
          expectedTotalBytes: fileSize,
        });
        notify.success(t('common.downloadHistory.redownloadSuccess'));
        void refetch();
        void statsQuery.refetch();
      } else {
        await redownloadFromHistory(id, fileName);
        notify.success(t('common.downloadHistory.redownloadSuccess'));
        void refetch();
        void statsQuery.refetch();
      }
    } catch {
      notify.error(t('common.downloadHistory.redownloadFailed'));
    } finally {
      setBusyId(null);
    }
  };

  const handleCopyName = async (fileName: string) => {
    try {
      await navigator.clipboard.writeText(fileName);
      notify.success(t('common.downloadHistory.copyNameSuccess'));
    } catch {
      notify.error(t('common.downloadHistory.copyNameFailed'));
    }
  };

  const handlePreview = async (
    id: string,
    fileName: string,
    fileType: string,
    canRedownload: boolean
  ) => {
    if (!canRedownload) {
      notify.info(t('common.downloadHistory.redownloadUnavailable'));
      return;
    }
    if (!canPreviewFile(fileName, fileType)) {
      notify.info(t('common.filePreview.unsupported'));
      return;
    }
    setBusyId(id);
    setPreviewBusy(true);
    try {
      const blob = await fetchRedownloadBlob(id);
      setPreviewMeta({ id, fileName, fileType, blob });
      setPreviewOpen(true);
    } catch {
      notify.error(t('common.filePreview.loadFailed'));
    } finally {
      setBusyId(null);
      setPreviewBusy(false);
    }
  };

  const handleCleanup = () => {
    modal.confirm({
      title: t('common.downloadHistory.cleanupConfirmTitle'),
      content: t('common.downloadHistory.cleanupConfirmBody'),
      okText: t('common.downloadHistory.cleanupConfirmOk'),
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          const result = await cleanupMutation.mutateAsync();
          notify.success(
            t('common.downloadHistory.cleanupSuccess', { count: result.deletedCount })
          );
          setPage(1);
          void refetch();
          void statsQuery.refetch();
        } catch {
          notify.error(t('common.downloadHistory.cleanupFailed'));
          throw new Error('cleanup failed');
        }
      },
    });
  };

  const applySearch = () => {
    setPage(1);
    setSearch(searchInput.trim());
  };

  const typeOptions = [
    { value: 'dep-export', label: t('common.downloadHistory.kinds.depExport') },
    { value: 'invoice', label: t('common.downloadHistory.kinds.invoice') },
    { value: 'backup', label: t('common.downloadHistory.kinds.backup') },
    { value: 'tenant-backup', label: t('common.downloadHistory.kinds.tenantBackup') },
  ];

  return (
    <Card
      size="small"
      title={t('common.downloadHistory.title')}
      extra={
        <Space>
          <Link href="/admin/download-history/analytics">
            <Button size="small" type="link">
              {t('common.downloadAnalytics.menuLabel')}
            </Button>
          </Link>
          <Button
            icon={<ReloadOutlined />}
            onClick={() => {
              void refetch();
              void statsQuery.refetch();
            }}
            loading={isFetching || statsQuery.isFetching}
            size="small"
          >
            {t('common.buttons.refresh')}
          </Button>
        </Space>
      }
    >
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        <Alert
          type="info"
          showIcon
          title={t('common.downloadHistory.retentionTitle')}
          description={t('common.downloadHistory.retentionBody')}
        />

        {statsOpen && statsQuery.data ? (
          <Alert
            type="success"
            showIcon
            closable
            onClose={() => setStatsOpen(false)}
            title={t('common.downloadHistory.statsTitle')}
            description={t('common.downloadHistory.statsBody', {
              count: statsQuery.data.fileCount,
              size: formatBytes(statsQuery.data.totalBytes, formatLocale),
            })}
          />
        ) : null}

        <Space wrap style={{ width: '100%' }}>
          <Input
            allowClear
            prefix={<SearchOutlined />}
            placeholder={t('common.downloadHistory.searchPlaceholder')}
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            onPressEnter={applySearch}
            style={{ width: 220 }}
          />
          <Button type="default" onClick={applySearch}>
            {t('common.downloadHistory.searchButton')}
          </Button>
          <RangePicker
            value={dateRange}
            onChange={(v) => {
              setPage(1);
              setDateRange(v);
            }}
            format="DD.MM.YYYY"
            placeholder={[
              t('common.downloadHistory.dateFrom'),
              t('common.downloadHistory.dateTo'),
            ]}
          />
          {!lockedFileType ? (
            <Select
              allowClear
              placeholder={t('common.downloadHistory.typeFilter')}
              style={{ minWidth: 160 }}
              options={typeOptions}
              value={sourceKind}
              onChange={(v) => {
                setPage(1);
                setSourceKind(v);
              }}
            />
          ) : null}
        </Space>

        {isError ? (
          <Alert type="error" showIcon title={t('common.downloadHistory.loadFailed')} />
        ) : (
          <List
            loading={isLoading}
            locale={{ emptyText: <Empty description={t('common.downloadHistory.empty')} /> }}
            dataSource={data?.items ?? []}
            pagination={
              data && data.totalCount > data.pageSize
                ? {
                    current: page,
                    pageSize: data.pageSize,
                    total: data.totalCount,
                    onChange: setPage,
                    size: 'small',
                  }
                : false
            }
            renderItem={(item) => (
              <List.Item
                actions={[
                  canPreviewFile(item.fileName, item.fileType) ? (
                    <Button
                      key="preview"
                      size="small"
                      icon={<EyeOutlined />}
                      loading={busyId === item.id && previewBusy}
                      disabled={!item.canRedownload}
                      onClick={() =>
                        void handlePreview(
                          item.id,
                          item.fileName,
                          item.fileType,
                          item.canRedownload
                        )
                      }
                    >
                      {t('common.downloadHistory.preview')}
                    </Button>
                  ) : null,
                  <Button
                    key="redo"
                    size="small"
                    icon={<RedoOutlined />}
                    loading={busyId === item.id && !previewBusy}
                    disabled={!item.canRedownload}
                    onClick={() =>
                      void handleRedownload(
                        item.id,
                        item.fileName,
                        item.canRedownload,
                        item.fileSize,
                        item.sourceKind
                      )
                    }
                  >
                    {t('common.downloadHistory.redownload')}
                  </Button>,
                  <Button
                    key="copy"
                    size="small"
                    icon={<CopyOutlined />}
                    onClick={() => void handleCopyName(item.fileName)}
                  >
                    {t('common.downloadHistory.copyName')}
                  </Button>,
                ].filter(Boolean)}
              >
                <List.Item.Meta
                  avatar={<FileOutlined style={{ fontSize: 20 }} />}
                  title={
                    <Space wrap size="small">
                      <Typography.Text code style={{ whiteSpace: 'pre-wrap' }}>
                        {item.fileName}
                      </Typography.Text>
                      <Typography.Text type="secondary">
                        {formatDate(item.downloadedAt, formatLocale)}
                      </Typography.Text>
                    </Space>
                  }
                  description={
                    <Space split="|" size="small" wrap>
                      <span>
                        {item.fileSize != null
                          ? formatBytes(item.fileSize, formatLocale)
                          : t('common.downloadHistory.sizeUnknown')}
                      </span>
                      <span>{sourceKindLabel(item.sourceKind, item.fileType, t)}</span>
                      <span>
                        {formatDateTime(item.downloadedAt, formatLocale, { second: '2-digit' })}
                      </span>
                    </Space>
                  }
                />
              </List.Item>
            )}
          />
        )}

        <Space wrap>
          <Button
            danger
            icon={<DeleteOutlined />}
            loading={cleanupMutation.isPending}
            onClick={handleCleanup}
          >
            {t('common.downloadHistory.cleanupOld')}
          </Button>
          <Button
            onClick={() => {
              setStatsOpen(true);
              void statsQuery.refetch();
            }}
          >
            {t('common.downloadHistory.showStats')}
          </Button>
        </Space>
      </Space>

      <FilePreviewModal
        open={previewOpen && previewMeta != null}
        fileName={previewMeta?.fileName ?? ''}
        fileType={previewMeta?.fileType}
        source={{ blob: previewMeta?.blob ?? null }}
        onClose={() => {
          setPreviewOpen(false);
          setPreviewMeta(null);
        }}
        onDownload={
          previewMeta
            ? async () => {
                await redownloadFromHistory(previewMeta.id, previewMeta.fileName);
                notify.success(t('common.downloadHistory.redownloadSuccess'));
                void refetch();
                void statsQuery.refetch();
              }
            : undefined
        }
      />
      <DownloadProgressModal {...progressiveDownload.modalProps} />
    </Card>
  );
}
