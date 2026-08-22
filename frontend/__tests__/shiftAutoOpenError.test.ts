import { describe, expect, it } from '@jest/globals';

import {
  parseShiftAutoOpenError,
  SHIFT_AUTO_OPEN_CODES,
  shouldClearPosRegisterAssignment,
} from '../utils/shiftAutoOpenError';

describe('parseShiftAutoOpenError', () => {
  it('reads code from apiClient normalized 400 payload', () => {
    const err = parseShiftAutoOpenError({
      status: 400,
      data: {
        success: false,
        code: SHIFT_AUTO_OPEN_CODES.REGISTER_UNAVAILABLE,
        message: 'Die ausgewählte Kasse ist nicht verfügbar. Bitte kontaktieren Sie den Administrator.',
      },
    });
    expect(err.code).toBe(SHIFT_AUTO_OPEN_CODES.REGISTER_UNAVAILABLE);
    expect(err.httpStatus).toBe(400);
  });
});

describe('shouldClearPosRegisterAssignment', () => {
  it('clears stale assignment for selection and unavailable codes', () => {
    expect(shouldClearPosRegisterAssignment(SHIFT_AUTO_OPEN_CODES.NEED_REGISTER_SELECTION)).toBe(
      true
    );
    expect(shouldClearPosRegisterAssignment(SHIFT_AUTO_OPEN_CODES.REGISTER_NOT_FOUND)).toBe(true);
    expect(shouldClearPosRegisterAssignment(SHIFT_AUTO_OPEN_CODES.REGISTER_DECOMMISSIONED)).toBe(
      true
    );
    expect(shouldClearPosRegisterAssignment(SHIFT_AUTO_OPEN_CODES.SHIFT_ALREADY_OPEN)).toBe(false);
    expect(shouldClearPosRegisterAssignment(SHIFT_AUTO_OPEN_CODES.OK)).toBe(false);
  });
});
