'use client';

import { CheckCircleOutlined, WarningOutlined } from '@ant-design/icons';
import { Button, Progress, Skeleton, Space, Tag, Typography } from 'antd';
import Link from 'next/link';
import { useQueryClient } from '@tanstack/react-query';

import { postApiTagesabschlussDaily } from '@/api/generated/tagesabschluss/tagesabschluss';
import { useDailyClosingDashboardSummary } from '@/features/dashboard/api/dailyClosingDashboard';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import {
  resolveDailyClosingTodayTone,
  weekClosingPercent,
} from '@/features/dashboard/logic/dailyClosingWidgetStatus';
import { getTagesabschlussUserFacingError } from '@/features/tagesabschluss/tagesabschlussApiErrors';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import { PERMISSIONS } from '@/shared/auth/permissions';

type ShellProps = Pick<WidgetShellProps, 'title' | 'dragHandleProps'>;

export function DailyClosingWidget({ title, dragHandleProps }: ShellProps) {
  const { t, formatLocale } = useI18n();
  const { modal, message } = useAntdApp();
  const queryClient = useQueryClient();
  const { isAuthorized: canView } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.DAILY_CLOSING_VIEW,
  });
  const { isAuthorized: canExecute } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.DAILY_CLOSING_EXECUTE,
  });
  const { selectedRegisterId } = useCashRegisterSelection({
    autoSelect: true,
    persistSelection: true,
    enabled: canView,
  });
  const registerId = selectedRegisterId?.trim() || undefined;
  const query = useDailyClosingDashboardSummary(registerId, canView && Boolean(registerId));
  const data = query.data;
  const today = data?.today;
  const week = data?.week;
  const tone = today ? resolveDailyClosingTodayTone(today) : 'empty';
  const closedDays = week?.closedDays ?? 0;
  const totalDays = week?.totalDays ?? 7;
  const percent = weekClosingPercent(closedDays, totalDays);

  const handleCloseToday = () => {
    if (!canExecute || !registerId || !data?.requiresAttention) return;
    const emptyDay = (today?.transactionCount ?? 0) === 0;
    modal.confirm({
      title: t('dashboard.dailyClosing.closeNowConfirmTitle'),
      content: emptyDay
        ? t('tagesabschluss.emptyDayConfirm')
        : t('dashboard.dailyClosing.closeNowConfirm'),
      okText: t('dashboard.dailyClosing.closeNow'),
      cancelText: t('tagesabschluss.actions.modalCancel'),
      onOk: async () => {
        try {
          await postApiTagesabschlussDaily({ cashRegisterId: registerId });
          await queryClient.invalidateQueries({ queryKey: ['/api/admin/daily-closing'] });
          await query.refetch();
          message.success(t('tagesabschluss.messages.successDaily'));
        } catch (error) {
          message.error(
            getTagesabschlussUserFacingError(t, error, {
              logContext: 'DailyClosingWidget.closeToday',
              fallbackKey: 'tagesabschluss.errors.unknown',
            })
          );
          throw error;
        }
      },
    });
  };

  return (
    <WidgetShell
      title={title}
      dragHandleProps={dragHandleProps}
      onRefresh={() => void query.refetch()}
      refreshing={query.isFetching}
    >
      {!canView ? (
        <Typography.Text type="secondary">{t('dashboard.dailyClosing.noPermission')}</Typography.Text>
      ) : !registerId ? (
        <Typography.Text type="secondary">{t('dashboard.manager.noRegister')}</Typography.Text>
      ) : query.isLoading ? (
        <Skeleton active paragraph={{ rows: 4 }} />
      ) : query.isError ? (
        <Typography.Text type="danger">
          {getTagesabschlussUserFacingError(t, query.error, {
            logContext: 'DailyClosingWidget',
            fallbackKey: 'dashboard.dailyClosing.loadError',
            skipLog: true,
          })}
        </Typography.Text>
      ) : !data || !today || !week ? null : (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <div>
            {tone === 'closed' ? (
              <Tag color="success" icon={<CheckCircleOutlined />} variant="filled">
                {t('dashboard.dailyClosing.todayClosed')}
              </Tag>
            ) : tone === 'open' ? (
              <Tag color="error" icon={<WarningOutlined />} variant="filled">
                {t('dashboard.dailyClosing.todayOpen', { count: today.transactionCount ?? 0 })}
              </Tag>
            ) : (
              <Tag variant="filled">{t('dashboard.dailyClosing.todayEmpty')}</Tag>
            )}
          </div>
          <div>
            <Typography.Text strong>{t('dashboard.dailyClosing.weekSummaryTitle')}</Typography.Text>
            <div>
              {t('dashboard.dailyClosing.weekSummary', {
                closed: closedDays,
                total: totalDays,
              })}
            </div>
            <Progress percent={percent} size="small" />
          </div>
          <Space wrap>
            <Link href="/tagesabschluss">
              <Button type="primary">{t('dashboard.dailyClosing.open')}</Button>
            </Link>
            {data.requiresAttention && canExecute ? (
              <Button type="primary" danger onClick={handleCloseToday}>
                {t('dashboard.dailyClosing.closeNow')}
              </Button>
            ) : null}
          </Space>
          <Typography.Text type="secondary">
            {data.lastClosing?.closedAt
              ? t('dashboard.dailyClosing.lastClosing', {
                  date: formatDateTime(data.lastClosing.closedAt, formatLocale),
                })
              : t('dashboard.dailyClosing.noClosingYet')}
          </Typography.Text>
        </Space>
      )}
    </WidgetShell>
  );
}
