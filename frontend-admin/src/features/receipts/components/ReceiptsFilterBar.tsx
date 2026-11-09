'use client';

import { ClearOutlined, SearchOutlined } from '@ant-design/icons';
import { Button, DatePicker, Form, Input, Select, Space } from 'antd';
import dayjs from 'dayjs';
import { useMemo } from 'react';

import type { ReceiptListParams } from '@/features/receipts/types/receipts';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';
import { useI18n } from '@/i18n';

const { RangePicker } = DatePicker;

interface ReceiptsFilterBarProps {
  initialValues: Partial<ReceiptListParams>;
  onFilterChange: (values: Partial<ReceiptListParams>) => void;
  onReset: () => void;
  loading: boolean;
}

interface FilterFormValues {
  receiptNumber?: string;
  cashRegisterId?: string;
  cashierId?: string;
  dateRange?: [dayjs.Dayjs, dayjs.Dayjs] | null;
}

/**
 * Filter bar for the receipts list.
 * Purely presentational — does not touch URL or queries directly.
 */
export default function ReceiptsFilterBar({
  initialValues,
  onFilterChange,
  onReset,
  loading,
}: ReceiptsFilterBarProps) {
  const { t } = useI18n();
  const [form] = Form.useForm<FilterFormValues>();
  const { registers, isLoading: registersLoading } = useCashRegisterSelection({
    autoSelect: false,
    autoSelectSingle: false,
    persistSelection: false,
  });
  const cashRegisterOptions = useMemo(
    () =>
      registers
        .filter((register) => register.isActive !== false)
        .map((register) => ({
          value: register.id,
          label: `${register.registerNumber} — ${register.location ?? ''}`.trim(),
        })),
    [registers]
  );

  const toParams = (values: FilterFormValues): Partial<ReceiptListParams> => ({
    receiptNumber: values.receiptNumber || undefined,
    cashRegisterId: values.cashRegisterId || undefined,
    cashierId: values.cashierId || undefined,
    issuedFrom: values.dateRange?.[0]?.toISOString(),
    issuedTo: values.dateRange?.[1]?.toISOString(),
  });

  const handleFinish = (values: FilterFormValues) => {
    onFilterChange(toParams(values));
  };

  const handleValuesChange = (
    changed: Partial<FilterFormValues>,
    all: FilterFormValues
  ) => {
    if (!Object.prototype.hasOwnProperty.call(changed, 'cashRegisterId')) return;
    onFilterChange(toParams(all));
  };

  const handleReset = () => {
    form.resetFields();
    onReset();
  };

  // Map ReceiptListParams back to form field values for initial state
  const formInitial: FilterFormValues = {
    receiptNumber: initialValues.receiptNumber,
    cashRegisterId: initialValues.cashRegisterId,
    cashierId: initialValues.cashierId,
    dateRange:
      initialValues.issuedFrom && initialValues.issuedTo
        ? [dayjs(initialValues.issuedFrom), dayjs(initialValues.issuedTo)]
        : undefined,
  };

  return (
    <Form
      form={form}
      layout="inline"
      initialValues={formInitial}
      onFinish={handleFinish}
      onValuesChange={handleValuesChange}
      style={{ marginBottom: 16, flexWrap: 'wrap', gap: 8 }}
    >
      <Form.Item name="receiptNumber">
        <Input
          placeholder={t('receipts.filters.placeholderReceiptNumber')}
          prefix={<SearchOutlined />}
          allowClear
          style={{ width: 180 }}
        />
      </Form.Item>

      <Form.Item name="cashRegisterId" label={t('receipts.filters.labelCashRegister')}>
        <Select
          allowClear
          showSearch
          optionFilterProp="label"
          loading={registersLoading}
          placeholder={t('receipts.filters.placeholderCashRegister')}
          options={cashRegisterOptions}
          style={{ width: 240 }}
        />
      </Form.Item>

      <Form.Item name="cashierId">
        <Input
          placeholder={t('receipts.filters.placeholderCashier')}
          allowClear
          style={{ width: 140 }}
        />
      </Form.Item>

      <Form.Item name="dateRange">
        <RangePicker
          format="DD.MM.YYYY"
          placeholder={[t('receipts.filters.dateFrom'), t('receipts.filters.dateTo')]}
        />
      </Form.Item>

      <Form.Item>
        <Space>
          <Button type="primary" htmlType="submit" icon={<SearchOutlined />} loading={loading}>
            {t('receipts.filters.search')}
          </Button>
          <Button onClick={handleReset} icon={<ClearOutlined />}>
            {t('receipts.filters.reset')}
          </Button>
        </Space>
      </Form.Item>
    </Form>
  );
}
