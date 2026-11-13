'use client';

import { DatabaseOutlined, ReloadOutlined } from '@ant-design/icons';
import { Button, Card, Col, Row, Space, Statistic, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import React, { useMemo } from 'react';

import { DateColumn } from '@/components/DateColumn';
import {
  bytesToMegabytes,
  selectActiveArchivedExports,
  useDepExportArchiveReport,
  type DepExportArchiveSummaryItemDto,
} from '@/features/rksv/hooks/useDepExportArchive';
import { useI18n } from '@/i18n';
import { formatBytes, formatDate } from '@/i18n/formatting';

type Props = {
  style?: React.CSSProperties;
};

export function DepExportArchiveCard({ style }: Props) {
  const { t, formatLocale } = useI18n();
  const { data, isLoading, isFetching, refetch } = useDepExportArchiveReport();

  const archivedExports = useMemo(() => selectActiveArchivedExports(data), [data]);
  const totalSizeMb = bytesToMegabytes(data?.totalArchivedSizeBytes ?? 0);
  const oldestLabel = data?.oldestArchivedExportAt
    ? formatDate(data.oldestArchivedExportAt, formatLocale)
    : '—';

  const columns: ColumnsType<DepExportArchiveSummaryItemDto> = [
    {
      title: t('rksvHub.depExportArchive.colExport'),
      dataIndex: 'fileName',
      key: 'fileName',
      ellipsis: true,
    },
    {
      title: t('rksvHub.depExportArchive.colDate'),
      dataIndex: 'exportedAt',
      key: 'exportedAt',
      width: 140,
      render: (value: string) => <DateColumn date={value} format="short" />,
    },
    {
      title: t('rksvHub.depExportArchive.colSize'),
      dataIndex: 'fileSizeBytes',
      key: 'fileSizeBytes',
      width: 110,
      render: (bytes: number) => formatBytes(bytes ?? 0, formatLocale),
    },
    {
      title: t('rksvHub.depExportArchive.colChecksum'),
      dataIndex: 'archiveChecksum',
      key: 'archiveChecksum',
      width: 180,
      render: (checksum: string | null | undefined) =>
        checksum ? (
          <Typography.Text copyable={{ text: checksum }} style={{ fontFamily: 'monospace', fontSize: 12 }}>
            {checksum.slice(0, 12)}…
          </Typography.Text>
        ) : (
          '—'
        ),
    },
    {
      title: t('rksvHub.depExportArchive.colRetention'),
      dataIndex: 'retentionUntil',
      key: 'retentionUntil',
      width: 150,
      render: (value: string | null | undefined) =>
        value ? <DateColumn date={value} format="short" /> : '—',
    },
    {
      title: t('rksvHub.depExportArchive.colStatus'),
      key: 'status',
      width: 120,
      render: (_, row) =>
        row.hasArchiveFile ? (
          <Tag color="success">{t('rksvHub.depExportArchive.statusOnDisk')}</Tag>
        ) : (
          <Tag>{t('rksvHub.depExportArchive.statusMetaOnly')}</Tag>
        ),
    },
  ];

  return (
    <Card
      title={
        <Space>
          <DatabaseOutlined />
          <span>{t('rksvHub.depExportArchive.title')}</span>
        </Space>
      }
      loading={isLoading}
      style={style}
      extra={
        <Button
          icon={<ReloadOutlined />}
          loading={isFetching}
          onClick={() => {
            void refetch();
          }}
        >
          {t('rksvHub.depExportArchive.refresh')}
        </Button>
      }
    >
      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={8}>
          <Statistic
            title={t('rksvHub.depExportArchive.statArchived')}
            value={data?.archivedCount ?? 0}
          />
        </Col>
        <Col xs={24} sm={8}>
          <Statistic
            title={t('rksvHub.depExportArchive.statTotalSize')}
            value={totalSizeMb}
            suffix={t('rksvHub.depExportArchive.sizeSuffixMb')}
          />
        </Col>
        <Col xs={24} sm={8}>
          <Statistic title={t('rksvHub.depExportArchive.statOldest')} value={oldestLabel} />
        </Col>
      </Row>

      {(data?.pendingArchiveCount ?? 0) > 0 ? (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          {t('rksvHub.depExportArchive.pendingNote', {
            count: data?.pendingArchiveCount ?? 0,
            years: data?.retentionYears ?? 7,
          })}
        </Typography.Paragraph>
      ) : (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          {t('rksvHub.depExportArchive.retentionNote', {
            years: data?.retentionYears ?? 7,
          })}
        </Typography.Paragraph>
      )}

      <Table<DepExportArchiveSummaryItemDto>
        rowKey="exportId"
        size="small"
        loading={isFetching}
        dataSource={archivedExports}
        columns={columns}
        pagination={{ pageSize: 10, hideOnSinglePage: true }}
        locale={{ emptyText: t('rksvHub.depExportArchive.empty') }}
        scroll={{ x: 900 }}
      />
    </Card>
  );
}
