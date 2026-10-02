import { describe, expect, it } from 'vitest';

import { ActivityEventType } from '@/api/generated/model/activityEventType';

function isSuspiciousActivityType(type: string): boolean {
  return type.startsWith('Suspicious');
}

describe('suspicious alerts activity refresh', () => {
  it('matches suspicious activity event types', () => {
    expect(isSuspiciousActivityType(ActivityEventType.SuspiciousHighValuePayment)).toBe(true);
    expect(isSuspiciousActivityType(ActivityEventType.SuspiciousMultipleStornos)).toBe(true);
    expect(isSuspiciousActivityType(ActivityEventType.BackupFailed)).toBe(false);
    expect(isSuspiciousActivityType(ActivityEventType.UserCreated)).toBe(false);
  });
});
