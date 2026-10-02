'use client';

import { Card, Col, Row, Statistic, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import React from 'react';

import { formatCurrency, formatNumber } from '@/i18n/formatting';

export type TagesberichtPaymentRow = {
  methodKey: string;
  displayLabel?: string;
  rowCount: number;
  totalAmount: number;
};

export type TagesberichtTaxRow = {
  taxBucketKey: string;
  taxAmount: number;
};

type Props = {
  grossLabel: string;
  taxLabel: string;
  paymentsLabel: string;
  grossAmount: number;
  taxTotalAmount: number;
  paymentRows: TagesberichtPaymentRow[];
  taxRows: TagesberichtTaxRow[];
  formatLocale: string;
  methodColumn: string;
  linesColumn: string;
  sumColumn: string;
  taxBucketColumn: string;
  taxAmountColumn: string;
};

export function TagesberichtSummaryCards({
  grossLabel,
  taxLabel,
  paymentsLabel,
  grossAmount,
  taxTotalAmount,
  paymentRows,
  taxRows,
  formatLocale,
  methodColumn,
  linesColumn,
  sumColumn,
  taxBucketColumn,
  taxAmountColumn,
}: Props) {
  const pmCols: ColumnsType<TagesberichtPaymentRow> = [
    {
      title: methodColumn,
      dataIndex: 'methodKey',
      render: (v: string, row) => row.displayLabel || v,
    },
    { title: linesColumn, dataIndex: 'rowCount' },
    {
      title: sumColumn,
      dataIndex: 'totalAmount',
      render: (v: number) => formatCurrency(v ?? 0, formatLocale),
    },
  ];
  const taxCols: ColumnsType<TagesberichtTaxRow> = [
    { title: taxBucketColumn, dataIndex: 'taxBucketKey' },
    {
      title: taxAmountColumn,
      dataIndex: 'taxAmount',
      render: (v: number) =>
        formatNumber(v ?? 0, formatLocale, {
          minimumFractionDigits: 2,
          maximumFractionDigits: 4,
        }),
    },
  ];

  return (
    <Row gutter={[16, 16]} data-testid="tagesbericht-summary-cards">
      <Col xs={24} md={8}>
        <Card>
          <Statistic
            title={grossLabel}
            value={grossAmount}
            precision={2}
            suffix="EUR"
          />
        </Card>
      </Col>
      <Col xs={24} md={8}>
        <Card title={taxLabel}>
          <Statistic value={taxTotalAmount} precision={2} suffix="EUR" />
          {taxRows.length ? (
            <Table
              style={{ marginTop: 12 }}
              rowKey="taxBucketKey"
              size="small"
              pagination={false}
              dataSource={taxRows}
              columns={taxCols}
            />
          ) : (
            <Typography.Text type="secondary">—</Typography.Text>
          )}
        </Card>
      </Col>
      <Col xs={24} md={8}>
        <Card title={paymentsLabel}>
          <Table
            rowKey="methodKey"
            size="small"
            pagination={false}
            dataSource={paymentRows}
            columns={pmCols}
          />
        </Card>
      </Col>
    </Row>
  );
}
