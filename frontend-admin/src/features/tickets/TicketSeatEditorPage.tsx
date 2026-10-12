'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Form, Input, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';

import {
  adminTicketEventQueryKeys,
  createAdminTicketSeat,
  useAdminTicketSeats,
  type AdminTicketSeat,
} from '@/api/admin/ticket-events';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

type SeatFormValues = {
  row: string;
  number: string;
  section?: string;
};

const STATUS_KEYS: Record<string, string> = {
  Available: 'products.ticketEvents.statusAvailable',
  Reserved: 'products.ticketEvents.statusReserved',
  Sold: 'products.ticketEvents.statusSold',
};

export function TicketSeatEditorPage({ eventId }: { eventId: string }) {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const canManage = hasPermission(PERMISSIONS.PRODUCT_MANAGE);
  const queryClient = useQueryClient();
  const [form] = Form.useForm<SeatFormValues>();
  const seats = useAdminTicketSeats(eventId);

  const create = useMutation({
    mutationFn: (body: SeatFormValues) => createAdminTicketSeat(eventId, body),
    onSuccess: async () => {
      notify.success(t('products.ticketEvents.seatSaved'));
      form.resetFields();
      await queryClient.invalidateQueries({ queryKey: adminTicketEventQueryKeys.seats(eventId) });
    },
    onError: (error) => {
      notify.apiError(error, {
        logContext: 'TicketSeatEditorPage.create',
        fallbackKey: 'products.ticketEvents.seatError',
      });
    },
  });

  const columns: ColumnsType<AdminTicketSeat> = [
    { title: t('products.ticketEvents.section'), dataIndex: 'section', render: (value) => value || '—' },
    { title: t('products.ticketEvents.row'), dataIndex: 'row' },
    { title: t('products.ticketEvents.number'), dataIndex: 'number' },
    {
      title: t('products.ticketRedemptions.colStatus'),
      dataIndex: 'status',
      render: (value: string) => (STATUS_KEYS[value] ? t(STATUS_KEYS[value]) : value),
    },
  ];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <AdminPageHeader title={t('products.ticketEvents.seatsTitle')} />
      {canManage ? (
        <Form<SeatFormValues>
          form={form}
          layout="inline"
          onFinish={(values) => {
            create.mutate({
              row: values.row.trim(),
              number: values.number.trim(),
              section: values.section?.trim() || undefined,
            });
          }}
        >
          <Form.Item name="section" label={t('products.ticketEvents.section')}>
            <Input aria-label={t('products.ticketEvents.section')} />
          </Form.Item>
          <Form.Item name="row" label={t('products.ticketEvents.row')} rules={[{ required: true }]}>
            <Input aria-label={t('products.ticketEvents.row')} />
          </Form.Item>
          <Form.Item
            name="number"
            label={t('products.ticketEvents.number')}
            rules={[{ required: true }]}
          >
            <Input aria-label={t('products.ticketEvents.number')} />
          </Form.Item>
          <Form.Item>
            <Button type="primary" htmlType="submit" loading={create.isPending}>
              {t('products.ticketEvents.addSeat')}
            </Button>
          </Form.Item>
        </Form>
      ) : null}
      {seats.isError ? (
        <Typography.Text type="danger">{t('products.ticketEvents.loadError')}</Typography.Text>
      ) : null}
      <Table
        rowKey={(row) => row.id}
        loading={seats.isLoading}
        dataSource={seats.data ?? []}
        columns={columns}
        pagination={false}
        locale={{ emptyText: t('products.ticketEvents.empty') }}
      />
    </Space>
  );
}
