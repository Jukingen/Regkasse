'use client';

import { DownloadOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Col, DatePicker, Row, Space, Table, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { useMemo, useState } from 'react';

import { CashRegisterSelector } from '@/components/CashRegisterSelector';
import { CashRegisterStatusContextAlert } from '@/features/cash-registers/components/CashRegisterStatusContextAlert';
import { toEnhancedCashRegister } from '@/features/cash-registers/api/cashRegisters';
import { isDecommissionedRegister } from '@/features/cash-registers/utils/registerStatus';
import { fetchAdminPaymentsList } from '@/features/payments/api/adminPaymentsListQuery';
import { getReceiptListForensics } from '@/features/receipts/api/forensics-client';
import { formatRksvSpecialReceiptKindDisplay } from '@/features/receipts/utils/formatRksvSpecialReceiptKind';
import { fetchAdminShiftOverview } from '@/features/shifts/api/shiftsOverview';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, useI18n } from '@/i18n';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';
import { cashRegisterDetailPath } from '@/shared/cashRegisterRoutes';
import { downloadCsvText, rowsToCsv } from '@/shared/utils/csv';

const { RangePicker } = DatePicker;

export type CashRegisterReportType = 'revenue' | 'transactions' | 'shifts' | 'specialReceipts';

export type CashRegisterReportsWorkspaceProps = {
  initialRegisterId?: string;
};

