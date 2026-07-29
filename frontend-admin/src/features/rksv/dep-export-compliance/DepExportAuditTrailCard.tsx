'use client';

import { AuditOutlined, ReloadOutlined } from '@ant-design/icons';
import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Input,
  Row,
  Select,
  Space,
  Statistic,
  Table,
  Tag,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import React, { useMemo, useState } from 'react';

import {
  auditActionTagColor,
  useDepExportAuditReport,
  useDepExportAuditTrail,
  type DepExportAuditEntryDto,
} from '@/features/rksv/hooks/useDepExportAudit';
import { useI18n } from '@/i18n';
import dayjs from '@/lib/dayjs';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

type Props = {
  style?: React.CSSProperties;
};

const ACTION_VALUES = ['Created', 'Downloaded', 'Archived', 'Deleted', 'Validated', 'Failed'] as const;

export function DepExportAuditTrailCard({ style }: Props) {
  const { t } = useI18n();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>([
    dayjs().subtract(12, 'month').startOf('day'),
    dayjs().endOf('day'),
  ]);
  const [action, setAction] = useState<string | undefined>();
  const [userSearch, setUserSearch] = useState('');
  const [userSearchDebounced, setUserSearchDebounced] = useState('');

  const fromUtc = range?.[0]?.toISOString();
  const toUtc = range?.[1]?.toISOString();

  const trailParams = useMemo(
    () => ({
      fromUtc,
      toUtc,
      action,
      userSearch: userSearchDebounced || undefined,
      limit: 100,
    }),
    [fromUtc, toUtc, action, userSearchDebounced]
  );

  const trailQuery = useDepExportAuditTrail(trailParams);
  const reportQuery = useDepExportAuditReport(fromUtc, toUtc);

  const isFetching = trailQuery.isFetching || reportQuery.isFetching;
  const loadError = trailQuery.error ?? reportQuery.error;

  const actionOptions = ACTION_VALUES.map((value) => ({
    value,
    label: t(`rksvHub.depExportAudit.actions.${value}`),
  }));

  const columns: ColumnsType<DepExportAuditEntryDto> = [
    {
      title: t('rksvHub.depExportAudit.colTime'),
      dataIndex: 'actionAt',
      key: 'actionAt',
      width: 160,
      render: (value: string) => dayjs(value).format('DD.MM.YYYY HH:mm'),
    },
    {
      title: t('rksvHub.depExportAudit.colAction'),
      dataIndex: 'action',
      key: 'action',
      width: 120,
      render: (value: string) => {
        const key = `rksvHub.depExportAudit.actions.${value}`;
        const label = t(key);
        return <Tag color={auditActionTagColor(value)}>{label === key ? value : label}</Tag>;
      },
    },
    {
      title: t('rksvHub.depExportAudit.colExport'),
      dataIndex: 'exportName',
      key: 'exportName',
      ellipsis: true,
    },
    {
      title: t('rksvHub.depExportAudit.colUser'),
      dataIndex: 'userEmail',
      key: 'userEmail',
      width: 200,
      ellipsis: true,
      render: (email: string | null | undefined, row) => email || row.userId || '—',
    },
    {
      title: t('rksvHub.depExportAudit.colIp'),
      dataIndex: 'ipAddress',
      key: 'ipAddress',
      width: 130,
      render: (value: string | null | undefined) => value || '—',
    },
  ];

  const refresh = () => {
    void trailQuery.refetch();
    void reportQuery.refetch();
  };

  return (
    <Card
      title={
        <Space>
          <AuditOutlined />
          <span>{t('rksvHub.depExportAudit.title')}</span>
        </Space>
      }
      loading={trailQuery.isLoading && !trailQuery.data}
      style={style}
      extra={
        <Button icon={<ReloadOutlined />} loading={isFetching} onClick={refresh}>
          {t('rksvHub.depExportAudit.refresh')}
        </Button>
      }
    >
      {loadError ? (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title={t('rksvHub.depExportAudit.loadFailed')}
          description={
            <ApiErrorAlertDescription
              t={t}
              error={loadError}
              logContext="DepExportAuditTrail.load"
              fallbackKey="rksvHub.depExportAudit.loadFailed"
            />
          }
        />
      ) : null}

      <Row gutter={16} style={{ marginBottom: 16 }}>
        <Col xs={12} sm={6}>
          <Statistic
            title={t('rksvHub.depExportAudit.statTotal')}
            value={reportQuery.data?.totalEntries ?? 0}
          />
        </Col>
        <Col xs={12} sm={6}>
          <Statistic
            title={t('rksvHub.depExportAudit.statCreated')}
            value={reportQuery.data?.countsByAction?.Created ?? 0}
          />
        </Col>
        <Col xs={12} sm={6}>
          <Statistic
            title={t('rksvHub.depExportAudit.statDownloaded')}
            value={reportQuery.data?.countsByAction?.Downloaded ?? 0}
          />
        </Col>
        <Col xs={12} sm={6}>
          <Statistic
            title={t('rksvHub.depExportAudit.statArchived')}
            value={reportQuery.data?.countsByAction?.Archived ?? 0}
          />
        </Col>
      </Row>

      <Space wrap style={{ marginBottom: 16, width: '100%' }}>
        <DatePicker.RangePicker
          value={range}
          onChange={(values) => setRange(values)}
          allowClear
        />
        <Select
          allowClear
          placeholder={t('rksvHub.depExportAudit.actionPlaceholder')}
          style={{ minWidth: 160 }}
          options={actionOptions}
          value={action}
          onChange={(value) => setAction(value)}
        />
        <Input.Search
          allowClear
          placeholder={t('rksvHub.depExportAudit.userSearchPlaceholder')}
          style={{ minWidth: 220 }}
          value={userSearch}
          onChange={(e) => setUserSearch(e.target.value)}
          onSearch={(value) => setUserSearchDebounced(value.trim())}
        />
      </Space>

      <Table<DepExportAuditEntryDto>
        rowKey="id"
        size="small"
        columns={columns}
        dataSource={trailQuery.data ?? []}
        pagination={{ pageSize: 10, showSizeChanger: false }}
        locale={{ emptyText: t('rksvHub.depExportAudit.empty') }}
      />
    </Card>
  );
}
