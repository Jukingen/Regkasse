'use client';

/**
 * Floating FA feedback widget: submit + track status of own items.
 */
import {
  BugOutlined,
  CommentOutlined,
  FormOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Button,
  Drawer,
  Empty,
  Form,
  Input,
  List,
  Rate,
  Select,
  Space,
  Switch,
  Tabs,
  Tag,
  Typography,
} from 'antd';
import { usePathname } from 'next/navigation';
import { useMemo, useState, type ReactNode } from 'react';

import {
  type AdminFeedbackCategory,
  type AdminFeedbackDto,
  type AdminFeedbackStatus,
  createAdminFeedback,
  fetchMyAdminFeedback,
} from '@/api/manual/adminFeedback';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { useDateFormatter } from '@/lib/hooks/useDateFormatter';

import styles from './FeedbackWidget.module.css';

const CATEGORIES: AdminFeedbackCategory[] = [
  'EaseOfUse',
  'Performance',
  'FeatureRequest',
  'Bug',
];

const STATUS_COLOR: Record<string, string> = {
  UnderReview: 'processing',
  InProgress: 'blue',
  Implemented: 'success',
  Declined: 'default',
  Duplicate: 'warning',
};

type FormValues = {
  category: AdminFeedbackCategory;
  title: string;
  message: string;
  rating?: number;
  includePage: boolean;
};

function statusTag(status: string, t: (key: string) => string) {
  return (
    <Tag color={STATUS_COLOR[status] ?? 'default'}>
      {t(`feedback.statuses.${status}` as 'feedback.statuses.UnderReview')}
    </Tag>
  );
}

export function FeedbackWidget() {
  const { t } = useI18n();
  const notify = useNotify();
  const pathname = usePathname();
  const { formatDateTime } = useDateFormatter();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm<FormValues>();
  const category = Form.useWatch('category', form);

  const showRating = category === 'EaseOfUse' || category === 'Performance';

  const mineQuery = useQuery({
    queryKey: ['admin', 'feedback', 'mine'],
    queryFn: () => fetchMyAdminFeedback({ limit: 30 }),
    enabled: open,
  });

  const submitMutation = useMutation({
    mutationFn: createAdminFeedback,
    onSuccess: async () => {
      notify.success(t('feedback.form.success'));
      form.resetFields();
      form.setFieldsValue({ includePage: true, category: 'FeatureRequest' });
      await queryClient.invalidateQueries({ queryKey: ['admin', 'feedback', 'mine'] });
    },
    onError: () => {
      notify.error(t('feedback.form.error'));
    },
  });

  const categoryOptions = useMemo(
    () =>
      CATEGORIES.map((c) => ({
        value: c,
        label: t(`feedback.categories.${c}` as 'feedback.categories.Bug'),
      })),
    [t],
  );

  const onSubmit = async (values: FormValues) => {
    await submitMutation.mutateAsync({
      category: values.category,
      title: values.title.trim(),
      message: values.message.trim(),
      rating: showRating ? values.rating : undefined,
      pagePath: values.includePage ? pathname : undefined,
    });
  };

  return (
    <>
      <Button
        type="primary"
        shape="round"
        size="large"
        className={styles.fab}
        icon={<CommentOutlined />}
        aria-label={t('feedback.fabAria')}
        onClick={() => setOpen(true)}
      >
        {t('feedback.fabLabel')}
      </Button>

      <Drawer
        title={t('feedback.drawerTitle')}
        open={open}
        onClose={() => setOpen(false)}
        width={420}
        destroyOnHidden
      >
        <Typography.Paragraph type="secondary">{t('feedback.drawerSubtitle')}</Typography.Paragraph>

        <Tabs
          items={[
            {
              key: 'submit',
              label: t('feedback.tabs.submit'),
              children: (
                <Form<FormValues>
                  form={form}
                  layout="vertical"
                  initialValues={{ category: 'FeatureRequest', includePage: true }}
                  onFinish={onSubmit}
                >
                  <Form.Item
                    name="category"
                    label={t('feedback.form.category')}
                    rules={[{ required: true, message: t('feedback.form.categoryRequired') }]}
                  >
                    <Select options={categoryOptions} />
                  </Form.Item>
                  <Form.Item
                    name="title"
                    label={t('feedback.form.title')}
                    rules={[
                      { required: true, message: t('feedback.form.titleRequired') },
                      { min: 3 },
                    ]}
                  >
                    <Input maxLength={200} showCount />
                  </Form.Item>
                  <Form.Item
                    name="message"
                    label={t('feedback.form.message')}
                    rules={[
                      { required: true, message: t('feedback.form.messageRequired') },
                      { min: 10 },
                    ]}
                  >
                    <Input.TextArea rows={5} maxLength={4000} showCount />
                  </Form.Item>
                  {showRating ? (
                    <Form.Item
                      name="rating"
                      label={t('feedback.form.rating')}
                      extra={t('feedback.form.ratingHelp')}
                    >
                      <Rate />
                    </Form.Item>
                  ) : null}
                  <Form.Item
                    name="includePage"
                    label={t('feedback.form.includePage')}
                    valuePropName="checked"
                  >
                    <Switch />
                  </Form.Item>
                  <Button type="primary" htmlType="submit" loading={submitMutation.isPending} block>
                    {t('feedback.form.submit')}
                  </Button>
                </Form>
              ),
            },
            {
              key: 'mine',
              label: t('feedback.tabs.mine'),
              children: (
                <MyFeedbackList
                  loading={mineQuery.isLoading}
                  error={mineQuery.isError}
                  items={mineQuery.data?.items ?? []}
                  formatDateTime={formatDateTime}
                  t={t}
                  statusTag={statusTag}
                />
              ),
            },
          ]}
        />
      </Drawer>
    </>
  );
}

