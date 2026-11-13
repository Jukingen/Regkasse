export type OpenTableCartSnapshot = {
  items?: ReadonlyArray<{ qty?: number | null }> | null;
};

export type OpenTableRecoverySnapshot = {
  tableOrders?: ReadonlyArray<{
    tableNumber?: number | null;
    itemCount?: number | null;
  }> | null;
};

function addOpenTable(open: Set<number>, tableNumber: number, itemCount: number): void {
  if (!Number.isFinite(tableNumber) || tableNumber <= 0) return;
  if (itemCount > 0) open.add(tableNumber);
}

type OpenTableCartMap = {
  [tableNumber: number]: OpenTableCartSnapshot | undefined;
};

/** Tables that currently hold cart lines, from local carts and recovery snapshots. */
export function collectOpenTableNumbers(
  cartsByTable: OpenTableCartMap | null | undefined,
  recoveryData?: OpenTableRecoverySnapshot | null
): number[] {
  const open = new Set<number>();
  const localTables = new Set<number>();

  for (const [key, cartData] of Object.entries(cartsByTable ?? {})) {
    const tableNumber = Number(key);
    if (Number.isFinite(tableNumber) && tableNumber > 0) localTables.add(tableNumber);
    const total = (cartData?.items ?? []).reduce((sum, item) => sum + (item?.qty ?? 0), 0);
    addOpenTable(open, tableNumber, total);
  }

  for (const order of recoveryData?.tableOrders ?? []) {
    const tableNumber = Number(order?.tableNumber);
    // A loaded local cart is the source of truth. Stale recovery counts must not
    // keep a table "open" after Alle leeren already emptied it.
    if (localTables.has(tableNumber)) continue;
    addOpenTable(open, tableNumber, order?.itemCount ?? 0);
  }

  return [...open].sort((a, b) => a - b);
}
