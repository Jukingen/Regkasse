'use client';

import { Alert, Card, Descriptions, Empty, Table, Typography } from 'antd';
import Link from 'next/link';

import { useAdminFolios, useAdminRoom, useAdminRoomMinibar } from '@/api/admin/lodging';
import { useAmbientVerticalProfile } from '@/api/admin/vertical-profile';
import { useI18n } from '@/i18n';

export function RoomDetail({ roomId }: { roomId: string }) {
  const { t } = useI18n();
  const ambientProfile = useAmbientVerticalProfile();
  const isLodging = ambientProfile.data?.profileId === 'beherbergung';
  const room = useAdminRoom(roomId, { enabled: isLodging || !ambientProfile.isFetched });
  const folios = useAdminFolios({ openOnly: true }, { enabled: isLodging || !ambientProfile.isFetched });
  const minibar = useAdminRoomMinibar(roomId, { enabled: isLodging });
  const guest = (folios.data ?? []).find((row) => row.roomId === roomId && row.isOpen);

  if (ambientProfile.isFetched && !isLodging) {
    return <Alert type="info" showIcon title={t('admin.rooms.unavailable')} />;
  }

  const row = room.data;
  return (
    <>
      <Link href="/admin/rooms">{t('admin.rooms.back')}</Link>
      {room.isError ? <Typography.Text type="danger">{t('admin.rooms.loadError')}</Typography.Text> : null}
      {row ? (
        <Card title={row.number} style={{ marginTop: 16 }}>
          <Descriptions column={1}>
            <Descriptions.Item label={t('admin.rooms.colType')}>{row.type}</Descriptions.Item>
            <Descriptions.Item label={t('admin.rooms.colCapacity')}>{row.capacity}</Descriptions.Item>
            <Descriptions.Item label={t('admin.rooms.colStatus')}>
              {t(`admin.rooms.status.${row.status ?? (row.occupied ? 'Occupied' : 'Available')}`)}
            </Descriptions.Item>
            <Descriptions.Item label={t('admin.rooms.colGuest')}>
              {guest ? (
                <Link href={`/admin/folios/${guest.id}`}>{guest.customerName?.trim() || '—'}</Link>
              ) : (
                t('admin.rooms.noGuest')
              )}
            </Descriptions.Item>
          </Descriptions>
        </Card>
      ) : null}
      {isLodging ? (
        <Card title={t('admin.rooms.minibarTitle')} style={{ marginTop: 16 }}>
          <Table
            size="small"
            rowKey="id"
            pagination={false}
            loading={minibar.isLoading}
            dataSource={minibar.data ?? []}
            locale={{ emptyText: <Empty description={t('admin.rooms.minibarEmpty')} /> }}
            columns={[
              { title: t('admin.rooms.minibarDescription'), dataIndex: 'description' },
              { title: t('admin.rooms.minibarQuantity'), dataIndex: 'quantity' },
              { title: t('admin.rooms.minibarUnitPrice'), dataIndex: 'unitPrice' },
              {
                title: t('admin.rooms.minibarStatus'),
                dataIndex: 'status',
                render: (status: string) => t(`admin.rooms.minibarStatusValue.${status}`),
              },
            ]}
          />
        </Card>
      ) : null}
    </>
  );
}
