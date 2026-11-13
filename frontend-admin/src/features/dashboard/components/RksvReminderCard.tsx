'use client';

import { CheckCircleOutlined, ReloadOutlined, WarningOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Empty, Skeleton, Space, Tag } from 'antd';

import { useRksvReminderOverview } from '@/features/rksv-operations/hooks/useRksvReminderOverview';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDateTime } from '@/i18n/formatting';
import { RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';

export function RksvReminderCard() {
  const { t } = useI18n();
  const { data, isLoading, error, refetch, isError, isFetching } = useRksvReminderOverview();
  const canOpenSonderbelege = useCanAccessPath(RKSV_SONDERBELEGE_PATH);
  const cardTitle = t('dashboard.rksvReminder.card_title');

  if (isLoading) {
    return (
      <Card
        title={cardTitle}
        extra={<Button icon={<ReloadOutlined />} onClick={() => void refetch()} />}
        variant="borderless"
        style={{ marginBottom: 24 }}
      >
        <Space orientation="vertical" style={{ width: '100%' }}>
          <Skeleton active paragraph={{ rows: 3 }} />
        </Space>
      </Card>
    );
  }

  const hasLoadError = isError || Boolean(error);
  if (hasLoadError) {
    return (
      <Card
        title={cardTitle}
        extra={
          <Button icon={<ReloadOutlined />} loading={isFetching} onClick={() => void refetch()}>
            {t('dashboard.rksvReminderCard.reload')}
          </Button>
        }
        variant="borderless"
        style={{ marginBottom: 24 }}
      >
        <Alert
          type="error"
          title={t('dashboard.rksvReminderCard.load_error_title')}
          description={t('dashboard.rksvReminderCard.load_error_description')}
          showIcon
        />
      </Card>
    );
  }

  if (!data || data.totalRegisters === 0) {
    return (
      <Card
        title={cardTitle}
        extra={
          <Button icon={<ReloadOutlined />} loading={isFetching} onClick={() => void refetch()} />
        }
        variant="borderless"
        style={{ marginBottom: 24 }}
      >
        <Empty
          image={Empty.PRESENTED_IMAGE_SIMPLE}
          description={t('dashboard.rksvReminderCard.empty_description')}
        />
      </Card>
    );
  }

  const hasIssues =
    data.missingStartbeleg > 0 ||
    data.missingMonatsbeleg > 0 ||
    data.overdueMonatsbeleg > 0 ||
    data.missingJahresbeleg > 0;

  const lastUpdatedLabel = data.lastUpdated
    ? t('dashboard.rksvReminderCard.last_updated', {
        time: formatDateTime(data.lastUpdated, ''),
      })
    : t('dashboard.rksvReminderCard.last_updated', {
        time: t('dashboard.rksvReminderCard.last_updated_unknown'),
      });

  return (
    <Card
      title={
        <Space>
          <span>{cardTitle}</span>
          {hasIssues ? (
            <Tag color="red" icon={<WarningOutlined />}>
              {t('dashboard.rksvReminderCard.action_required')}
            </Tag>
          ) : (
            <Tag color="green" icon={<CheckCircleOutlined />}>
              {t('dashboard.rksvReminderCard.all_ok')}
            </Tag>
          )}
        </Space>
      }
      extra={
        <Space>
          <Button
            size="small"
            icon={<ReloadOutlined />}
            loading={isFetching}
            onClick={() => void refetch()}
          />
          {canOpenSonderbelege ? (
            <Button size="small" type="link" href={RKSV_SONDERBELEGE_PATH}>
              {t('dashboard.rksvReminderCard.manage')}
            </Button>
          ) : null}
        </Space>
      }
      variant="borderless"
      style={{ marginBottom: 24 }}
    >
      <Space orientation="vertical" style={{ width: '100%' }} size="middle">
        {data.missingStartbeleg > 0 ? (
          <Alert
            type="warning"
            showIcon
            title={t('dashboard.rksvReminderCard.startbeleg_missing_title')}
            description={t('dashboard.rksvReminderCard.startbeleg_missing_description', {
              missing: data.missingStartbeleg,
              total: data.totalRegisters,
            })}
          />
        ) : null}

        {data.overdueMonatsbeleg > 0 ? (
          <Alert
            type="error"
            showIcon
            title={t('dashboard.rksvReminderCard.monatsbeleg_overdue_title')}
            description={t('dashboard.rksvReminderCard.monatsbeleg_overdue_description', {
              count: data.overdueMonatsbeleg,
            })}
          />
        ) : null}

        {data.missingMonatsbeleg > 0 && data.overdueMonatsbeleg === 0 ? (
          <Alert
            type="info"
            showIcon
            title={t('dashboard.rksvReminderCard.monatsbeleg_pending_title')}
            description={t('dashboard.rksvReminderCard.monatsbeleg_pending_description', {
              count: data.missingMonatsbeleg,
            })}
          />
        ) : null}

        {data.missingJahresbeleg > 0 ? (
          <Alert
            type="warning"
            showIcon
            title={t('dashboard.rksvReminderCard.jahresbeleg_pending_title')}
            description={t('dashboard.rksvReminderCard.jahresbeleg_pending_description', {
              count: data.missingJahresbeleg,
            })}
          />
        ) : null}

        {!hasIssues ? (
          <Alert
            type="success"
            showIcon
            title={t('dashboard.rksvReminderCard.all_current_title')}
            description={t('dashboard.rksvReminderCard.all_current_description')}
          />
        ) : null}

        <div style={{ fontSize: 12, color: '#8c8c8c', textAlign: 'right' }}>{lastUpdatedLabel}</div>
      </Space>
    </Card>
  );
}
