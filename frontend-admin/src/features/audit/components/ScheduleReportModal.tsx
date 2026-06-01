'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useState } from 'react';
import { Modal, Form, Input, Select } from 'antd';

import type { AuditLogListParams } from '@/features/audit-logs/hooks/useAuditLogSearchParams';
import { scheduleAuditReport, type AuditExportFormat } from '@/features/audit/api/auditAdmin';
import { useI18n } from '@/i18n';

type Props = {
    open: boolean;
    params: AuditLogListParams;
    onClose: () => void;
    onScheduled?: () => void;
};

export function ScheduleReportModal({ open, params, onClose, onScheduled }: Props) {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const [form] = Form.useForm();
    const [submitting, setSubmitting] = useState(false);

    const handleOk = async () => {
        try {
            const values = await form.validateFields();
            setSubmitting(true);
            await scheduleAuditReport({
                name: values.name as string,
                schedule: values.schedule as string,
                recipients: (values.recipients as string).split(/[,;\s]+/).filter(Boolean),
                format: values.format as AuditExportFormat,
                params,
            });
            message.success(t('common.auditLogs.scheduleSuccess'));
            form.resetFields();
            onScheduled?.();
            onClose();
        } catch (e) {
            if (e && typeof e === 'object' && 'errorFields' in e) return;
            message.error(e instanceof Error ? e.message : t('common.auditLogs.scheduleFailed'));
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <Modal
            open={open}
            title={t('common.auditLogs.scheduleModalTitle')}
            onCancel={onClose}
            onOk={handleOk}
            confirmLoading={submitting}
            destroyOnHidden
            okText={t('common.auditLogs.scheduleModalSubmit')}
        >
            <Form
                form={form}
                layout="vertical"
                initialValues={{ format: 'csv', schedule: '0 9 * * 1' }}
            >
                <Form.Item
                    name="name"
                    label={t('common.auditLogs.scheduleName')}
                    rules={[{ required: true, message: t('common.validation.fieldRequired') }]}
                >
                    <Input placeholder={t('common.auditLogs.scheduleNamePlaceholder')} />
                </Form.Item>
                <Form.Item
                    name="schedule"
                    label={t('common.auditLogs.scheduleCron')}
                    rules={[{ required: true, message: t('common.validation.fieldRequired') }]}
                    extra={t('common.auditLogs.scheduleCronHint')}
                >
                    <Input placeholder="0 9 * * 1" />
                </Form.Item>
                <Form.Item
                    name="recipients"
                    label={t('common.auditLogs.scheduleRecipients')}
                    rules={[{ required: true, message: t('common.validation.fieldRequired') }]}
                >
                    <Input placeholder="admin@regkasse.at" />
                </Form.Item>
                <Form.Item name="format" label={t('common.auditLogs.scheduleFormat')}>
                    <Select
                        options={[
                            { value: 'csv', label: 'CSV' },
                            { value: 'json', label: 'JSON' },
                            { value: 'excel', label: t('common.auditLogs.exportExcel') },
                        ]}
                    />
                </Form.Item>
            </Form>
        </Modal>
    );
}
