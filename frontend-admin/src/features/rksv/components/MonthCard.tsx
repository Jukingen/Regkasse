'use client';

import {
  BarChartOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
  CloseCircleOutlined,
  FileSearchOutlined,
  FileTextOutlined,
  LinkOutlined,
  PlusCircleOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import type { MenuProps } from 'antd';
import { Card, Dropdown, Tag, Typography } from 'antd';
import { type CSSProperties, type ReactNode, useCallback, useMemo, useRef, useState } from 'react';

import { useI18n } from '@/i18n';

export type MonthCardStatus = 'missing' | 'completed' | 'pending';

export type MonthCardAction =
  'create-late' | 'view-report' | 'view-revenue' | 'view-receipts' | 'recreate' | 'copy-link';

export type MonthCardActionPayload = {
  month: number;
  year: number;
  status: MonthCardStatus;
};

export type MonthCardProps = {
  month: number;
  year: number;
  status: MonthCardStatus;
  /** When true, shows «Erneut erstellen» for completed months. */
  canRecreate?: boolean;
  onAction: (action: MonthCardAction, data: MonthCardActionPayload) => void;
  /**
   * When set, primary click/Enter opens the quick summary instead of the action menu.
   * Right-click / long-press still open the full context menu.
   */
  onOpenSummary?: (data: MonthCardActionPayload) => void;
};

const LONG_PRESS_MS = 550;

const STATUS_BORDER: Record<MonthCardStatus, string> = {
  missing: '#dc2626',
  completed: '#16a34a',
  pending: '#eab308',
};

const STATUS_BG: Record<MonthCardStatus, string> = {
  missing: '#fff2f0',
  completed: '#f6ffed',
  pending: '#fafafa',
};

export function monthStatusTagColor(status: MonthCardStatus): string {
  switch (status) {
    case 'missing':
      return 'red';
    case 'completed':
      return 'green';
    case 'pending':
    default:
      return 'orange';
  }
}

function monthShortName(month1to12: number, locale: string): string {
  return new Intl.DateTimeFormat(locale, {
    month: 'short',
    timeZone: 'Europe/Vienna',
  }).format(new Date(Date.UTC(2026, month1to12 - 1, 1)));
}

export function MonthCard({
  month,
  year,
  status,
  canRecreate = false,
  onAction,
  onOpenSummary,
}: MonthCardProps) {
  const { t, textLocale } = useI18n();
  const [clickMenuOpen, setClickMenuOpen] = useState(false);
  const [contextMenuOpen, setContextMenuOpen] = useState(false);
  const longPressTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const longPressTriggeredRef = useRef(false);

  const payload = useMemo(
    (): MonthCardActionPayload => ({ month, year, status }),
    [month, year, status]
  );

  const emit = useCallback(
    (action: MonthCardAction) => {
      onAction(action, payload);
    },
    [onAction, payload]
  );

  const openPrimary = useCallback(() => {
    setContextMenuOpen(false);
    if (onOpenSummary) {
      setClickMenuOpen(false);
      onOpenSummary(payload);
      return;
    }
    setClickMenuOpen(true);
  }, [onOpenSummary, payload]);

  const statusConfig = useMemo((): {
    icon: ReactNode;
    label: string;
    color: string;
  } => {
    switch (status) {
      case 'missing':
        return {
          icon: <CloseCircleOutlined style={{ color: '#dc2626' }} />,
          label: t('rksvHub.monatsbelegTimeline.statusMissing'),
          color: monthStatusTagColor(status),
        };
      case 'completed':
        return {
          icon: <CheckCircleOutlined style={{ color: '#16a34a' }} />,
          label: t('rksvHub.monatsbelegTimeline.statusCompleted'),
          color: monthStatusTagColor(status),
        };
      case 'pending':
      default:
        return {
          icon: <ClockCircleOutlined style={{ color: '#eab308' }} />,
          label: t('rksvHub.monatsbelegTimeline.statusPending'),
          color: monthStatusTagColor(status),
        };
    }
  }, [status, t]);

  const primaryActionItems = useMemo((): NonNullable<MenuProps['items']> => {
    const shared: NonNullable<MenuProps['items']> = [
      {
        key: 'view-revenue',
        icon: <BarChartOutlined />,
        label: t('rksvHub.monatsbelegTimeline.actionViewRevenue'),
        onClick: () => emit('view-revenue'),
      },
      {
        key: 'view-receipts',
        icon: <FileSearchOutlined />,
        label: t('rksvHub.monatsbelegTimeline.actionViewReceipts'),
        onClick: () => emit('view-receipts'),
      },
    ];

    if (status === 'missing') {
      return [
        {
          key: 'create-late',
          icon: <PlusCircleOutlined />,
          label: t('rksvHub.monatsbelegTimeline.actionCreateLate'),
          onClick: () => emit('create-late'),
        },
        ...shared,
      ];
    }

    if (status === 'completed') {
      const items: NonNullable<MenuProps['items']> = [
        {
          key: 'view-report',
          icon: <FileTextOutlined />,
          label: t('rksvHub.monatsbelegTimeline.actionViewReport'),
          onClick: () => emit('view-report'),
        },
        ...shared,
      ];
      if (canRecreate) {
        items.push({
          key: 'recreate',
          icon: <ReloadOutlined />,
          label: t('rksvHub.monatsbelegTimeline.actionRecreate'),
          danger: true,
          onClick: () => emit('recreate'),
        });
      }
      return items;
    }

    return shared;
  }, [canRecreate, emit, status, t]);

  const contextMenuItems = useMemo((): MenuProps['items'] => {
    return [
      {
        key: 'quick-actions',
        icon: <FileSearchOutlined />,
        label: t('rksvHub.monatsbelegTimeline.contextQuickActions'),
        children: primaryActionItems,
      },
      { type: 'divider' },
      {
        key: 'copy-link',
        icon: <LinkOutlined />,
        label: t('rksvHub.monatsbelegTimeline.contextCopyLink'),
        onClick: () => emit('copy-link'),
      },
    ];
  }, [emit, primaryActionItems, t]);

  const clearLongPress = useCallback(() => {
    if (longPressTimerRef.current != null) {
      clearTimeout(longPressTimerRef.current);
      longPressTimerRef.current = null;
    }
  }, []);

  const onPointerDown = useCallback(() => {
    longPressTriggeredRef.current = false;
    clearLongPress();
    longPressTimerRef.current = setTimeout(() => {
      longPressTriggeredRef.current = true;
      setClickMenuOpen(false);
      setContextMenuOpen(true);
    }, LONG_PRESS_MS);
  }, [clearLongPress]);

  const onPointerUpOrLeave = useCallback(() => {
    clearLongPress();
  }, [clearLongPress]);

  const cardStyle: CSSProperties = {
    cursor: 'pointer',
    textAlign: 'center',
    background: STATUS_BG[status],
    borderColor: STATUS_BORDER[status],
    userSelect: 'none',
    WebkitUserSelect: 'none',
    WebkitTouchCallout: 'none',
  };

  const card = (
    <Card
      size="small"
      hoverable
      role="button"
      tabIndex={0}
      aria-label={t('rksvHub.monatsbelegTimeline.cardAriaLabel', {
        month: monthShortName(month, textLocale),
        year,
        status: statusConfig.label,
      })}
      style={cardStyle}
      styles={{
        body: {
          padding: 12,
        },
      }}
      onClick={(e) => {
        if (longPressTriggeredRef.current) {
          e.preventDefault();
          e.stopPropagation();
          longPressTriggeredRef.current = false;
          return;
        }
        openPrimary();
      }}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          openPrimary();
        }
      }}
      onPointerDown={onPointerDown}
      onPointerUp={onPointerUpOrLeave}
      onPointerLeave={onPointerUpOrLeave}
      onPointerCancel={onPointerUpOrLeave}
    >
      <Typography.Text strong style={{ fontSize: 16, display: 'block' }}>
        {monthShortName(month, textLocale)}
      </Typography.Text>
      <div style={{ fontSize: 22, margin: '6px 0' }} aria-hidden>
        {statusConfig.icon}
      </div>
      <Tag color={statusConfig.color}>{statusConfig.label}</Tag>
      <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: 4 }}>
        {year}
      </Typography.Text>
    </Card>
  );

  // When summary owns the click, only keep the context-menu dropdown.
  if (onOpenSummary) {
    return (
      <Dropdown
        menu={{ items: contextMenuItems }}
        trigger={['contextMenu']}
        open={contextMenuOpen}
        onOpenChange={setContextMenuOpen}
        placement="bottom"
      >
        {card}
      </Dropdown>
    );
  }

  return (
    <Dropdown
      menu={{ items: contextMenuItems }}
      trigger={['contextMenu']}
      open={contextMenuOpen}
      onOpenChange={(open) => {
        setContextMenuOpen(open);
        if (open) setClickMenuOpen(false);
      }}
      placement="bottom"
    >
      <Dropdown
        menu={{ items: primaryActionItems }}
        trigger={['click']}
        open={clickMenuOpen}
        onOpenChange={(open) => {
          if (longPressTriggeredRef.current && open) {
            return;
          }
          setClickMenuOpen(open);
          if (open) setContextMenuOpen(false);
        }}
        placement="bottom"
      >
        {card}
      </Dropdown>
    </Dropdown>
  );
}
