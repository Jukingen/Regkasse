'use client';

import { Table, type TableProps } from 'antd';

import {
  ADMIN_TABLE_VIRTUAL_SCROLL_Y,
  shouldUseAdminTableVirtual,
} from '@/components/ui/adminTableVirtual';

export type VirtualTableProps<T extends object> = TableProps<T> & {
  /** Body height when virtual mode is on (default ADMIN_TABLE_VIRTUAL_SCROLL_Y). */
  virtualScrollY?: number;
  /** Force virtual on/off; default: auto from dataSource length (≥ ADMIN_TABLE_VIRTUAL_MIN_ROWS). */
  forceVirtual?: boolean;
};

function resolveScrollX(scroll: TableProps<object>['scroll'], fallback = 1200): number {
  if (typeof scroll === 'object' && scroll && 'x' in scroll && scroll.x != null) {
    const x = scroll.x;
    if (typeof x === 'number' && Number.isFinite(x)) return x;
    if (typeof x === 'string') {
      const parsed = Number.parseInt(x, 10);
      if (Number.isFinite(parsed)) return parsed;
    }
  }
  return fallback;
}

/**
 * Ant Design Table facade that enables native `virtual` scrolling when the
 * current page has enough rows. Uses `@rc-component/virtual-list` via antd —
 * do not slice dataSource manually.
 */
export function VirtualTable<T extends object>({
  dataSource,
  scroll,
  virtualScrollY = ADMIN_TABLE_VIRTUAL_SCROLL_Y,
  forceVirtual,
  pagination,
  ...rest
}: VirtualTableProps<T>) {
  const rowCount = Array.isArray(dataSource) ? dataSource.length : 0;
  const useVirtual = forceVirtual ?? shouldUseAdminTableVirtual(rowCount);
  const scrollX = resolveScrollX(scroll);

  const resolvedScroll: TableProps<T>['scroll'] = useVirtual
    ? { x: scrollX, y: virtualScrollY }
    : scroll
      ? { ...scroll, x: scroll.x ?? scrollX }
      : { x: scrollX };

  return (
    <Table<T>
      {...rest}
      dataSource={dataSource}
      virtual={useVirtual}
      scroll={resolvedScroll}
      pagination={pagination}
    />
  );
}
