'use client';

import { LeftOutlined, RightOutlined } from '@ant-design/icons';
import type { MenuProps } from 'antd';
import { Alert, Button, Dropdown, Skeleton, Space, Tooltip, Typography } from 'antd';
import { useCallback, useMemo } from 'react';

import type { DailyClosingCalendarDay } from '@/features/tagesabschluss/api/dailyClosingCalendar';
import {
  CalendarDayStatus,
  calendarDateKey,
  calendarStatusTooltipKey,
  resolveCalendarDayStatus,
} from '@/features/tagesabschluss/calendarStatus';
import { useI18n } from '@/i18n';
import { formatUserMonthYear } from '@/lib/dateFormatter';

import styles from './DailyClosingCalendar.module.css';

const WEEKDAY_KEYS = ['mon', 'tue', 'wed', 'thu', 'fri', 'sat', 'sun'] as const;

export type DailyClosingCalendarProps = {
  year: number;
  month: number;
  days: DailyClosingCalendarDay[];
  loading?: boolean;
  errorMessage?: string | null;
  selectedDate?: string;
  canExecute?: boolean;
  canDownloadPdf?: boolean;
  onMonthChange: (year: number, month: number) => void;
  onSelectDay: (day: DailyClosingCalendarDay) => void;
  onCloseDay: (day: DailyClosingCalendarDay) => void;
  onViewDetails: (day: DailyClosingCalendarDay) => void;
  onExportSummary: (day: DailyClosingCalendarDay) => void;
};

type GridCell =
  | { kind: 'pad'; key: string }
  | { kind: 'day'; key: string; day: DailyClosingCalendarDay };

function statusClass(status: CalendarDayStatus): string {
  switch (status) {
    case CalendarDayStatus.Closed:
      return styles.closed;
    case CalendarDayStatus.Empty:
      return styles.empty;
    case CalendarDayStatus.Open:
      return styles.open;
    case CalendarDayStatus.NoTransactions:
      return styles.noTransactions;
    case CalendarDayStatus.Future:
      return styles.future;
    default:
      return '';
  }
}

function shiftMonth(year: number, month: number, delta: number): { year: number; month: number } {
  const next = month - 1 + delta;
  const wrappedYear = year + Math.floor(next / 12);
  const wrappedMonth = ((next % 12) + 12) % 12;
  return { year: wrappedYear, month: wrappedMonth + 1 };
}

function mondayFirstIndex(year: number, month: number): number {
  const jsDay = new Date(Date.UTC(year, month - 1, 1)).getUTCDay();
  return (jsDay + 6) % 7;
}

