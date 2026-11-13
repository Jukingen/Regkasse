'use client';

import { Alert, Button, Card, Form, Input, InputNumber, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useMemo, useState } from 'react';
import axios from 'axios';

import {
  useAdminFolios,
  useAdminRooms,
  useCreateAdminRoom,
  useDeleteAdminRoom,
  useUpdateAdminRoomDetails,
  useUpdateAdminRoomStatus,
  type AdminGuestFolioRow,
  type AdminRoomRow,
  type CreateAdminRoomRequest,
} from '@/api/admin/lodging';
import { useAmbientVerticalProfile } from '@/api/admin/vertical-profile';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

const STATUS_OPTIONS = ['Available', 'Cleaning', 'Maintenance'] as const;

type RoomFormValues = CreateAdminRoomRequest;

function lodgingErrorCode(error: unknown): string | null {
  if (!axios.isAxiosError(error)) return null;
  const data = error.response?.data;
  if (data && typeof data === 'object' && 'code' in data && typeof data.code === 'string') {
    return data.code;
  }
  return null;
}

export function LodgingWorkspace() {
  const { t } = useI18n();
  const { modal } = useAntdApp();
  const { user } = useAuth();
  const notify = useNotify();
  const canWrite = hasPermission(user, PERMISSIONS.PRODUCT_MANAGE);
  const ambientProfile = useAmbientVerticalProfile();
  const isLodging = ambientProfile.data?.profileId === 'beherbergung';
  const rooms = useAdminRooms({ enabled: isLodging || !ambientProfile.isFetched });
  const folios = useAdminFolios({ openOnly: true }, { enabled: isLodging || !ambientProfile.isFetched });
  const createRoom = useCreateAdminRoom();
  const updateDetails = useUpdateAdminRoomDetails();
  const updateStatus = useUpdateAdminRoomStatus();
  const deleteRoom = useDeleteAdminRoom();
  const [form] = Form.useForm<RoomFormValues>();
  const [editor, setEditor] = useState<AdminRoomRow | 'create' | null>(null);

  const guestByRoom = useMemo(() => {
    const map = new Map<string, AdminGuestFolioRow>();
    for (const folio of folios.data ?? []) {
      if (folio.isOpen) map.set(folio.roomId, folio);
    }
    return map;
  }, [folios.data]);

  if (ambientProfile.isFetched && !isLodging) {
    return <Alert type="info" showIcon title={t('admin.rooms.unavailable')} />;
  }

  const openEditor = (room: AdminRoomRow | 'create') => {
    setEditor(room);
    form.setFieldsValue(
      room === 'create'
        ? { number: '', type: '', capacity: 2 }
        : { number: room.number, type: room.type, capacity: room.capacity }
    );
  };

  const roomColumns: ColumnsType<AdminRoomRow> = [
    {
      title: t('admin.rooms.colNumber'),
      dataIndex: 'number',
      render: (number: string, row) => <Link href={`/admin/rooms/${row.id}`}>{number}</Link>,
    },
    { title: t('admin.rooms.colType'), dataIndex: 'type' },
    { title: t('admin.rooms.colCapacity'), dataIndex: 'capacity' },
    {
      title: t('admin.rooms.colStatus'),
      dataIndex: 'status',
      render: (status: string | undefined, row) => {
        const label = status ?? (row.occupied ? 'Occupied' : 'Available');
        return <Tag>{t(`admin.rooms.status.${label}`)}</Tag>;
      },
    },
    {
      title: t('admin.rooms.colGuest'),
      key: 'guest',
      render: (_value, row) => {
        const folio = guestByRoom.get(row.id);
        if (!folio) return '—';
        const name = folio.customerName?.trim() || '—';
        return <Link href={`/admin/folios/${folio.id}`}>{name}</Link>;
      },
    },
    {
      title: t('admin.rooms.colActions'),
      key: 'actions',
      render: (_value, row) =>
        canWrite ? (
          <Space>
            <Button size="small" onClick={() => openEditor(row)}>
              {t('admin.rooms.edit')}
            </Button>
            <Select
              size="small"
              aria-label={t('admin.rooms.changeStatus')}
              disabled={row.occupied || row.status === 'Occupied'}
              value={STATUS_OPTIONS.includes(row.status as (typeof STATUS_OPTIONS)[number]) ? row.status : undefined}
              placeholder={t('admin.rooms.changeStatus')}
              options={STATUS_OPTIONS.map((status) => ({
                value: status,
                label: t(`admin.rooms.status.${status}`),
              }))}
              onChange={(status) => {
                updateStatus.mutate(
                  { id: row.id, status },
                  {
                    onSuccess: () => notify.success(t('admin.rooms.statusSuccess')),
                    onError: () => notify.error(t('admin.rooms.saveFailed')),
                  }
                );
              }}
              style={{ minWidth: 140 }}
            />
            <Button
              size="small"
              danger
              onClick={() => {
                modal.confirm({
                  title: t('admin.rooms.deleteConfirm'),
                  onOk: () =>
                    deleteRoom.mutateAsync(row.id).then(
                      () => notify.success(t('admin.rooms.deleteSuccess')),
                      (error: unknown) => {
                        notify.error(
                          lodgingErrorCode(error) === 'ROOM_IN_USE'
                            ? t('admin.rooms.deleteBlocked')
                            : t('admin.rooms.saveFailed')
                        );
                      }
                    ),
                });
              }}
            >
              {t('admin.rooms.delete')}
            </Button>
          </Space>
        ) : (
          '—'
        ),
    },
  ];

  const roomRows = rooms.data ?? [];
  const occupiedCount = roomRows.filter((row) => row.occupied || row.status === 'Occupied').length;
  const occupancy = roomRows.length === 0 ? null : Math.round((occupiedCount / roomRows.length) * 100);

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      <Card title={t('admin.rooms.analyticsTitle')}>
        <Typography.Text>
          {t('admin.rooms.occupancy')}: {occupancy === null ? '—' : `${occupancy}%`}
        </Typography.Text>
        <div style={{ marginTop: 12 }}>
          <Link href="/admin/folios">{t('admin.rooms.foliosLink')}</Link>
        </div>
      </Card>
      <Card
        title={t('admin.rooms.roomsTitle')}
        extra={
          canWrite ? (
            <Button type="primary" onClick={() => openEditor('create')}>
              {t('admin.rooms.create')}
            </Button>
          ) : (
            <Typography.Text type="secondary">{t('admin.rooms.readOnly')}</Typography.Text>
          )
        }
      >
        {rooms.isError ? <Typography.Text type="danger">{t('admin.rooms.loadError')}</Typography.Text> : null}
        <Table<AdminRoomRow>
          rowKey="id"
          loading={rooms.isLoading}
          dataSource={roomRows}
          columns={roomColumns}
          pagination={false}
          locale={{ emptyText: t('admin.rooms.empty') }}
        />
      </Card>
      <Modal
        open={editor !== null}
        destroyOnHidden
        title={editor === 'create' ? t('admin.rooms.create') : t('admin.rooms.edit')}
        okText={t('admin.rooms.save')}
        cancelText={t('admin.rooms.cancel')}
        confirmLoading={createRoom.isPending || updateDetails.isPending}
        onCancel={() => setEditor(null)}
        onOk={() => form.submit()}
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={(values) => {
            const done = () => {
              setEditor(null);
              notify.success(
                editor === 'create' ? t('admin.rooms.createSuccess') : t('admin.rooms.updateSuccess')
              );
            };
            const fail = () => notify.error(t('admin.rooms.saveFailed'));
            if (editor === 'create') {
              createRoom.mutate(values, { onSuccess: done, onError: fail });
              return;
            }
            if (editor) {
              updateDetails.mutate({ id: editor.id, body: values }, { onSuccess: done, onError: fail });
            }
          }}
        >
          <Form.Item
            name="number"
            label={t('admin.rooms.colNumber')}
            rules={[{ required: true, message: t('admin.rooms.numberRequired') }]}
          >
            <Input />
          </Form.Item>
          <Form.Item
            name="type"
            label={t('admin.rooms.colType')}
            rules={[{ required: true, message: t('admin.rooms.typeRequired') }]}
          >
            <Input />
          </Form.Item>
          <Form.Item name="capacity" label={t('admin.rooms.colCapacity')}>
            <InputNumber min={1} max={20} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
