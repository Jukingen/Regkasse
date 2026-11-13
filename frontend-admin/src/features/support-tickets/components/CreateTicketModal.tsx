'use client';

import { Button, Form, Input, Modal, Select } from 'antd';

import { useI18n } from '@/i18n';

export type CreateTicketFormValues = {
  category: 'Technical' | 'Billing' | 'License' | 'FeatureRequest' | 'General';
  priority: 'Low' | 'Medium' | 'High' | 'Urgent';
  title: string;
  message: string;
};

type CreateTicketModalProps = {
  open: boolean;
  loading?: boolean;
  onCancel: () => void;
  onSubmit: (values: CreateTicketFormValues) => void;
};

export function CreateTicketModal({ open, loading, onCancel, onSubmit }: CreateTicketModalProps) {
  const { t } = useI18n();
  const [form] = Form.useForm<CreateTicketFormValues>();

  return (
    <Modal
      title={t('support.tickets.newTicket')}
      open={open}
      onCancel={onCancel}
      footer={null}
      destroyOnHidden
      afterOpenChange={(isOpen) => {
        if (!isOpen) form.resetFields();
      }}
    >
      <Form
        form={form}
        layout="vertical"
        onFinish={onSubmit}
        initialValues={{ category: 'Technical', priority: 'Medium' }}
      >
        <Form.Item
          name="category"
          label={t('support.tickets.category')}
          rules={[{ required: true }]}
        >
          <Select
            options={[
              { value: 'Technical', label: t('support.tickets.categoryTechnical') },
              { value: 'Billing', label: t('support.tickets.categoryBilling') },
              { value: 'License', label: t('support.tickets.categoryLicense') },
              { value: 'FeatureRequest', label: t('support.tickets.categoryFeature') },
              { value: 'General', label: t('support.tickets.categoryGeneral') },
            ]}
          />
        </Form.Item>
        <Form.Item
          name="priority"
          label={t('support.tickets.priority')}
          rules={[{ required: true }]}
        >
          <Select
            options={[
              { value: 'Low', label: t('support.tickets.priorityLow') },
              { value: 'Medium', label: t('support.tickets.priorityMedium') },
              { value: 'High', label: t('support.tickets.priorityHigh') },
              { value: 'Urgent', label: t('support.tickets.priorityUrgent') },
            ]}
          />
        </Form.Item>
        <Form.Item
          name="title"
          htmlFor="support-ticket-subject"
          label={t('support.tickets.subject')}
          rules={[{ required: true, min: 3 }]}
        >
          <Input id="support-ticket-subject" />
        </Form.Item>
        <Form.Item
          name="message"
          htmlFor="support-ticket-message"
          label={t('support.tickets.message')}
          rules={[{ required: true, min: 10 }]}
        >
          <Input.TextArea id="support-ticket-message" rows={4} />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={loading}>
          {t('support.tickets.newTicket')}
        </Button>
      </Form>
    </Modal>
  );
}
