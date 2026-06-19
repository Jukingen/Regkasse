'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Back-office inventory: stock, movements, reorder suggestions, stocktake draft, transfers — permissions and confirm dialogs.
 */
import React, { useCallback, useMemo, useState } from 'react';

import { Modal, Alert, Button, Card, DatePicker, Drawer, Flex, Input, InputNumber, Select, Space, Spin, Table, Tabs, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import axios from 'axios';
import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import { useQueryClient } from '@tanstack/react-query';
import { InboxOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n/I18nProvider';
import { formatCurrency } from '@/i18n/formatting';
import {
  getGetApiInventoryHistoryQueryKey,
  getGetApiInventoryQueryKey,
  getGetApiInventoryReorderSuggestionsQueryKey,
  getGetApiInventoryTransactionsIdQueryKey,
  postApiInventoryIdAdjust,
  useDeleteApiInventoryId,
  useGetApiInventory,
  useGetApiInventoryHistory,
  useGetApiInventoryReorderSuggestions,
  useGetApiInventoryTransactionsId,
  usePostApiInventoryIdAdjust,
  usePostApiInventoryIdRestock,
  usePostApiInventoryIdTransfer,
} from '@/api/generated/inventory/inventory';
import type { InventoryHistoryRowDto } from '@/api/generated/model';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { isAdminInventoryNavEnabled } from '@/shared/config/adminInventoryNavUi';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';

dayjs.extend(utc);

type EnrichedInventoryRow = {
  id: string;
  productId: string;
  productName?: string;
  productCategory?: string;
  currentStock: number;
  minStockLevel: number;
  maxStockLevel?: number | null;
  reorderPoint?: number | null;
  unitCost: number;
  lastRestocked?: string | null;
  notes?: string | null;
  isActive?: boolean;
};

function txLabel(t: (k: string) => string, v: number | undefined): string {
  switch (v) {
    case 1:
      return t('adminShell.inventory.txOpen');
    case 2:
      return t('adminShell.inventory.txClose');
    case 3:
      return t('adminShell.inventory.txRestock');
    case 4:
      return t('adminShell.inventory.txSale');
    case 5:
      return t('adminShell.inventory.txAdjustment');
    case 6:
      return t('adminShell.inventory.txLoss');
    case 7:
      return t('adminShell.inventory.txReturn');
    case 8:
      return t('adminShell.inventory.txTransfer');
    default:
      return `${t('adminShell.inventory.txType')} ${v ?? '—'}`;
  }
}

function extractApiError(err: unknown, translate: (key: string) => string): string {
  if (axios.isAxiosError(err)) {
    const d = err.response?.data;
    if (d && typeof d === 'object' && 'message' in d && typeof (d as { message: unknown }).message === 'string') {
      return (d as { message: string }).message;
    }
  }
  return translate('adminShell.inventory.errorRequestFailed');
}

export default function InventoryOperationsPage() {
  const { message, modal } = useAntdApp();

  const { t, formatLocale } = useI18n();
  const qc = useQueryClient();
  const { hasPermission } = usePermissions();

  const inventoryNavEnabled = isAdminInventoryNavEnabled();
  const canView = hasPermission(PERMISSIONS.INVENTORY_VIEW);
  const canManage = hasPermission(PERMISSIONS.INVENTORY_MANAGE);
  const canAdjust = hasPermission(PERMISSIONS.INVENTORY_ADJUST);
  const canDelete = hasPermission(PERMISSIONS.INVENTORY_DELETE);

  const [tab, setTab] = useState('overview');
  const [search, setSearch] = useState('');
  const [histPage, setHistPage] = useState(1);
  const [histPageSize] = useState(25);
  const [histInvFilter, setHistInvFilter] = useState<string | undefined>();
  const [histRange, setHistRange] = useState<[dayjs.Dayjs | null, dayjs.Dayjs | null]>([null, null]);

  const [drawerInvId, setDrawerInvId] = useState<string | null>(null);

  const [restockOpen, setRestockOpen] = useState(false);
  const [adjustOpen, setAdjustOpen] = useState(false);
  const [transferOpen, setTransferOpen] = useState(false);
  const [activeRow, setActiveRow] = useState<EnrichedInventoryRow | null>(null);

  const [restockQty, setRestockQty] = useState(1);
  const [restockCost, setRestockCost] = useState<number | null>(null);
  const [restockNotes, setRestockNotes] = useState('');

  const [adjDelta, setAdjDelta] = useState(0);
  const [adjReason, setAdjReason] = useState('');

  const [xferTarget, setXferTarget] = useState<string | undefined>();
  const [xferQty, setXferQty] = useState(1);
  const [xferNotes, setXferNotes] = useState('');

  const [countDraft, setCountDraft] = useState<Record<string, number | null>>({});

  const invQuery = useGetApiInventory({ query: { enabled: canView && inventoryNavEnabled } });
  const rows = useMemo(() => (invQuery.data ?? []) as EnrichedInventoryRow[], [invQuery.data]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter(
      (r) =>
        (r.productName ?? '').toLowerCase().includes(q) ||
        (r.productCategory ?? '').toLowerCase().includes(q),
    );
  }, [rows, search]);

  const histParams = useMemo(
    () => ({
      page: histPage,
      pageSize: histPageSize,
      inventoryId: histInvFilter,
      fromUtc: histRange[0]?.startOf('day').utc().toISOString(),
      toUtc: histRange[1]?.endOf('day').utc().toISOString(),
    }),
    [histPage, histPageSize, histInvFilter, histRange],
  );

  const histQ = useGetApiInventoryHistory(histParams, {
    query: { enabled: canView && inventoryNavEnabled && tab === 'movements' },
  });

  const reorderQ = useGetApiInventoryReorderSuggestions({
    query: { enabled: canView && inventoryNavEnabled && tab === 'reorder' },
  });

  const txQ = useGetApiInventoryTransactionsId(drawerInvId ?? '', {
    query: { enabled: !!drawerInvId && inventoryNavEnabled },
  });

  const invalidateAll = useCallback(async () => {
    await qc.invalidateQueries({ queryKey: getGetApiInventoryQueryKey() });
    await qc.invalidateQueries({ queryKey: getGetApiInventoryReorderSuggestionsQueryKey() });
    await qc.invalidateQueries({ queryKey: getGetApiInventoryHistoryQueryKey(histParams) });
    await qc.invalidateQueries({ queryKey: ['/api/Inventory/history'] });
  }, [qc, histParams]);

  const restockMut = usePostApiInventoryIdRestock({
    mutation: {
      onSuccess: async () => {
        message.success(t('adminShell.inventory.success'));
        setRestockOpen(false);
        await invalidateAll();
      },
      onError: (e) => message.error(extractApiError(e, t)),
    },
  });

  const adjustMut = usePostApiInventoryIdAdjust({
    mutation: {
      onSuccess: async () => {
        message.success(t('adminShell.inventory.success'));
        setAdjustOpen(false);
        await invalidateAll();
      },
      onError: (e) => message.error(extractApiError(e, t)),
    },
  });

  const transferMut = usePostApiInventoryIdTransfer({
    mutation: {
      onSuccess: async () => {
        message.success(t('adminShell.inventory.success'));
        setTransferOpen(false);
        await invalidateAll();
      },
      onError: (e) => message.error(extractApiError(e, t)),
    },
  });

  const deleteMut = useDeleteApiInventoryId({
    mutation: {
      onSuccess: async () => {
        message.success(t('adminShell.inventory.success'));
        await invalidateAll();
      },
      onError: (e) => message.error(extractApiError(e, t)),
    },
  });

  const formatMoney = useCallback((v: number) => formatCurrency(v, formatLocale), [formatLocale]);

  const openRestock = (r: EnrichedInventoryRow) => {
    if (!canManage) {
      message.warning(t('adminShell.inventory.permissionDenied'));
      return;
    }
    setActiveRow(r);
    setRestockQty(1);
    setRestockCost(null);
    setRestockNotes('');
    setRestockOpen(true);
  };

  const openAdjust = (r: EnrichedInventoryRow) => {
    if (!canAdjust) {
      message.warning(t('adminShell.inventory.permissionDenied'));
      return;
    }
    setActiveRow(r);
    setAdjDelta(0);
    setAdjReason('');
    setAdjustOpen(true);
  };

  const openTransfer = (r: EnrichedInventoryRow) => {
    if (!canManage) {
      message.warning(t('adminShell.inventory.permissionDenied'));
      return;
    }
    setActiveRow(r);
    setXferTarget(undefined);
    setXferQty(1);
    setXferNotes('');
    setTransferOpen(true);
  };

  const confirmDelete = (r: EnrichedInventoryRow) => {
    if (!canDelete) {
      message.warning(t('adminShell.inventory.permissionDenied'));
      return;
    }
    modal.confirm({
      title: t('adminShell.inventory.actionDelete'),
      content: t('adminShell.inventory.confirmDelete'),
      okButtonProps: { danger: true },
      onOk: () => deleteMut.mutateAsync({ id: r.id }),
    });
  };

  const invOptions = useMemo(
    () =>
      rows
        .filter((r) => r.id !== activeRow?.id)
        .map((r) => ({
          value: r.id,
          label: `${r.productName ?? r.id} (${r.currentStock})`,
        })),
    [rows, activeRow?.id],
  );

  const overviewCols: ColumnsType<EnrichedInventoryRow> = [
    {
      title: t('adminShell.inventory.colProduct'),
      dataIndex: 'productName',
      render: (_, r) => (
        <Space orientation="vertical" size={0}>
          <Typography.Text strong>{r.productName}</Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {r.productCategory}
          </Typography.Text>
        </Space>
      ),
    },
    { title: t('adminShell.inventory.colStock'), dataIndex: 'currentStock', width: 100 },
    {
      title: t('adminShell.inventory.colMin'),
      dataIndex: 'minStockLevel',
      width: 80,
      render: (v: number, r) =>
        r.currentStock <= r.minStockLevel ? (
          <Tag color="red">
            {v} {t('adminShell.inventory.lowTag')}
          </Tag>
        ) : (
          v
        ),
    },
    {
      title: t('adminShell.inventory.colCost'),
      dataIndex: 'unitCost',
      render: (v: number) => formatMoney(Number(v ?? 0)),
    },
    {
      title: t('adminShell.inventory.colActions'),
      key: 'actions',
      width: 280,
      render: (_, r) => (
        <Space wrap size="small">
          <Button size="small" onClick={() => setDrawerInvId(r.id)} disabled={!canView}>
            {t('adminShell.inventory.actionHistory')}
          </Button>
          {canManage ? (
            <Button size="small" type="primary" onClick={() => openRestock(r)}>
              {t('adminShell.inventory.actionRestock')}
            </Button>
          ) : null}
          {canAdjust ? (
            <Button size="small" onClick={() => openAdjust(r)}>
              {t('adminShell.inventory.actionAdjust')}
            </Button>
          ) : null}
          {canManage ? (
            <Button size="small" onClick={() => openTransfer(r)}>
              {t('adminShell.inventory.actionTransfer')}
            </Button>
          ) : null}
          {canDelete ? (
            <Button size="small" danger onClick={() => confirmDelete(r)}>
              {t('adminShell.inventory.actionDelete')}
            </Button>
          ) : null}
        </Space>
      ),
    },
  ];

  const histCols: ColumnsType<InventoryHistoryRowDto> = [
    {
      title: t('adminShell.inventory.colProduct'),
      render: (_, row) => (
        <Space orientation="vertical" size={0}>
          <span>{row.productName}</span>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {row.productCategory}
          </Typography.Text>
        </Space>
      ),
    },
    {
      title: t('adminShell.inventory.txType'),
      dataIndex: 'transactionType',
      width: 120,
      render: (v: number) => txLabel(t, v),
    },
    { title: t('adminShell.inventory.txQty'), dataIndex: 'quantity', width: 90 },
    {
      title: t('adminShell.inventory.txDate'),
      dataIndex: 'transactionDateUtc',
      render: (v: string | undefined) =>
        v ? dayjs(v).utc().format('DD.MM.YYYY HH:mm') : '—',
    },
    { title: t('adminShell.inventory.notes'), dataIndex: 'notes', ellipsis: true },
  ];

  const finalizeCount = () => {
    if (!canAdjust) {
      message.warning(t('adminShell.inventory.permissionDenied'));
      return;
    }
    const tasks: { id: string; adj: number; booked: number; counted: number }[] = [];
    for (const r of rows) {
      const counted = countDraft[r.id];
      if (counted === undefined || counted === null) continue;
      const adj = counted - r.currentStock;
      if (adj !== 0) tasks.push({ id: r.id, adj, booked: r.currentStock, counted });
    }
    if (tasks.length === 0) {
      message.info(t('adminShell.inventory.countNoVariance'));
      return;
    }
    modal.confirm({
      title: t('adminShell.inventory.finalizeCount'),
      content: t('adminShell.inventory.confirmFinalize'),
      onOk: async () => {
        for (const x of tasks) {
          const reason = `${t('adminShell.inventory.countReasonPrefix')}|booked=${x.booked}|counted=${x.counted}|delta=${x.adj}`;
          await postApiInventoryIdAdjust(x.id, { adjustment: x.adj, reason });
          await qc.invalidateQueries({ queryKey: getGetApiInventoryTransactionsIdQueryKey(x.id) });
        }
        setCountDraft({});
        await invalidateAll();
        message.success(t('adminShell.inventory.success'));
      },
    });
  };

  const reorderCols: ColumnsType<Record<string, unknown>> = [
    { title: t('adminShell.inventory.colProduct'), dataIndex: 'productName' },
    { title: t('adminShell.inventory.colStock'), dataIndex: 'currentStock' },
    { title: t('adminShell.inventory.colMin'), dataIndex: 'minStockLevel' },
    {
      title: t('adminShell.inventory.reorderSuggested'),
      dataIndex: 'suggestedOrderQuantity',
    },
  ];

  const countCols: ColumnsType<EnrichedInventoryRow> = [
    {
      title: t('adminShell.inventory.colProduct'),
      render: (_, r) => (
        <Space orientation="vertical" size={0}>
          <Typography.Text strong>{r.productName}</Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {r.productCategory}
          </Typography.Text>
        </Space>
      ),
    },
    { title: t('adminShell.inventory.colStock'), dataIndex: 'currentStock', width: 100 },
    {
      title: t('adminShell.inventory.counted'),
      key: 'counted',
      render: (_, r) => (
        <InputNumber
          min={0}
          value={countDraft[r.id] ?? null}
          onChange={(v) => setCountDraft((d) => ({ ...d, [r.id]: v }))}
        />
      ),
    },
    {
      title: t('adminShell.inventory.variance'),
      key: 'var',
      render: (_, r) => {
        const c = countDraft[r.id];
        if (c === undefined || c === null) return '—';
        return c - r.currentStock;
      },
    },
  ];

  if (!inventoryNavEnabled) {
    return (
      <div style={{ paddingBottom: 24 }}>
        <AdminPageHeader
          title={
            <Space>
              <InboxOutlined />
              {t('adminShell.inventory.pageTitle')}
            </Space>
          }
          breadcrumbs={[adminOverviewCrumb(t), { title: t('adminShell.inventory.pageTitle'), href: '/inventory' }]}
        />
        <Alert type="info" showIcon title={t('adminShell.inventory.featureDisabledTitle')} description={t('adminShell.inventory.featureDisabledBody')} style={{ marginTop: 16 }} />
      </div>
    );
  }

  if (!canView) {
    return (
      <Alert type="error" title={t('adminShell.inventory.permissionDenied')} style={{ margin: 24 }} />
    );
  }

  return (
    <div style={{ paddingBottom: 24 }}>
      <AdminPageHeader
        title={
          <Space>
            <InboxOutlined />
            {t('adminShell.inventory.pageTitle')}
          </Space>
        }
        breadcrumbs={[adminOverviewCrumb(t), { title: t('adminShell.inventory.pageTitle'), href: '/inventory' }]}
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('adminShell.inventory.pageIntro')}
        </Typography.Paragraph>
      </AdminPageHeader>

      <Tabs
        activeKey={tab}
        onChange={setTab}
        items={[
          {
            key: 'overview',
            label: t('adminShell.inventory.tabOverview'),
            children: (
              <Card size="small">
                <Flex gap={12} wrap="wrap" style={{ marginBottom: 12 }}>
                  <Input.Search
                    allowClear
                    placeholder={t('adminShell.inventory.searchPlaceholder')}
                    onSearch={setSearch}
                    onChange={(e) => setSearch(e.target.value)}
                    style={{ maxWidth: 320 }}
                  />
                </Flex>
                <Spin spinning={invQuery.isLoading}>
                  <Table rowKey="id" columns={overviewCols} dataSource={filtered} pagination={{ pageSize: 15 }} />
                </Spin>
              </Card>
            ),
          },
          {
            key: 'movements',
            label: t('adminShell.inventory.tabMovements'),
            children: (
              <Card size="small" title={t('adminShell.inventory.historyTitle')}>
                <Space wrap style={{ marginBottom: 12 }}>
                  <Select
                    allowClear
                    placeholder={t('adminShell.inventory.filterInventory')}
                    style={{ minWidth: 260 }}
                    options={rows.map((r) => ({
                      value: r.id,
                      label: r.productName ?? r.id,
                    }))}
                    value={histInvFilter}
                    onChange={(v) => setHistInvFilter(v)}
                  />
                  <DatePicker.RangePicker format={DAYJS_DATE_FORMAT}
                    value={histRange[0] && histRange[1] ? [histRange[0], histRange[1]] : null}
                    onChange={(d) => setHistRange(d ? [d[0], d[1]] : [null, null])}
                  />
                  <Button onClick={() => setHistPage(1)}>{t('adminShell.inventory.historyFilterApply')}</Button>
                </Space>
                <Spin spinning={histQ.isLoading}>
                  <Table
                    rowKey={(r) => String(r.transactionId)}
                    columns={histCols}
                    dataSource={histQ.data?.items ?? []}
                    pagination={{
                      current: histPage,
                      pageSize: histPageSize,
                      total: histQ.data?.totalCount ?? 0,
                      onChange: (p) => setHistPage(p),
                    }}
                  />
                </Spin>
              </Card>
            ),
          },
          {
            key: 'reorder',
            label: t('adminShell.inventory.tabReorder'),
            children: (
              <Card size="small" title={t('adminShell.inventory.reorderTitle')}>
                <Spin spinning={reorderQ.isLoading}>
                  <Table
                    rowKey="inventoryId"
                    columns={reorderCols}
                    dataSource={(reorderQ.data ?? []) as Record<string, unknown>[]}
                  />
                </Spin>
              </Card>
            ),
          },
          {
            key: 'count',
            label: t('adminShell.inventory.tabCount'),
            children: (
              <Card size="small" title={t('adminShell.inventory.countTitle')}>
                <Alert type="info" showIcon title={t('adminShell.inventory.countDraftHint')} style={{ marginBottom: 12 }} />
                <Spin spinning={invQuery.isLoading}>
                  <Table rowKey="id" columns={countCols} dataSource={filtered} pagination={false} />
                  <Button
                    type="primary"
                    style={{ marginTop: 16 }}
                    disabled={!canAdjust}
                    onClick={() => finalizeCount()}
                  >
                    {t('adminShell.inventory.finalizeCount')}
                  </Button>
                </Spin>
              </Card>
            ),
          },
        ]}
      />

      <Modal
        title={t('adminShell.inventory.modalRestockTitle')}
        open={restockOpen}
        onCancel={() => setRestockOpen(false)}
        onOk={() => {
          if (!activeRow) return;
          restockMut.mutate({
            id: activeRow.id,
            data: {
              quantity: restockQty,
              unitCost: restockCost ?? undefined,
              notes: restockNotes || undefined,
            },
          });
        }}
        confirmLoading={restockMut.isPending}
      >
        <Space orientation="vertical" style={{ width: '100%' }}>
          <Typography.Text>
            {activeRow?.productName} — {t('adminShell.inventory.colStock')}: {activeRow?.currentStock}
          </Typography.Text>
          <InputNumber min={1} value={restockQty} onChange={(v) => setRestockQty(Number(v) || 1)} />
          <InputNumber
            min={0}
            step={0.01}
            placeholder={t('adminShell.inventory.unitCost')}
            value={restockCost ?? undefined}
            onChange={(v) => setRestockCost(v === null ? null : Number(v))}
            style={{ width: '100%' }}
          />
          <Input placeholder={t('adminShell.inventory.notes')} value={restockNotes} onChange={(e) => setRestockNotes(e.target.value)} />
        </Space>
      </Modal>

      <Modal
        title={t('adminShell.inventory.modalAdjustTitle')}
        open={adjustOpen}
        onCancel={() => setAdjustOpen(false)}
        onOk={() => {
          if (!activeRow || !adjReason.trim()) {
            message.error(t('adminShell.inventory.reason'));
            return;
          }
          adjustMut.mutate({
            id: activeRow.id,
            data: { adjustment: adjDelta, reason: adjReason.trim() },
          });
        }}
        confirmLoading={adjustMut.isPending}
      >
        <Space orientation="vertical" style={{ width: '100%' }}>
          <Typography.Paragraph type="secondary">{t('adminShell.inventory.adjustmentHint')}</Typography.Paragraph>
          <InputNumber value={adjDelta} onChange={(v) => setAdjDelta(Number(v) || 0)} />
          <Input.TextArea rows={3} placeholder={t('adminShell.inventory.reason')} value={adjReason} onChange={(e) => setAdjReason(e.target.value)} />
        </Space>
      </Modal>

      <Modal
        title={t('adminShell.inventory.modalTransferTitle')}
        open={transferOpen}
        onCancel={() => setTransferOpen(false)}
        onOk={() => {
          if (!activeRow || !xferTarget) {
            message.error(t('adminShell.inventory.targetLine'));
            return;
          }
          transferMut.mutate({
            id: activeRow.id,
            data: {
              targetInventoryId: xferTarget,
              quantity: xferQty,
              notes: xferNotes || undefined,
            },
          });
        }}
        confirmLoading={transferMut.isPending}
      >
        <Space orientation="vertical" style={{ width: '100%' }}>
          <Typography.Text>{activeRow?.productName}</Typography.Text>
          <Select
            showSearch
            optionFilterProp="label"
            style={{ width: '100%' }}
            placeholder={t('adminShell.inventory.targetLine')}
            options={invOptions}
            value={xferTarget}
            onChange={setXferTarget}
          />
          <InputNumber min={1} value={xferQty} onChange={(v) => setXferQty(Number(v) || 1)} />
          <Input placeholder={t('adminShell.inventory.transferNotes')} value={xferNotes} onChange={(e) => setXferNotes(e.target.value)} />
        </Space>
      </Modal>

      <Drawer
        title={t('adminShell.inventory.drawerTxTitle')}
        open={!!drawerInvId}
        onClose={() => setDrawerInvId(null)}
        size={560}
      >
        <Spin spinning={txQ.isLoading}>
          <Table
            size="small"
            rowKey="id"
            columns={[
              {
                title: t('adminShell.inventory.txType'),
                dataIndex: 'transactionType',
                render: (v: number | undefined) => txLabel(t, v),
              },
              { title: t('adminShell.inventory.txQty'), dataIndex: 'quantity' },
              {
                title: t('adminShell.inventory.txDate'),
                dataIndex: 'transactionDate',
                render: (v: string) => dayjs(v).utc().format('DD.MM.YYYY HH:mm'),
              },
              { title: t('adminShell.inventory.notes'), dataIndex: 'notes', ellipsis: true },
            ]}
            dataSource={txQ.data ?? []}
            pagination={false}
          />
        </Spin>
      </Drawer>
    </div>
  );
}
