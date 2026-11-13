'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Form, Input, InputNumber, Select, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useEffect, useMemo, useState } from 'react';

import {
  useAdminTicketPriceCategories,
  type AdminTicketPriceCategory,
} from '@/api/admin/ticket-price-categories';
import {
  adminTicketEventQueryKeys,
  createAdminTicketSeat,
  setAdminTicketCategoryPrice,
  updateAdminTicketEvent,
  useAdminTicketCategoryPrices,
  useAdminTicketEvents,
  useAdminTicketSeats,
  type AdminTicketCategoryPrice,
  type AdminTicketSeat,
  type SetTicketCategoryPriceBody,
} from '@/api/admin/ticket-events';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ensureTicketApiErrorTranslations } from '@/features/tickets/ticketApiErrors';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

ensureTicketApiErrorTranslations();

type SeatFormValues = {
  row: string;
  number: string;
  section?: string;
  categoryId?: string;
  gridColumn?: number | null;
  seatRole?: number;
  companionOfSeatId?: string;
};

const STATUS_KEYS: Record<string, string> = {
  Available: 'products.ticketEvents.statusAvailable',
  Reserved: 'products.ticketEvents.statusReserved',
  Sold: 'products.ticketEvents.statusSold',
};

const ROLE_KEYS: Record<string, string> = {
  Standard: 'products.ticketEvents.roleStandard',
  Accessible: 'products.ticketEvents.roleAccessible',
  Companion: 'products.ticketEvents.roleCompanion',
};

function CategoryPriceRow({
  category,
  existing,
  disabled,
  saving,
  onSave,
}: {
  category: AdminTicketPriceCategory;
  existing?: AdminTicketCategoryPrice;
  disabled: boolean;
  saving: boolean;
  onSave: (body: SetTicketCategoryPriceBody) => void;
}) {
  const { t } = useI18n();
  const [price, setPrice] = useState<number>(existing?.price ?? 0);
  const [capacity, setCapacity] = useState<number>(existing?.capacity ?? 0);

  useEffect(() => {
    setPrice(existing?.price ?? 0);
    setCapacity(existing?.capacity ?? 0);
  }, [existing?.price, existing?.capacity]);

  return (
    <Space wrap>
      <Typography.Text>{category.name}</Typography.Text>
      <InputNumber
        aria-label={`${t('products.ticketEvents.price')} ${category.name}`}
        min={0}
        step={0.01}
        value={price}
        disabled={disabled}
        onChange={(value) => setPrice(typeof value === 'number' ? value : 0)}
      />
      <InputNumber
        aria-label={`${t('products.ticketEvents.capacity')} ${category.name}`}
        min={0}
        precision={0}
        value={capacity}
        disabled={disabled}
        onChange={(value) => setCapacity(typeof value === 'number' ? value : 0)}
      />
      {disabled ? null : (
        <Button
          aria-label={`${t('products.ticketEvents.save')} ${category.name}`}
          loading={saving}
          onClick={() =>
            onSave({ categoryId: category.id, price, capacity })
          }
        >
          {t('products.ticketEvents.save')}
        </Button>
      )}
    </Space>
  );
}

