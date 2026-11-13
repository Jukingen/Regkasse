'use client';

import { useQuery } from '@tanstack/react-query';
import { Card, Empty, Spin, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import React, { useMemo } from 'react';

import {
  getPriceHistoryReport,
  priceHistoryReportQueryKey,
} from '@/features/tax/api/priceHistory';
import { useI18n } from '@/i18n';
import { useDateFormatter } from '@/lib/hooks/useDateFormatter';

export type PriceHistoryCardProps = {
  productId: string;
};

type PriceHistoryRow = {
  key: string;
  version: string;
  price: number;
  taxRate: number | null;
  taxGroupName?: string | null;
  validFrom: string;
  validTo?: string | null;
  isCurrent: boolean;
  isRksvCompliant: boolean;
};

export function PriceHistoryCard({ productId }: PriceHistoryCardProps) {
  const { t } = useI18n();
  const { formatDate } = useDateFormatter();

  const { data, isLoading, isError } = useQuery({
    queryKey: priceHistoryReportQueryKey(productId),
    queryFn: () => getPriceHistoryReport(productId),
    enabled: !!productId,
  });

  const rows = useMemo<PriceHistoryRow[]>(() => {
    if (!data) return [];

    const rateByFrom = new Map<string, number>();
    for (const h of data.history) {
      rateByFrom.set(new Date(h.effectiveFrom).toISOString(), h.newTaxRate);
    }

    return data.versions.map((v) => {
      const fromKey = new Date(v.validFrom).toISOString();
      const matchedHistory = data.history.find(
        (h) => Math.abs(new Date(h.effectiveFrom).getTime() - new Date(v.validFrom).getTime()) < 2000
      );
      return {
        key: v.id,
        version: v.version || String(data.catalogVersion),
        price: v.price,
        taxRate: matchedHistory?.newTaxRate ?? rateByFrom.get(fromKey) ?? null,
        taxGroupName: v.taxGroupName,
        validFrom: v.validFrom,
        validTo: v.validTo,
        isCurrent: v.isCurrent,
        isRksvCompliant: matchedHistory?.isRksvCompliant ?? true,
      };
    });
  }, [data]);

  const columns: ColumnsType<PriceHistoryRow> = [
    {
      title: t('products.priceHistory.version'),
      dataIndex: 'version',
      key: 'version',
      width: 90,
      render: (version: string, row) => (
        <span>
          {version}
          {row.isCurrent ? (
            <Tag color="blue" style={{ marginInlineStart: 6, marginInlineEnd: 0 }}>
              {t('products.priceHistory.current')}
            </Tag>
          ) : null}
        </span>
      ),
    },
    {
      title: t('products.priceHistory.price'),
      dataIndex: 'price',
      key: 'price',
      render: (price: number) => `€${Number(price).toFixed(2)}`,
    },
    {
      title: t('products.priceHistory.tax'),
      key: 'tax',
      render: (_, row) =>
        row.taxRate == null
          ? row.taxGroupName || '—'
          : `${Number(row.taxRate).toFixed(2)}%${row.taxGroupName ? ` (${row.taxGroupName})` : ''}`,
    },
    {
      title: t('products.priceHistory.validFrom'),
      dataIndex: 'validFrom',
      key: 'validFrom',
      render: (date: string) => formatDate(date),
    },
    {
      title: t('products.priceHistory.validTo'),
      dataIndex: 'validTo',
      key: 'validTo',
      render: (date: string | null | undefined) =>
        date ? formatDate(date) : t('products.priceHistory.active'),
    },
    {
      title: t('products.priceHistory.rksvCompliant'),
      dataIndex: 'isRksvCompliant',
      key: 'isRksvCompliant',
      render: (ok: boolean) => (
        <Tag color={ok ? 'green' : 'red'} style={{ marginInlineEnd: 0 }}>
          {ok ? t('products.priceHistory.compliantYes') : t('products.priceHistory.compliantNo')}
        </Tag>
      ),
    },
  ];

  return (
    <Card
      size="small"
      title={t('products.priceHistory.cardTitle')}
      style={{ marginTop: 16, marginBottom: 8 }}
    >
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {t('products.priceHistory.description')}
      </Typography.Paragraph>
      {isLoading ? (
        <div style={{ textAlign: 'center', padding: 16 }}>
          <Spin />
        </div>
      ) : isError ? (
        <Typography.Text type="danger">{t('products.priceHistory.loadFailed')}</Typography.Text>
      ) : rows.length === 0 ? (
        <Empty description={t('products.priceHistory.empty')} image={Empty.PRESENTED_IMAGE_SIMPLE} />
      ) : (
        <Table<PriceHistoryRow>
          size="small"
          rowKey="key"
          pagination={false}
          dataSource={rows}
          columns={columns}
          scroll={{ x: 720 }}
        />
      )}
    </Card>
  );
}