function buildGrid(year: number, month: number, days: DailyClosingCalendarDay[]): GridCell[] {
  const byDate = new Map(days.map((day) => [calendarDateKey(day.date ?? ''), day]));
  const daysInMonth = new Date(year, month, 0).getDate();
  const lead = mondayFirstIndex(year, month);
  const cells: GridCell[] = [];
  for (let i = 0; i < lead; i += 1) {
    cells.push({ kind: 'pad', key: `pad-lead-${i}` });
  }
  for (let d = 1; d <= daysInMonth; d += 1) {
    const date = `${year}-${String(month).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
    const day = byDate.get(date);
    if (!day) {
      cells.push({ kind: 'pad', key: `missing-${date}` });
      continue;
    }
    cells.push({ kind: 'day', key: date, day });
  }
  while (cells.length % 7 !== 0) {
    cells.push({ kind: 'pad', key: `pad-trail-${cells.length}` });
  }
  return cells;
}

export function DailyClosingCalendar({
  year,
  month,
  days,
  loading,
  errorMessage,
  selectedDate,
  canExecute = false,
  canDownloadPdf = false,
  onMonthChange,
  onSelectDay,
  onCloseDay,
  onViewDetails,
  onExportSummary,
}: DailyClosingCalendarProps) {
  const { t } = useI18n();
  const monthLabel = formatUserMonthYear(`${year}-${String(month).padStart(2, '0')}-01`);
  const grid = useMemo(() => buildGrid(year, month, days), [year, month, days]);
  const dayOrder = useMemo(
    () => grid.filter((cell): cell is Extract<GridCell, { kind: 'day' }> => cell.kind === 'day'),
    [grid]
  );

  const activate = useCallback(
    (day: DailyClosingCalendarDay) => {
      onSelectDay(day);
      const status = resolveCalendarDayStatus(day);
      if (status === CalendarDayStatus.Future) return;
      if (day.isClosed) {
        onViewDetails(day);
        return;
      }
      if (canExecute && day.canClose) {
        onCloseDay(day);
      }
    },
    [canExecute, onCloseDay, onSelectDay, onViewDetails]
  );

  const moveSelection = useCallback(
    (current: DailyClosingCalendarDay, delta: number) => {
      const index = dayOrder.findIndex((cell) => cell.key === calendarDateKey(current.date ?? ''));
      if (index < 0) return;
      const next = dayOrder[index + delta];
      if (next) onSelectDay(next.day);
    },
    [dayOrder, onSelectDay]
  );

  const goPrev = () => {
    const next = shiftMonth(year, month, -1);
    onMonthChange(next.year, next.month);
  };
  const goNext = () => {
    const next = shiftMonth(year, month, 1);
    onMonthChange(next.year, next.month);
  };

  if (loading) {
    return <Skeleton active paragraph={{ rows: 8 }} />;
  }

  if (errorMessage) {
    return <Alert type="error" showIcon title={t('tagesabschluss.calendar.loadErrorTitle')} description={errorMessage} />;
  }

  return (
    <div className={styles.wrap}>
      <div className={styles.nav}>
        <Button icon={<LeftOutlined />} onClick={goPrev} aria-label={t('tagesabschluss.calendar.prevMonth')} />
        <Typography.Title level={5} style={{ margin: 0 }}>
          {monthLabel}
        </Typography.Title>
        <Button icon={<RightOutlined />} onClick={goNext} aria-label={t('tagesabschluss.calendar.nextMonth')} />
      </div>
      <div className={styles.weekdays}>
        {WEEKDAY_KEYS.map((key) => (
          <div key={key} className={styles.weekday}>
            {t(`tagesabschluss.calendar.weekdays.${key}`)}
          </div>
        ))}
      </div>
      <div className={styles.grid} role="grid" aria-label={t('tagesabschluss.calendar.gridLabel', { month: monthLabel })}>
        {grid.map((cell) => {
          if (cell.kind === 'pad') {
            return <div key={cell.key} className={styles.pad} />;
          }
          const { day } = cell;
          const status = resolveCalendarDayStatus(day);
          const dateKey = calendarDateKey(day.date ?? '');
          if (!dateKey) {
            return <div key={cell.key} className={styles.pad} />;
          }
          const selected = selectedDate === dateKey;
          const tooltip = t(calendarStatusTooltipKey(status), {
            count: day.transactionCount ?? 0,
          });
          const todayTip = day.isToday ? ` ${t('tagesabschluss.calendar.tooltip.today')}` : '';
          const items: NonNullable<MenuProps['items']> = [];
          if (canExecute && day.canClose) {
            items.push({
              key: 'close',
              label: t('tagesabschluss.calendar.menu.close'),
              onClick: () => onCloseDay(day),
            });
          }
          if (day.isClosed) {
            items.push({
              key: 'details',
              label: t('tagesabschluss.calendar.menu.details'),
              onClick: () => onViewDetails(day),
            });
          }
          if (day.isClosed && day.closingId && canDownloadPdf) {
            items.push({
              key: 'export',
              label: t('tagesabschluss.calendar.menu.export'),
              onClick: () => onExportSummary(day),
            });
          }

          const button = (
            <button
              type="button"
              className={[
                styles.cell,
                statusClass(status),
                selected ? styles.cellSelected : '',
                day.isToday ? styles.cellToday : '',
                status === CalendarDayStatus.Future ? styles.cellDisabled : '',
              ]
                .filter(Boolean)
                .join(' ')}
              disabled={status === CalendarDayStatus.Future}
              aria-current={day.isToday ? 'date' : undefined}
              aria-label={`${dateKey}. ${tooltip}`}
              onClick={() => activate(day)}
              onKeyDown={(event) => {
                if (event.key === 'ArrowLeft') {
                  event.preventDefault();
                  moveSelection(day, -1);
                } else if (event.key === 'ArrowRight') {
                  event.preventDefault();
                  moveSelection(day, 1);
                } else if (event.key === 'ArrowUp') {
                  event.preventDefault();
                  moveSelection(day, -7);
                } else if (event.key === 'ArrowDown') {
                  event.preventDefault();
                  moveSelection(day, 7);
                } else if (event.key === 'Enter') {
                  event.preventDefault();
                  activate(day);
                }
              }}
            >
              <span className={styles.dayNumber}>{Number(dateKey.slice(8, 10))}</span>
              <span className={styles.count}>
                {t('tagesabschluss.calendar.txCount', { count: day.transactionCount ?? 0 })}
              </span>
            </button>
          );

          const wrapped =
            items.length > 0 ? (
              <Dropdown menu={{ items }} trigger={['contextMenu']}>
                {button}
              </Dropdown>
            ) : (
              button
            );

          return (
            <Tooltip key={cell.key} title={`${tooltip}${todayTip}`}>
              {wrapped}
            </Tooltip>
          );
        })}
      </div>
      <Space className={styles.legend} wrap size={[12, 8]}>
        <span className={styles.legendItem}>
          <span className={`${styles.swatch} ${styles.swatchClosed}`} />
          {t('tagesabschluss.calendar.legend.closed')}
        </span>
        <span className={styles.legendItem}>
          <span className={`${styles.swatch} ${styles.swatchEmpty}`} />
          {t('tagesabschluss.calendar.legend.empty')}
        </span>
        <span className={styles.legendItem}>
          <span className={`${styles.swatch} ${styles.swatchOpen}`} />
          {t('tagesabschluss.calendar.legend.open')}
        </span>
        <span className={styles.legendItem}>
          <span className={`${styles.swatch} ${styles.swatchNoTx}`} />
          {t('tagesabschluss.calendar.legend.noTransactions')}
        </span>
        <span className={styles.legendItem}>
          <span className={`${styles.swatch} ${styles.swatchFuture}`} />
          {t('tagesabschluss.calendar.legend.future')}
        </span>
        <span className={styles.legendItem}>
          <span className={`${styles.swatch} ${styles.swatchToday}`} />
          {t('tagesabschluss.calendar.legend.today')}
        </span>
      </Space>
    </div>
  );
}
