import { describe, expect, it } from '@jest/globals';

import {
  cashRegisterPersistFailureAlertDe,
  isCashRegisterAssignmentRejectedByBackend,
  shouldRetainOptimisticCashRegisterAfterPersistFailure,
} from '../utils/cashRegisterAssignmentPersistPolicy';
import { POS_CASH_REGISTER_CODES } from '../utils/posRegisterGateCopy';

const validA = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const validB = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

describe('isCashRegisterAssignmentRejectedByBackend', () => {
  it('treats 403 as assignment rejection', () => {
    expect(isCashRegisterAssignmentRejectedByBackend({ status: 403, data: {} })).toBe(true);
  });

  it('treats 401 as rejection (no session-usable messaging path)', () => {
    expect(isCashRegisterAssignmentRejectedByBackend({ status: 401, data: {} })).toBe(true);
  });

  it('treats 400 with known cash-register code as rejection', () => {
    expect(
      isCashRegisterAssignmentRejectedByBackend({
        status: 400,
        data: { code: POS_CASH_REGISTER_CODES.CLOSED },
      })
    ).toBe(true);
  });

  it('treats generic 400 as rejection for this endpoint', () => {
    expect(isCashRegisterAssignmentRejectedByBackend({ status: 400, data: { message: 'bad' } })).toBe(true);
  });

  it('does not treat 500 as policy rejection', () => {
    expect(isCashRegisterAssignmentRejectedByBackend({ status: 500, data: {} })).toBe(false);
  });

  it('does not treat shapeless errors as policy rejection', () => {
    expect(isCashRegisterAssignmentRejectedByBackend(new Error('network'))).toBe(false);
  });
});

describe('shouldRetainOptimisticCashRegisterAfterPersistFailure', () => {
  const readyInput = (attempted: string) => ({
    nextAction: 'ready' as const,
    effectiveRegisterId: validA,
    attemptedRegisterId: attempted,
  });

  it('allows retention on transient error when ensure-ready approved the same register', () => {
    expect(
      shouldRetainOptimisticCashRegisterAfterPersistFailure({ status: 500 }, readyInput(validA))
    ).toBe(true);
  });

  it('disallows retention when backend rejected assignment', () => {
    expect(
      shouldRetainOptimisticCashRegisterAfterPersistFailure(
        { status: 403, data: {} },
        readyInput(validA)
      )
    ).toBe(false);
  });

  it('disallows retention when attempted register differs from effective readiness id', () => {
    expect(
      shouldRetainOptimisticCashRegisterAfterPersistFailure({ status: 500 }, readyInput(validB))
    ).toBe(false);
  });

  it('disallows retention when readiness is not ready even on 500', () => {
    expect(
      shouldRetainOptimisticCashRegisterAfterPersistFailure(
        { status: 500 },
        { nextAction: 'select_register', effectiveRegisterId: validA, attemptedRegisterId: validA }
      )
    ).toBe(false);
  });

  it('disallows retention on transient error when readiness is open_register (no false usable session)', () => {
    expect(
      shouldRetainOptimisticCashRegisterAfterPersistFailure(
        { status: 500 },
        {
          nextAction: 'open_register',
          effectiveRegisterId: validA,
          attemptedRegisterId: validA,
        }
      )
    ).toBe(false);
  });
});

describe('cashRegisterPersistFailureAlertDe', () => {
  it('does not promise session usability when backend rejected assignment', () => {
    const { title, message } = cashRegisterPersistFailureAlertDe(
      { status: 400, data: { code: POS_CASH_REGISTER_CODES.FORBIDDEN } },
      false
    );
    expect(title).toContain('Zuweisung');
    expect(message.toLowerCase()).not.toMatch(/für diese sitzung.*nutzbar/i);
    expect(message.toLowerCase()).not.toMatch(/freigibt/i);
  });

  it('mentions readiness guard when retention is allowed (profile-only failure)', () => {
    const { message } = cashRegisterPersistFailureAlertDe({ status: 500 }, true);
    expect(message).toMatch(/Profil/i);
    expect(message).toMatch(/Kassenbereitschaft|freigibt/i);
  });

  it('transient failure without retention uses neutral retry copy (no false payment guarantee)', () => {
    const { message } = cashRegisterPersistFailureAlertDe({ status: 500 }, false);
    expect(message).toMatch(/erneut versuchen/i);
    expect(message.toLowerCase()).not.toMatch(/für diese sitzung.*nutzbar/i);
  });

  it('maps closed code to German copy', () => {
    const { message } = cashRegisterPersistFailureAlertDe(
      { status: 400, data: { code: POS_CASH_REGISTER_CODES.CLOSED } },
      false
    );
    expect(message).toMatch(/nicht geöffnet/i);
  });
});
