import { describe, expect, it } from 'vitest';

function isSuspiciousActivityType(type: string): boolean {
    return type.startsWith('Suspicious');
}

describe('suspicious alerts activity refresh', () => {
    it('matches suspicious activity event types', () => {
        expect(isSuspiciousActivityType('SuspiciousHighValuePayment')).toBe(true);
        expect(isSuspiciousActivityType('SuspiciousMultipleStornos')).toBe(true);
        expect(isSuspiciousActivityType('BackupFailed')).toBe(false);
        expect(isSuspiciousActivityType('UserCreated')).toBe(false);
    });
});
