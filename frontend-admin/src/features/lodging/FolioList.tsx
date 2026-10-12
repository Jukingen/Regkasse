'use client';

import { Alert, DatePicker, Segmented, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import Link from 'next/link';
import { useState } from 'react';

import { useAdminFolios, type AdminFolioListQuery, type AdminGuestFolioRow } from '@/api/admin/lodging';
import { useAmbientVerticalProfile } from '@/api/admin/vertical-profile';
import { useI18n } from '@/i18n';

function stamp(value: string | null | undefined): string {
  return value ? value.slice(0, 19).replace('T', ' ') : '—';
}

export function FolioList() {
  const { t } = useI18n();
  const ambientProfile = useAmbientVerticalProfile();
  const isLodging = ambientProfile.data?.profileId === 'beherbergung';
  const [mode, setMode] = useState<'open' | 'closed'>('open');
  const [range, setRange] = useState<[Dayjs, Dayjs] | null>(null);

  const query: AdminFolioListQuery =
    mode === 'open'
      ? { openOnly: true }
      : {
          fromUtc: range?.[0]?.startOf('day').toISOString(),
          toUtc: range?.[1]?.add(1, 'day').startOf('day').toISOString(),
        };
  const folios = useAdminFolios(query, { enabled: isLodging || !ambientProfile.isFetched });

  if (ambientProfile.isFetched && !isLodging) {
    return <Alert type="info" showIcon title={t('admin.folios.unavailable')} />;
  }

  const rows = (folios.data ?? []).filter((row) => (mode === 'open' ? row.isOpen : !row.isOpen));
  const columns: ColumnsType<AdminGuestFolioRow> = [
    {
      title: t('admin.folios.colGuest'),
      dataIndex: 'customerName',
      render: (name: string | null | undefined, row) => (
        <Link href={`/admin/folios/${row.id}`}>{name?.trim() || '—'}</Link>
      ),
    },
    {
      title: t('admin.folios.colRoom'),
      dataIndex: 'roomNumber',
      render: (number: string | null | undefined, row) => (
        <Link href={`/admin/rooms/${row.roomId}`}>{number || '—'}</Link>
      ),
    },
    { title: t('admin.folios.colCheckIn'), dataIndex: 'checkIn', render: stamp },
    { title: t('admin.folios.colCheckOut'), dataIndex: 'checkOut', render: stamp },
    {
      title: t('admin.folios.colBalance'),
      dataIndex: 'balance',
      render: (value: number) => value.toFixed(2),
    },
    {
      title: t('admin.folios.colStatus'),
      dataIndex: 'isOpen',
      render: (open: boolean) => (open ? t('admin.folios.open') : t('admin.folios.closed')),
    },
  ];

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      <Space wrap>
        <Segmented
          value={mode}
          onChange={(value) => setMode(value as 'open' | 'closed')}
          options={[
            { label: t('admin.folios.openTab'), value: 'open' },
            { label: t('admin.folios.closedTab'), value: 'closed' },
          ]}
        />
        {mode === 'closed' ? (
          <DatePicker.RangePicker
            value={range}
            onChange={(value) => {
              if (value?.[0] && value[1]) setRange([value[0], value[1]]);
              else setRange(null);
            }}
            allowEmpty={[true, true]}
          />
        ) : null}
      </Space>
      {folios.isError ? <Typography.Text type="danger">{t('admin.folios.loadError')}</Typography.Text> : null}
      <Table<AdminGuestFolioRow>
        rowKey="id"
        loading={folios.isLoading}
        dataSource={rows}
        columns={columns}
        pagination={false}
        locale={{ emptyText: mode === 'open' ? t('admin.folios.emptyOpen') : t('admin.folios.emptyClosed') }}
      />
    </Space>
  );
}
