'use client';

import { Button, Card, DatePicker, Form, Space } from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { DownloadOutlined } from '@ant-design/icons';
import type { ReactNode } from 'react';

import { CashRegisterSelector } from '@/components/CashRegisterSelector';
import { StaffSelector } from '@/components/StaffSelector';
import { useI18n } from '@/i18n';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';

const { RangePicker } = DatePicker;

export type ReportFilterValues = {
    registerId?: string;
    dateRange: [Dayjs, Dayjs];
    staffId?: string;
};

export type ReportFiltersProps = {
    onGenerate: (values: ReportFilterValues) => void;
    /** Fires when any field changes (e.g. auto-selected register). Used by live-mode pages. */
    onValuesChange?: (values: Partial<ReportFilterValues>) => void;
    /** `submit` = explicit generate button; `live` = apply on every change. */
    mode?: 'submit' | 'live';
    showStaffFilter?: boolean;
    loading?: boolean;
    registerRequired?: boolean;
    registerAllowClear?: boolean;
    pickerMode?: 'date' | 'month' | 'year';
    /** Page-specific filters rendered inside the same form row. */
    extra?: ReactNode;
    /** Wrap in Card when set (consistent filter chrome across report pages). */
    cardTitle?: string;
    cardStyle?: React.CSSProperties;
    /** Show PDF / Excel export actions (requires callbacks). */
    showExport?: boolean;
    canExport?: boolean;
    exportLoading?: boolean;
    exportDisabled?: boolean;
    onExportPdf?: () => void;
    onExportExcel?: () => void;
    initialValues?: Partial<{
        registerId: string;
        dateRange: [Dayjs, Dayjs];
        staffId: string;
    }>;
};

const defaultDateRange = (): [Dayjs, Dayjs] => [dayjs().startOf('month'), dayjs().endOf('month')];

export function ReportFilters({
    onGenerate,
    onValuesChange,
    mode = 'submit',
    showStaffFilter = false,
    loading = false,
    registerRequired = true,
    registerAllowClear = false,
    pickerMode = 'date',
    extra,
    cardTitle,
    cardStyle,
    showExport = false,
    canExport = true,
    exportLoading = false,
    exportDisabled = false,
    onExportPdf,
    onExportExcel,
    initialValues,
}: ReportFiltersProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<ReportFilterValues>();
    const isLive = mode === 'live';

    const formNode = (
        <Form
            form={form}
            layout="inline"
            initialValues={{
                dateRange: initialValues?.dateRange ?? defaultDateRange(),
                registerId: initialValues?.registerId,
                staffId: initialValues?.staffId,
            }}
            onFinish={onGenerate}
            onValuesChange={(_, allValues) => {
                onValuesChange?.(allValues);
                if (isLive) {
                    const range = allValues.dateRange;
                    if (range?.[0] && range[1]) {
                        onGenerate({
                            registerId: allValues.registerId,
                            dateRange: range,
                            staffId: allValues.staffId,
                        });
                    }
                }
            }}
            style={{ marginBottom: cardTitle ? 0 : 16, flexWrap: 'wrap', gap: 8 }}
        >
            <Form.Item
                name="registerId"
                label={t('adminShell.reporting.register')}
                rules={
                    registerRequired
                        ? [{ required: true, message: t('adminShell.reporting.registerRequired') }]
                        : undefined
                }
            >
                <CashRegisterSelector
                    showFormItem={false}
                    required={registerRequired}
                    autoSelect={registerRequired}
                    persistSelection
                    allowClear={registerAllowClear}
                    placeholder={
                        registerAllowClear ? t('adminShell.reporting.registerAll') : undefined
                    }
                    style={{ minWidth: 200 }}
                />
            </Form.Item>

            <Form.Item
                name="dateRange"
                label={t('adminShell.reporting.dateRange')}
                rules={[{ required: true, message: t('adminShell.reporting.dateRangeRequired') }]}
            >
                <RangePicker
                    format={pickerMode === 'date' ? DAYJS_DATE_FORMAT : undefined}
                    picker={pickerMode}
                />
            </Form.Item>

            {showStaffFilter ? (
                <Form.Item name="staffId" label={t('adminShell.reporting.cashier')}>
                    <StaffSelector style={{ minWidth: 200 }} />
                </Form.Item>
            ) : null}

            {extra}

            {!isLive ? (
                <Form.Item>
                    <Space wrap>
                        <Button type="primary" htmlType="submit" loading={loading}>
                            {t('adminShell.reporting.generateReport')}
                        </Button>
                        {showExport && canExport ? (
                            <>
                                {onExportPdf ? (
                                    <Button
                                        disabled={exportDisabled}
                                        loading={exportLoading}
                                        onClick={onExportPdf}
                                    >
                                        {t('adminShell.reporting.exportPdf')}
                                    </Button>
                                ) : null}
                                {onExportExcel ? (
                                    <Button
                                        icon={<DownloadOutlined />}
                                        disabled={exportDisabled}
                                        loading={exportLoading}
                                        onClick={onExportExcel}
                                    >
                                        {t('adminShell.reporting.exportExcel')}
                                    </Button>
                                ) : null}
                            </>
                        ) : null}
                    </Space>
                </Form.Item>
            ) : null}
        </Form>
    );

    if (cardTitle) {
        return (
            <Card size="small" title={cardTitle} style={{ marginBottom: 12, ...cardStyle }}>
                {formNode}
            </Card>
        );
    }

    return formNode;
}
