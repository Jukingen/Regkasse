'use client';

import { Alert, Button, Card, Form, Input, InputNumber, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';

import {
  useAdminFolios,
  useAdminRooms,
  useCreateAdminRoom,
  type AdminGuestFolioRow,
  type AdminRoomRow,
  type CreateAdminRoomRequest,
} from '@/api/admin/lodging';
import { useAmbientVerticalProfile } from '@/api/admin/vertical-profile';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

function stamp(value: string | null | undefined): string {
  return value ? value.slice(0, 19).replace('T', ' ') : '—';
}

export function LodgingWorkspace() {
  const { t } = useI18n();
  const { user } = useAuth();
  const notify = useNotify();
  const canWrite = hasPermission(user, PERMISSIONS.PRODUCT_MANAGE);
  const ambientProfile = useAmbientVerticalProfile();
  const isLodging = ambientProfile.data?.profileId === 'beherbergung';
  const rooms = useAdminRooms({ enabled: isLodging || !ambientProfile.isFetched });
  const folios = useAdminFolios({ enabled: isLodging || !ambientProfile.isFetched });
  const createRoom = useCreateAdminRoom();
  const [form] = Form.useForm<CreateAdminRoomRequest>();

  if (ambientProfile.isFetched && !isLodging) {
    return <Alert type="info" showIcon title={t('tenants.lodging.unavailable')} />;
  }

  const roomColumns: ColumnsType<AdminRoomRow> = [
    { title: t('tenants.lodging.colNumber'), dataIndex: 'number' },
    { title: t('tenants.lodging.colType'), dataIndex: 'type' },
    { title: t('tenants.lodging.colCapacity'), dataIndex: 'capacity' },
    {
      title: t('tenants.lodging.colStatus'),
      dataIndex: 'status',
      render: (status: string | undefined, row) => {
        const label = status ?? (row.occupied ? 'Occupied' : 'Available');
        return <Tag>{t(`tenants.lodging.status.${label}`)}</Tag>;
      },
    },
  ];

  const folioColumns: ColumnsType<AdminGuestFolioRow> = [
    { title: t('tenants.lodging.colRoom'), dataIndex: 'roomNumber' },
    { title: t('tenants.lodging.colCustomer'), dataIndex: 'customerName' },
    {
      title: t('tenants.lodging.colCheckIn'),
      dataIndex: 'checkIn',
      render: stamp,
    },
    {
      title: t('tenants.lodging.colCheckOut'),
      dataIndex: 'checkOut',
      render: stamp,
    },
    {
      title: t('tenants.lodging.colBalance'),
      dataIndex: 'balance',
      render: (value: number) => value.toFixed(2),
    },
    {
      title: t('tenants.lodging.colFolioStatus'),
      dataIndex: 'isOpen',
      render: (open: boolean) =>
        open ? t('tenants.lodging.open') : t('tenants.lodging.closed'),
    },
  ];

  const roomRows = rooms.data ?? [];
  const folioRows = folios.data ?? [];
  const occupiedCount = roomRows.filter((row) => row.occupied || row.status === 'Occupied').length;
  const occupancy =
    roomRows.length === 0 ? null : Math.round((occupiedCount / roomRows.length) * 100);
  const stays = folioRows
    .filter((row) => row.checkOut)
    .map((row) => {
      const start = Date.parse(row.checkIn);
      const end = Date.parse(row.checkOut ?? '');
      return Number.isFinite(start) && Number.isFinite(end) ? (end - start) / 86_400_000 : null;
    })
    .filter((value): value is number => value !== null && value >= 0);
  const averageStay =
    stays.length === 0 ? null : stays.reduce((sum, value) => sum + value, 0) / stays.length;

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      <Card title={t('tenants.lodging.analyticsTitle')}>
        <Typography.Text>
          {t('tenants.lodging.occupancy')}: {occupancy === null ? '—' : `${occupancy}%`}
        </Typography.Text>
        <br />
        <Typography.Text>
          {t('tenants.lodging.averageStay')}:{' '}
          {averageStay === null ? '—' : averageStay.toFixed(1)}
        </Typography.Text>
      </Card>
      <Card title={t('tenants.lodging.roomsTitle')}>
        {canWrite ? (
          <Form
            form={form}
            layout="inline"
            initialValues={{ capacity: 2 }}
            onFinish={(values) => {
              createRoom.mutate(values, {
                onSuccess: () => {
                  form.resetFields();
                  notify.success(t('tenants.lodging.createSuccess'));
                },
                onError: () => notify.error(t('tenants.lodging.createFailed')),
              });
            }}
            style={{ marginBottom: 16 }}
          >
            <Form.Item
              name="number"
              rules={[{ required: true, message: t('tenants.lodging.numberRequired') }]}
            >
              <Input aria-label={t('tenants.lodging.colNumber')} placeholder={t('tenants.lodging.colNumber')} />
            </Form.Item>
            <Form.Item
              name="type"
              rules={[{ required: true, message: t('tenants.lodging.typeRequired') }]}
            >
              <Input aria-label={t('tenants.lodging.colType')} placeholder={t('tenants.lodging.colType')} />
            </Form.Item>
            <Form.Item name="capacity">
              <InputNumber
                min={1}
                max={20}
                aria-label={t('tenants.lodging.colCapacity')}
              />
            </Form.Item>
            <Form.Item>
              <Button htmlType="submit" type="primary" loading={createRoom.isPending}>
                {t('tenants.lodging.create')}
              </Button>
            </Form.Item>
          </Form>
        ) : (
          <Typography.Text type="secondary">{t('tenants.lodging.readOnly')}</Typography.Text>
        )}
        {rooms.isError ? (
          <Typography.Text type="danger">{t('tenants.lodging.loadError')}</Typography.Text>
        ) : null}
        <Table<AdminRoomRow>
          rowKey="id"
          loading={rooms.isLoading}
          dataSource={rooms.data ?? []}
          columns={roomColumns}
          pagination={false}
          locale={{ emptyText: t('tenants.lodging.emptyRooms') }}
        />
      </Card>
      <Card title={t('tenants.lodging.foliosTitle')}>
        {folios.isError ? (
          <Typography.Text type="danger">{t('tenants.lodging.loadError')}</Typography.Text>
        ) : null}
        <Table<AdminGuestFolioRow>
          rowKey="id"
          loading={folios.isLoading}
          dataSource={folios.data ?? []}
          columns={folioColumns}
          pagination={false}
          locale={{ emptyText: t('tenants.lodging.emptyFolios') }}
        />
      </Card>
    </Space>
  );
}
