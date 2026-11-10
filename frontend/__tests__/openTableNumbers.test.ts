import { collectOpenTableNumbers } from '../utils/openTableNumbers';

describe('collectOpenTableNumbers', () => {
  it('returns every table that has items, not only the active table', () => {
    const tables = collectOpenTableNumbers(
      {
        1: { items: [{ qty: 2 }] },
        2: { items: [] },
        4: { items: [{ qty: 1 }, { qty: 0 }] },
      },
      {
        tableOrders: [
          { tableNumber: 1, itemCount: 2 },
          { tableNumber: 3, itemCount: 4 },
          { tableNumber: 5, itemCount: 0 },
        ],
      }
    );

    expect(tables).toEqual([1, 3, 4]);
  });

  it('returns an empty list when no table is open', () => {
    expect(collectOpenTableNumbers({ 1: { items: [{ qty: 0 }] } }, { tableOrders: [] })).toEqual(
      []
    );
  });

  it('ignores a stale recovery count when the local cart for that table is empty', () => {
    expect(
      collectOpenTableNumbers(
        { 2: { items: [{ qty: 0 }] } },
        { tableOrders: [{ tableNumber: 2, itemCount: 4 }, { tableNumber: 6, itemCount: 1 }] }
      )
    ).toEqual([6]);
  });
});