export function TicketSeatEditorPage({ eventId }: { eventId: string }) {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const canManage = hasPermission(PERMISSIONS.PRODUCT_MANAGE);
  const queryClient = useQueryClient();
  const [form] = Form.useForm<SeatFormValues>();
  const events = useAdminTicketEvents();
  const categories = useAdminTicketPriceCategories();
  const prices = useAdminTicketCategoryPrices(eventId);
  const seats = useAdminTicketSeats(eventId);
  const event = (events.data ?? []).find((row) => row.id === eventId);
  const [stageEdge, setStageEdge] = useState(event?.stageEdge === 'bottom' ? 'bottom' : 'top');

  useEffect(() => {
    if (event?.stageEdge === 'bottom' || event?.stageEdge === 'top') {
      setStageEdge(event.stageEdge);
    }
  }, [event?.stageEdge]);

  const activeCategories = useMemo(
    () => (categories.data ?? []).filter((row) => row.isActive),
    [categories.data]
  );
  const priceByCategory = useMemo(() => {
    const map = new Map<string, AdminTicketCategoryPrice>();
    for (const row of prices.data ?? []) map.set(row.categoryId, row);
    return map;
  }, [prices.data]);
  const pricedCategories = activeCategories.filter((row) => priceByCategory.has(row.id));
  const seatRows = seats.data ?? [];
  const seatById = useMemo(() => {
    const map = new Map<string, AdminTicketSeat>();
    for (const row of seatRows) map.set(row.id, row);
    return map;
  }, [seatRows]);
  const companionTargets = useMemo(() => {
    const taken = new Set(
      seatRows
        .map((row) => row.companionOfSeatId)
        .filter((id): id is string => Boolean(id))
    );
    return seatRows.filter((row) => row.seatRole === 'Accessible' && !taken.has(row.id));
  }, [seatRows]);

  const create = useMutation({
    mutationFn: (body: SeatFormValues) =>
      createAdminTicketSeat(eventId, {
        row: body.row.trim(),
        number: body.number.trim(),
        section: body.section?.trim() || undefined,
        categoryId: body.categoryId,
        gridColumn: body.gridColumn ?? undefined,
        seatRole: body.seatRole ?? 0,
        companionOfSeatId:
          (body.seatRole ?? 0) === 2 && body.companionOfSeatId
            ? body.companionOfSeatId
            : undefined,
      }),
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

  const saveStage = useMutation({
    mutationFn: () => {
      if (!event) throw new Error('Event is not loaded.');
      return updateAdminTicketEvent(eventId, {
        name: event.name,
        venue: event.venue,
        capacity: event.capacity,
        isActive: event.isActive,
        startAtUtc: event.startAtUtc,
        endAtUtc: event.endAtUtc,
        stageEdge,
      });
    },
    onSuccess: async () => {
      notify.success(t('products.ticketEvents.stageSaved'));
      await queryClient.invalidateQueries({ queryKey: adminTicketEventQueryKeys.all });
    },
    onError: (error) => {
      notify.apiError(error, {
        logContext: 'TicketSeatEditorPage.stage',
        fallbackKey: 'products.ticketEvents.saveError',
      });
    },
  });

  const savePrice = useMutation({
    mutationFn: (body: SetTicketCategoryPriceBody) => setAdminTicketCategoryPrice(eventId, body),
    onSuccess: async () => {
      notify.success(t('products.ticketEvents.priceSaved'));
      await queryClient.invalidateQueries({ queryKey: adminTicketEventQueryKeys.prices(eventId) });
    },
    onError: (error) => {
      notify.apiError(error, {
        logContext: 'TicketSeatEditorPage.price',
        fallbackKey: 'products.ticketEvents.priceError',
      });
    },
  });

  const columns: ColumnsType<AdminTicketSeat> = [
    { title: t('products.ticketEvents.section'), dataIndex: 'section', render: (value) => value || '—' },
    { title: t('products.ticketEvents.row'), dataIndex: 'row' },
    { title: t('products.ticketEvents.number'), dataIndex: 'number' },
    {
      title: t('products.ticketEvents.category'),
      dataIndex: 'categoryName',
      render: (value: string | null | undefined, record) => {
        const target = record.companionOfSeatId
          ? seatById.get(record.companionOfSeatId)
          : undefined;
        const pairing = target
          ? t('products.ticketEvents.companionOfLabel', { row: target.row, number: target.number })
          : null;
        return (
          <span>
            <span>{value || '—'}</span>
            {pairing ? <span> {pairing}</span> : null}
          </span>
        );
      },
    },
    {
      title: t('products.ticketEvents.gridColumn'),
      dataIndex: 'gridColumn',
      render: (value: number | null | undefined) => (value == null ? '—' : String(value)),
    },
    {
      title: t('products.ticketEvents.seatRole'),
      dataIndex: 'seatRole',
      render: (value: string | null | undefined) =>
        value && ROLE_KEYS[value] ? t(ROLE_KEYS[value]) : value || '—',
    },
    {
      title: t('products.ticketRedemptions.colStatus'),
      dataIndex: 'status',
      render: (value: string) => (STATUS_KEYS[value] ? t(STATUS_KEYS[value]) : value),
    },
  ];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <AdminPageHeader title={t('products.ticketEvents.seatsTitle')} />
      <Space wrap>
        <Select
          aria-label={t('products.ticketEvents.stageEdge')}
          value={stageEdge}
          disabled={!canManage}
          style={{ minWidth: 180 }}
          onChange={(value) => setStageEdge(value)}
          options={[
            { value: 'top', label: t('products.ticketEvents.stageTop') },
            { value: 'bottom', label: t('products.ticketEvents.stageBottom') },
          ]}
        />
        {canManage ? (
          <Button
            aria-label={`${t('products.ticketEvents.save')} ${t('products.ticketEvents.stageEdge')}`}
            loading={saveStage.isPending}
            disabled={!event}
            onClick={() => saveStage.mutate()}
          >
            {t('products.ticketEvents.save')}
          </Button>
        ) : null}
      </Space>
      <Typography.Title level={5}>{t('products.ticketEvents.tabCategories')}</Typography.Title>
      {activeCategories.map((category) => (
        <CategoryPriceRow
          key={category.id}
          category={category}
          existing={priceByCategory.get(category.id)}
          disabled={!canManage}
          saving={savePrice.isPending}
          onSave={(body) => savePrice.mutate(body)}
        />
      ))}
      <Typography.Title level={5}>{t('products.ticketEvents.newSeat')}</Typography.Title>
      <Form<SeatFormValues>
        form={form}
        layout="inline"
        initialValues={{ seatRole: 0 }}
        disabled={!canManage}
        onFinish={(values) => create.mutate(values)}
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
        <Form.Item name="categoryId" label={t('products.ticketEvents.category')}>
          <Select
            aria-label={t('products.ticketEvents.category')}
            allowClear
            style={{ minWidth: 160 }}
            options={pricedCategories.map((category) => ({
              value: category.id,
              label: category.name,
            }))}
          />
        </Form.Item>
        <Form.Item name="gridColumn" label={t('products.ticketEvents.gridColumn')}>
          <InputNumber min={1} precision={0} aria-label={t('products.ticketEvents.gridColumn')} />
        </Form.Item>
        <Form.Item name="seatRole" label={t('products.ticketEvents.seatRole')}>
          <Select
            aria-label={t('products.ticketEvents.seatRole')}
            style={{ minWidth: 160 }}
            options={[
              { value: 0, label: t('products.ticketEvents.roleStandard') },
              { value: 1, label: t('products.ticketEvents.roleAccessible') },
              { value: 2, label: t('products.ticketEvents.roleCompanion') },
            ]}
          />
        </Form.Item>
        <Form.Item name="companionOfSeatId" label={t('products.ticketEvents.companionOf')}>
          <Select
            aria-label={t('products.ticketEvents.companionOf')}
            allowClear
            placeholder={t('products.ticketEvents.companionOfNone')}
            style={{ minWidth: 200 }}
            options={companionTargets.map((seat) => ({
              value: seat.id,
              label: t('products.ticketEvents.companionTargetOption', {
                row: seat.row,
                number: seat.number,
              }),
            }))}
          />
        </Form.Item>
        {canManage ? (
          <Form.Item>
            <Button type="primary" htmlType="submit" loading={create.isPending}>
              {t('products.ticketEvents.addSeat')}
            </Button>
          </Form.Item>
        ) : null}
      </Form>
      {seats.isError || events.isError ? (
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