function MyFeedbackList({
  loading,
  error,
  items,
  formatDateTime,
  t,
  statusTag: renderStatus,
}: {
  loading: boolean;
  error: boolean;
  items: AdminFeedbackDto[];
  formatDateTime: (value: string | Date | null | undefined) => string;
  t: (key: string) => string;
  statusTag: (status: string, t: (key: string) => string) => ReactNode;
}) {
  if (error) {
    return <Typography.Text type="danger">{t('feedback.mine.loadError')}</Typography.Text>;
  }
  if (!loading && items.length === 0) {
    return <Empty description={t('feedback.mine.empty')} />;
  }

  return (
    <List
      loading={loading}
      dataSource={items}
      renderItem={(item) => (
        <List.Item>
          <List.Item.Meta
            avatar={categoryIcon(item.category)}
            title={
              <Space wrap>
                <span>{item.title}</span>
                {renderStatus(item.status, t)}
              </Space>
            }
            description={
              <Space direction="vertical" size={4} style={{ width: '100%' }}>
                <Typography.Text type="secondary">
                  {t(`feedback.categories.${item.category}` as 'feedback.categories.Bug')}
                  {item.rating ? ` · ★ ${item.rating}` : ''}
                  {item.pagePath ? ` · ${item.pagePath}` : ''}
                </Typography.Text>
                <Typography.Paragraph ellipsis={{ rows: 2 }} style={{ marginBottom: 0 }}>
                  {item.message}
                </Typography.Paragraph>
                {item.reviewerNote ? (
                  <Typography.Text>
                    <strong>{t('feedback.mine.reviewerNote')}:</strong> {item.reviewerNote}
                  </Typography.Text>
                ) : null}
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {t('feedback.mine.updated')}: {formatDateTime(item.updatedAtUtc)}
                </Typography.Text>
              </Space>
            }
          />
        </List.Item>
      )}
    />
  );
}

function categoryIcon(category: string) {
  switch (category as AdminFeedbackCategory | AdminFeedbackStatus) {
    case 'Bug':
      return <BugOutlined />;
    case 'Performance':
      return <ThunderboltOutlined />;
    case 'EaseOfUse':
      return <FormOutlined />;
    default:
      return <CommentOutlined />;
  }
}