export function CashRegisterReportsWorkspace({
  initialRegisterId,
}: CashRegisterReportsWorkspaceProps) {
  const { t, formatLocale } = useI18n();
  const [registerId, setRegisterId] = useState<string | undefined>(initialRegisterId);
  const [reportType, setReportType] = useState<CashRegisterReportType>('revenue');
  const [dateRange, setDateRange] = useState<[Dayjs, Dayjs]>([
    dayjs().startOf('month'),
    dayjs().endOf('month'),
  ]);

  const selection = useCashRegisterSelection({
    value: registerId,
    onChange: (next) => setRegisterId(next),
    includeDecommissioned: true,
    autoSelect: !initialRegisterId,
    persistSelection: false,
  });

  const selectedRegister = selection.selectedRegister;
  const enhanced = selectedRegister ? toEnhancedCashRegister(selectedRegister) : null;
  const decommissioned = selectedRegister
    ? isDecommissionedRegister(selectedRegister.status)
    : false;

  const startDate = dateRange[0].format('YYYY-MM-DD');
  const endDate = dateRange[1].format('YYYY-MM-DD');
  const fromUtc = dateRange[0].startOf('day').toISOString();
  const toUtc = dateRange[1].endOf('day').toISOString();

  const paymentsQuery = useQuery({
    queryKey: ['admin', 'cash-register-reports', registerId, 'payments', startDate, endDate],
    queryFn: () =>
      fetchAdminPaymentsList({
        cashRegisterId: registerId,
        startDate,
        endDate,
        page: 1,
        pageNumber: 1,
        pageSize: 100,
        sortBy: 'CreatedAt',
        sortDirection: 'desc',
        includeTotalCount: true,
      }),
    enabled: Boolean(registerId),
    staleTime: 15_000,
  });

  const shiftsQuery = useQuery({
    queryKey: ['admin', 'cash-register-reports', registerId, 'shifts', fromUtc, toUtc],
    queryFn: () =>
      fetchAdminShiftOverview({
        cashRegisterId: registerId,
        fromUtc,
        toUtc,
        limit: 100,
      }),
    enabled: Boolean(registerId),
    staleTime: 15_000,
  });

  const receiptsQuery = useQuery({
    queryKey: ['admin', 'cash-register-reports', registerId, 'receipts', startDate, endDate],
    queryFn: () =>
      getReceiptListForensics({
        page: 1,
        pageSize: 100,
        sort: 'issuedAt:desc',
        cashRegisterId: registerId,
        issuedFrom: fromUtc,
        issuedTo: toUtc,
      }),
    enabled: Boolean(registerId) && reportType === 'specialReceipts',
    staleTime: 15_000,
  });

  const paymentRows = paymentsQuery.data?.items ?? [];
  const shiftRows = useMemo(
    () => [...(shiftsQuery.data?.activeShifts ?? []), ...(shiftsQuery.data?.shiftHistory ?? [])],
    [shiftsQuery.data]
  );
  const specialRows = useMemo(
    () =>
      (receiptsQuery.data?.items ?? []).filter((row) => Boolean(row.rksvSpecialReceiptKind?.trim())),
    [receiptsQuery.data?.items]
  );

  const revenueTotal = useMemo(() => {
    const fromShifts = shiftRows.reduce(
      (sum, row) => sum + (Number.isFinite(row.totalSales) ? row.totalSales : 0),
      0
    );
    if (fromShifts > 0) {
      return fromShifts;
    }
    return paymentRows.reduce(
      (sum, row) => sum + (typeof row.totalAmount === 'number' ? row.totalAmount : 0),
      0
    );
  }, [paymentRows, shiftRows]);

  const exportCurrentReport = () => {
    if (reportType === 'revenue' || reportType === 'transactions') {
      downloadCsvText(
        rowsToCsv([
          [
            t('receipts.table.colReceipt'),
            t('cashRegisters.detail.createdAt'),
            t('payments.table.colMethod'),
            t('cashRegisters.detail.statRevenue'),
            t('cashRegisters.detail.status'),
          ],
          ...paymentRows.map((row) => [
            row.receiptNumber ?? '',
            row.createdAt ?? '',
            row.method ?? '',
            row.totalAmount ?? '',
            row.status ?? '',
          ]),
        ]),
        `kasse-${registerId}-${reportType}.csv`
      );
      return;
    }
    if (reportType === 'shifts') {
      downloadCsvText(
        rowsToCsv([
          [
            t('cashRegisters.detail.historyShifts'),
            t('cashRegisters.detail.createdAt'),
            t('cashRegisters.detail.statRevenue'),
            t('cashRegisters.detail.status'),
          ],
          ...shiftRows.map((row) => [
            row.cashierName,
            row.startedAt,
            row.totalSales,
            row.status,
          ]),
        ]),
        `kasse-${registerId}-shifts.csv`
      );
      return;
    }
    downloadCsvText(
      rowsToCsv([
        [
          t('receipts.table.colReceipt'),
          t('cashRegisters.detail.createdAt'),
          t('cashRegisters.actions.specialReceipts'),
          t('cashRegisters.detail.statRevenue'),
        ],
        ...specialRows.map((row) => [
          row.receiptNumber,
          row.issuedAt,
          row.rksvSpecialReceiptKind ?? '',
          row.grandTotal,
        ]),
      ]),
      `kasse-${registerId}-sonderbelege.csv`
    );
  };

  const paymentColumns: ColumnsType<(typeof paymentRows)[number]> = [
    {
      title: t('receipts.table.colReceipt'),
      dataIndex: 'receiptNumber',
      render: (value: string | null | undefined) => value?.trim() || FORMAT_EMPTY_DISPLAY,
    },
    {
      title: t('cashRegisters.detail.createdAt'),
      dataIndex: 'createdAt',
      render: (value: string | undefined) =>
        value ? formatDateTime(value, formatLocale) : FORMAT_EMPTY_DISPLAY,
    },
    {
      title: t('payments.table.colMethod'),
      dataIndex: 'method',
      render: (value: string | null | undefined) => value?.trim() || FORMAT_EMPTY_DISPLAY,
    },
    {
      title: t('cashRegisters.detail.statRevenue'),
      dataIndex: 'totalAmount',
      render: (value: number | undefined) => formatCurrency(value ?? 0, formatLocale),
    },
  ];

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Card title={t('cashRegisters.reports.filterRegister')}>
        <Space wrap align="start">
          <CashRegisterSelector
            value={registerId}
            onChange={setRegisterId}
            includeDecommissioned
            required={false}
            autoSelect={!initialRegisterId}
            persistSelection={false}
            allowClear
            showFormItem={false}
            style={{ minWidth: 280 }}
          />
          <RangePicker
            value={dateRange}
            format={DAYJS_DATE_FORMAT}
            onChange={(range) => {
              if (range?.[0] && range[1]) {
                setDateRange([range[0], range[1]]);
              }
            }}
          />
        </Space>
      </Card>

      {enhanced ? <CashRegisterStatusContextAlert register={enhanced} /> : null}

      {!registerId ? (
        <Alert type="info" showIcon title={t('cashRegisters.reports.selectRegister')} />
      ) : (
        <>
          <Row gutter={[16, 16]}>
            {(
              [
                'revenue',
                'transactions',
                'shifts',
                'specialReceipts',
              ] as const satisfies readonly CashRegisterReportType[]
            ).map((type) => (
              <Col xs={24} sm={12} lg={6} key={type}>
                <Card
                  hoverable
                  onClick={() => setReportType(type)}
                  style={
                    reportType === type ? { borderColor: '#1677ff', boxShadow: '0 0 0 1px #1677ff' } : undefined
                  }
                >
                  <Typography.Text strong>{t(`cashRegisters.reports.types.${type}`)}</Typography.Text>
                </Card>
              </Col>
            ))}
          </Row>

          <Card
            title={t(`cashRegisters.reports.types.${reportType}`)}
            extra={
              <Space wrap>
                <Button icon={<DownloadOutlined />} onClick={exportCurrentReport}>
                  {t('cashRegisters.actions.exportReport')}
                </Button>
                <Button onClick={exportCurrentReport}>
                  {t('cashRegisters.actions.exportData')}
                </Button>
              </Space>
            }
          >
            {reportType === 'revenue' ? (
              <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                <Typography.Title level={4} style={{ margin: 0 }}>
                  {formatCurrency(revenueTotal, formatLocale)}
                </Typography.Title>
                <Table
                  size="small"
                  rowKey={(row) => row.id ?? row.transactionId ?? String(row.createdAt)}
                  loading={paymentsQuery.isLoading || shiftsQuery.isLoading}
                  columns={paymentColumns}
                  dataSource={paymentRows}
                  pagination={false}
                />
              </Space>
            ) : null}
            {reportType === 'transactions' ? (
              <Table
                size="small"
                rowKey={(row) => row.id ?? row.transactionId ?? String(row.createdAt)}
                loading={paymentsQuery.isLoading}
                columns={paymentColumns}
                dataSource={paymentRows}
                pagination={false}
              />
            ) : null}
            {reportType === 'shifts' ? (
              <Table
                size="small"
                rowKey={(row) => row.id}
                loading={shiftsQuery.isLoading}
                dataSource={shiftRows}
                pagination={false}
                columns={[
                  {
                    title: t('cashRegisters.detail.currentCashier'),
                    dataIndex: 'cashierName',
                  },
                  {
                    title: t('cashRegisters.detail.createdAt'),
                    dataIndex: 'startedAt',
                    render: (value: string) => formatDateTime(value, formatLocale),
                  },
                  {
                    title: t('cashRegisters.detail.statRevenue'),
                    dataIndex: 'totalSales',
                    render: (value: number) => formatCurrency(value ?? 0, formatLocale),
                  },
                  {
                    title: t('cashRegisters.detail.status'),
                    dataIndex: 'status',
                  },
                ]}
              />
            ) : null}
            {reportType === 'specialReceipts' ? (
              <Table
                size="small"
                rowKey={(row) => row.receiptId}
                loading={receiptsQuery.isLoading}
                dataSource={specialRows}
                pagination={false}
                columns={[
                  { title: t('receipts.table.colReceipt'), dataIndex: 'receiptNumber' },
                  {
                    title: t('cashRegisters.detail.createdAt'),
                    dataIndex: 'issuedAt',
                    render: (value: string) => formatDateTime(value, formatLocale),
                  },
                  {
                    title: t('cashRegisters.actions.specialReceipts'),
                    dataIndex: 'rksvSpecialReceiptKind',
                    render: (value: string | null | undefined) =>
                      formatRksvSpecialReceiptKindDisplay(t, value),
                  },
                  {
                    title: t('cashRegisters.detail.statRevenue'),
                    dataIndex: 'grandTotal',
                    render: (value: number) => formatCurrency(value ?? 0, formatLocale),
                  },
                ]}
              />
            ) : null}
          </Card>

          <Space wrap>
            <DisabledActionButton
              decommissioned={decommissioned}
              label={t('cashRegisters.actions.openRegister')}
              href={registerId ? cashRegisterDetailPath(registerId) : undefined}
            />
            <DisabledActionButton
              decommissioned={decommissioned}
              label={t('cashRegisters.actions.openShift')}
              href={registerId ? cashRegisterDetailPath(registerId) : undefined}
            />
            <DisabledActionButton
              decommissioned={decommissioned}
              label={t('cashRegisters.actions.createSpecialReceipt')}
              href={RKSV_SONDERBELEGE_PATH}
              disabledReason={t('cashRegisters.actions.decommissionedCannotCreateSpecialReceipts')}
            />
          </Space>
        </>
      )}
    </Space>
  );
}

function DisabledActionButton({
  decommissioned,
  label,
  href,
  disabledReason,
}: {
  decommissioned: boolean;
  label: string;
  href?: string;
  disabledReason?: string;
}) {
  const { t } = useI18n();
  const button = (
    <span>
      <Button disabled={decommissioned} href={decommissioned ? undefined : href}>
        {label}
      </Button>
    </span>
  );
  if (!decommissioned) {
    return button;
  }
  return (
    <Tooltip title={disabledReason ?? t('cashRegisters.actions.decommissionedCannotOpen')}>
      {button}
    </Tooltip>
  );
}
