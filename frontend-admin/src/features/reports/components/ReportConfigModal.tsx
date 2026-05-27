'use client';

import React, { useEffect } from 'react';
import { DatePicker, Form, Modal, Select } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useI18n } from '@/i18n/I18nProvider';
import type { ReportCategoryId } from './ReportSelector';

export type ReportConfigValues = {
    businessDay: Dayjs;
    dateRange: [Dayjs, Dayjs];
    cashRegisterId?: string;
};

export type ReportConfigModalProps = {
    open: boolean;
    reportKey: ReportCategoryId;
    initial: ReportConfigValues;
    registerOptions: { value: string; label: string }[];
    onCancel: () => void;
    onApply: (values: ReportConfigValues) => void;
};

export function ReportConfigModal({
    open,
    reportKey,
    initial,
    registerOptions,
    onCancel,
    onApply,
}: ReportConfigModalProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<ReportConfigValues>();

    useEffect(() => {
        if (open) form.setFieldsValue(initial);
    }, [open, initial, form]);

    const usesBusinessDay = reportKey === 'reconciliation';

    return (
        <Modal
            open={open}
            title={t('reporting.compliance.config.title')}
            onCancel={onCancel}
            onOk={() => {
                void form.validateFields().then((values) => onApply(values));
            }}
            destroyOnClose
        >
            <Form form={form} layout="vertical">
                {usesBusinessDay ? (
                    <Form.Item
                        name="businessDay"
                        label={t('reporting.compliance.config.businessDay')}
                        rules={[{ required: true }]}
                    >
                        <DatePicker style={{ width: '100%' }} />
                    </Form.Item>
                ) : (
                    <Form.Item
                        name="dateRange"
                        label={t('reporting.compliance.config.dateRange')}
                        rules={[{ required: true }]}
                    >
                        <DatePicker.RangePicker style={{ width: '100%' }} />
                    </Form.Item>
                )}
                <Form.Item name="cashRegisterId" label={t('reporting.compliance.config.register')}>
                    <Select allowClear options={registerOptions} placeholder={t('reporting.compliance.config.allRegisters')} />
                </Form.Item>
            </Form>
        </Modal>
    );
}

export function defaultReportConfig(): ReportConfigValues {
    return {
        businessDay: dayjs(),
        dateRange: [dayjs().startOf('month'), dayjs().endOf('month')],
        cashRegisterId: undefined,
    };
}
