/**
 * ticketSeatToastMessage uses four static i18n keys; unknown notices stay raw text and never reach `t`.
 */
import { jest, describe, it, expect } from '@jest/globals';

import { readTicketSeatErrorCode, ticketSeatToastMessage } from '../components/ticketSeatNotice';

describe('ticketSeatToastMessage', () => {
  it.each([
    ['SEAT_TAKEN', 'verticalProfiles:screens.tickets.seatTakenHold'],
    ['CATEGORY_CAPACITY_REACHED', 'verticalProfiles:screens.tickets.seatCategoryCapacity'],
    ['CATEGORY_PRICE_MISSING', 'verticalProfiles:screens.tickets.seatCategoryPriceMissing'],
    ['COMPANION_TARGET_NOT_SOLD', 'verticalProfiles:screens.tickets.companionTargetNotSold'],
  ])('maps %s to the static key %s', (notice, expectedKey) => {
    const t = jest.fn((key: string) => `translated:${key}`);

    expect(ticketSeatToastMessage(t, notice)).toBe(`translated:${expectedKey}`);
    expect(t).toHaveBeenCalledTimes(1);
    expect(t).toHaveBeenCalledWith(expectedKey);
  });

  it('returns unknown notices as raw text without calling t', () => {
    const t = jest.fn((key: string) => `translated:${key}`);

    expect(ticketSeatToastMessage(t, 'Sitz konnte nicht reserviert werden')).toBe(
      'Sitz konnte nicht reserviert werden'
    );
    expect(ticketSeatToastMessage(t, 'constructor')).toBe('constructor');
    expect(ticketSeatToastMessage(t, '')).toBe('');
    expect(t).not.toHaveBeenCalled();
  });
});

describe('readTicketSeatErrorCode', () => {
  it.each(['SEAT_TAKEN', 'CATEGORY_CAPACITY_REACHED', 'CATEGORY_PRICE_MISSING', 'COMPANION_TARGET_NOT_SOLD'])(
    'accepts %s on HTTP 409 and 400',
    (code) => {
      expect(readTicketSeatErrorCode({ status: 409, data: { code } })).toBe(code);
      expect(readTicketSeatErrorCode({ response: { status: 400, data: { code } } })).toBe(code);
    }
  );

  it('rejects unknown codes, prototype names and other statuses', () => {
    expect(readTicketSeatErrorCode({ status: 409, data: { code: 'SOMETHING_ELSE' } })).toBeNull();
    expect(readTicketSeatErrorCode({ status: 409, data: { code: 'constructor' } })).toBeNull();
    expect(readTicketSeatErrorCode({ status: 500, data: { code: 'SEAT_TAKEN' } })).toBeNull();
    expect(readTicketSeatErrorCode(null)).toBeNull();
    expect(readTicketSeatErrorCode('error')).toBeNull();
  });
});
