import { describe, expect, it } from '@jest/globals';

import {
  filterPaymentUsableSelectableRows,
  isPaymentUsableSelectableRow,
} from '../utils/posSelectableRegisterFilter';

describe('posSelectableRegisterFilter', () => {
  it('treats rows without status as usable (canonical POS selectable response)', () => {
    expect(
      isPaymentUsableSelectableRow({
        id: 'a',
        registerNumber: 'K1',
      })
    ).toBe(true);
  });

  it('drops Closed rows (inventory / wrong-endpoint hardening)', () => {
    expect(
      isPaymentUsableSelectableRow({
        id: 'a',
        registerNumber: 'K1',
        status: 'Closed',
      })
    ).toBe(false);
  });

  it('keeps Open rows when status is present', () => {
    expect(
      isPaymentUsableSelectableRow({
        id: 'a',
        registerNumber: 'K1',
        status: 'Open',
      })
    ).toBe(true);
  });

  it('filter leaves only usable options when mix of Open and Closed', () => {
    const rows = filterPaymentUsableSelectableRows([
      { id: '1', registerNumber: 'A', status: 'Closed' },
      { id: '2', registerNumber: 'B', status: 'Open' },
      { id: '3', registerNumber: 'C', status: 'closed' },
    ]);
    expect(rows).toHaveLength(1);
    expect(rows[0].id).toBe('2');
  });

  it('empty usable list when only closed rows (no false selectable surface)', () => {
    const rows = filterPaymentUsableSelectableRows([
      { id: '1', registerNumber: 'A', status: 'Closed' },
      { id: '2', registerNumber: 'B', status: 'Closed' },
    ]);
    expect(rows).toEqual([]);
  });
});
