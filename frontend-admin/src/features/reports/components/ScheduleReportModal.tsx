'use client';

import React from 'react';
import { Form, Input, Modal, Select } from 'antd';
import { useI18n } from '@/i18n/I18nProvider';
import { scheduleOperationalReport, type AdminReportType } from '@/features/reporting/compliance/complianceReportsApi';

export type ScheduleReportModalProps = {
    open: boolean;
    reportType: AdminReportType;
    filters: {
        startDate?: string;
        endDate?: string;
        businessDate?: string;
        cashRegisterId?: string;
    };
    onClose: () => void;
    onScheduled?: () => void;
};

export function ScheduleReportModal(props: ScheduleReportModalProps) {
    if (!props.open) {
        return null;
    }
    return <ScheduleReportModalContent {...props} />;
}

function ScheduleReportModalContent({
    open,
    reportType,
    filters,
    onClose,
    onScheduled,
}: ScheduleReportModalProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<{ schedule: string; recipients: string; format: string }>();

    return (
        <Modal
            open={open}
            title={t('reporting.compliance.schedule.title')}
            onCancel={onClose}
            onOk={() => {
                void form.validateFields().then(async (values) => {
                    const recipients = values.recipients
                        .split(/[,;\s]+/)
                        .map((e) => e.trim())
                        .filter(Boolean);
                    await scheduleOperationalReport({
                        reportType,
                        schedule: values.schedule,
                        recipients,
                        format: values.format,
                        filters: {
                            startDate: filters.startDate,
                            endDate: filters.endDate,
                            businessDate: filters.businessDate,
                            cashRegisterId: filters.cashRegisterId,
                        },
                    });
                    onScheduled?.();
                    onClose();
                    form.resetFields();
                });
            }}
            destroyOnHidden
        >
            <Form
                form={form}
                layout="vertical"
                initialValues={{ schedule: '0 8 * * *', format: 'pdf' }}
            >
                <Form.Item
                    name="schedule"
                    label={t('reporting.compliance.schedule.cron')}
                    rules={[{ required: true, message: t('common.validation.fieldRequired') }]}
                    extra={t('reporting.compliance.schedule.cronHint')}
                >
                    <Input placeholder="0 8 * * *" />
                </Form.Item>
                <Form.Item
                    name="recipients"
                    label={t('reporting.compliance.schedule.recipients')}
                    rules={[{ required: true, message: t('common.validation.fieldRequired') }]}
                >
                    <Input.TextArea rows={2} placeholder="manager@example.com" />
                </Form.Item>
                <Form.Item name="format" label={t('reporting.compliance.schedule.format')} rules={[{ required: true, message: t('common.validation.fieldRequired') }]}>
                    <Select
                        options={[
                            { value: 'pdf', label: 'PDF' },
                            { value: 'csv', label: 'CSV' },
                            { value: 'json', label: 'JSON' },
                        ]}
                    />
                </Form.Item>
            </Form>
        </Modal>
    );
}
