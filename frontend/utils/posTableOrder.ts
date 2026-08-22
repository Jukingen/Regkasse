/** POS floor tables 1–10 — same set as TableSelector. */
export const POS_TABLE_NUMBERS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] as const;

/** First table with no items, otherwise table 1 (so Neue Bestellung always has a target). */
export function pickFirstFreeTableNumber(itemCountByTable: Map<number, number>): number {
  for (const n of POS_TABLE_NUMBERS) {
    if ((itemCountByTable.get(n) ?? 0) === 0) return n;
  }
  return POS_TABLE_NUMBERS[0];
}
