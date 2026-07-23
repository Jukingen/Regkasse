'use client';

/**
 * Reusable Ant Design Table date cell.
 *
 * Do not call hooks inside column `render` — render this component instead:
 *   render: (value) => <DateColumn date={value} format="datetime" />
 * or:
 *   render: dateColumnRender('datetime')
 */
import type { CSSProperties, ReactNode } from 'react';

import { useFormattedDate, type DateFormatKey } from '@/hooks/useFormattedDate';
import type { DateInput } from '@/lib/dateUtils';

export type DateColumnProps = {
  date: DateInput;
  /** Catalog key under `common.dateFormat` (default: short = DD.MM.YYYY). */
  format?: DateFormatKey;
  /** Parse/format as UTC wall time. */
  utc?: boolean;
  className?: string;
  style?: CSSProperties;
};

export function DateColumn({
  date,
  format = 'short',
  utc = false,
  className,
  style,
}: DateColumnProps) {
  const { format: formatDate } = useFormattedDate();
  return (
    <span className={className} style={style}>
      {formatDate(date, format, { utc })}
    </span>
  );
}

/**
 * Factory for Ant Design `columns[].render` — keeps hooks inside {@link DateColumn}.
 */
export function dateColumnRender(
  format: DateFormatKey = 'short',
  options?: { utc?: boolean }
): (value: DateInput) => ReactNode {
  function DateColumnCell(value: DateInput): ReactNode {
    return <DateColumn date={value} format={format} utc={options?.utc} />;
  }
  DateColumnCell.displayName = `DateColumnCell(${format})`;
  return DateColumnCell;
}
