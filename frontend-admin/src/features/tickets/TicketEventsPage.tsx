'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, DatePicker, Form, Input, InputNumber, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import Link from 'next/link';

import {
  adminTicketEventQueryKeys,
  createAdminTicketEvent,
  useAdminTicketEvents,
  type AdminTicketEvent,
} from '@/api/admin/ticket-events';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

type EventFormValues = {
  name: string;
  venue: string;
  capacity: number;
  startAtUtc: Dayjs;
  endAtUtc: Dayjs;
};

export function TicketEventsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const canManage = hasPermission(PERMISSIONS.PRODUCT_MANAGE);
  const queryClient = useQueryClient();
  const [form] = Form.useForm<EventFormValues>();
  const list = useAdminTicketEvents();

  const create = useMutation({
    mutationFn: createAdminTicketEvent,
    onSuccess: async () => {
      notify.success(t('products.ticketEvents.saved'));
      form.resetFields();
      await queryClient.invalidateQueries({ queryKey: adminTicketEventQueryKeys.all });
    },
    onError: (error) => {
      notify.apiError(error, {
        logContext: 'TicketEventsPage.create',
        fallbackKey: 'products.ticketEvents.saveError',
      });
    },
  });

  const columns: ColumnsType<AdminTicketEvent> = [
    { title: t('products.ticketEvents.name'), dataIndex: 'name' },
    { title: t('products.ticketEvents.venue'), dataIndex: 'venue' },
    { title: t('products.ticketEvents.capacity'), dataIndex: 'capacity' },
    {
      title: t('products.ticketEvents.start'),
      dataIndex: 'startAtUtc',
      render: (value: string) => value.slice(0, 16).replace('T', ' '),
    },
    {
      title: t('products.ticketEvents.seats'),
      dataIndex: 'id',
      render: (id: string) => (
        <Link href={`/admin/tickets/events/${id}/seats`}>{t('products.ticketEvents.seats')}</Link>
      ),
    },
  ];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <AdminPageHeader
        title={t('products.ticketEvents.title')}
        subtitle={t('products.ticketEvents.subtitle')}
      />
      {canManage ? (
        <Form<EventFormValues>
          form={form}
          layout="inline"
          initialValues={{
            capacity: 20,
            startAtUtc: dayjs('2026-11-01T18:00:00Z'),
            endAtUtc: dayjs('2026-11-01T20:00:00Z'),
          }}
          onFinish={(values) => {
            create.mutate({
              name: values.name.trim(),
              venue: values.venue.trim(),
              capacity: values.capacity,
              isActive: true,
              startAtUtc: values.startAtUtc.toISOString(),
              endAtUtc: values.endAtUtc.toISOString(),
            });
          }}
        >
          <Form.Item
            name="name"
            label={t('products.ticketEvents.name')}
            rules={[{ required: true }]}
          >
            <Input aria-label={t('products.ticketEvents.name')} />
          </Form.Item>
          <Form.Item
            name="venue"
            label={t('products.ticketEvents.venue')}
            rules={[{ required: true }]}
          >
            <Input aria-label={t('products.ticketEvents.venue')} />
          </Form.Item>
          <Form.Item name="capacity" label={t('products.ticketEvents.capacity')}>
            <InputNumber min={0} max={100000} aria-label={t('products.ticketEvents.capacity')} />
          </Form.Item>
          <Form.Item name="startAtUtc" label={t('products.ticketEvents.start')}>
            <DatePicker showTime aria-label={t('products.ticketEvents.start')} />
          </Form.Item>
          <Form.Item name="endAtUtc" label={t('products.ticketEvents.end')}>
            <DatePicker showTime aria-label={t('products.ticketEvents.end')} />
          </Form.Item>
          <Form.Item>
            <Button type="primary" htmlType="submit" loading={create.isPending}>
              {t('products.ticketEvents.save')}
            </Button>
          </Form.Item>
        </Form>
      ) : null}
      {list.isError ? (
        <Typography.Text type="danger">{t('products.ticketEvents.loadError')}</Typography.Text>
      ) : null}
      <Table
        rowKey={(row) => row.id}
        loading={list.isLoading}
        dataSource={list.data ?? []}
        columns={columns}
        pagination={false}
        locale={{ emptyText: t('products.ticketEvents.empty') }}
      />
    </Space>
  );
}
