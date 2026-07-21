/** Vertical scroll height when Ant Design Table `virtual` mode is enabled. */
export const ADMIN_TABLE_VIRTUAL_SCROLL_Y = 520;

/** Enable virtual scrolling at or above this row count (client-side page only). */
export const ADMIN_TABLE_VIRTUAL_MIN_ROWS = 30;

export function shouldUseAdminTableVirtual(rowCount: number): boolean {
  return rowCount >= ADMIN_TABLE_VIRTUAL_MIN_ROWS;
}

export function adminTableScrollXy(scrollX: number, rowCount: number): { x: number; y?: number } {
  if (!shouldUseAdminTableVirtual(rowCount)) {
    return { x: scrollX };
  }
  return { x: scrollX, y: ADMIN_TABLE_VIRTUAL_SCROLL_Y };
}
