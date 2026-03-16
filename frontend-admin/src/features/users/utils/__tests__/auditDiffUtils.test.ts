/**
 * Audit diff parsing: stable UI on parse error, only safe fields from backend.
 */
import { describe, it, expect } from 'vitest';
import { parseAuditDiff, formatDiffValue, EMPTY_PLACEHOLDER } from '../auditDiffUtils';

const getLabel = (key: string) => key;

describe('parseAuditDiff', () => {
    it('returns null when both inputs are null/empty', () => {
        expect(parseAuditDiff(null, null, getLabel)).toBeNull();
        expect(parseAuditDiff('', '', getLabel)).toBeNull();
        expect(parseAuditDiff(undefined, undefined, getLabel)).toBeNull();
        expect(parseAuditDiff('  ', '  ', getLabel)).toBeNull();
    });

    it('returns null on invalid JSON (does not throw)', () => {
        expect(parseAuditDiff('{ invalid', null, getLabel)).toBeNull();
        expect(parseAuditDiff(null, 'not json', getLabel)).toBeNull();
    });

    it('returns diff rows for valid USER_UPDATE-style JSON', () => {
        const oldV = JSON.stringify({ FirstName: 'A', LastName: 'B', Role: 'Cashier' });
        const newV = JSON.stringify({ FirstName: 'A', LastName: 'C', Role: 'Cashier' });
        const rows = parseAuditDiff(oldV, newV, getLabel);
        expect(rows).not.toBeNull();
        expect(rows!.length).toBe(1);
        expect(rows![0].field).toBe('LastName');
        expect(rows![0].oldVal).toBe('B');
        expect(rows![0].newVal).toBe('C');
    });

    it('returns diff rows for USER_ROLE_CHANGE (Role only)', () => {
        const oldV = JSON.stringify({ Role: 'Kellner' });
        const newV = JSON.stringify({ Role: 'Demo' });
        const rows = parseAuditDiff(oldV, newV, getLabel);
        expect(rows).not.toBeNull();
        expect(rows!.length).toBe(1);
        expect(rows![0].label).toBe('Role');
        expect(rows![0].oldVal).toBe('Kellner');
        expect(rows![0].newVal).toBe('Demo');
    });

    it('returns null when no keys differ', () => {
        const v = JSON.stringify({ Role: 'Cashier' });
        expect(parseAuditDiff(v, v, getLabel)).toBeNull();
    });

    it('does not throw on non-object JSON (array or number)', () => {
        expect(() => parseAuditDiff('[]', null, getLabel)).not.toThrow();
        expect(() => parseAuditDiff('123', null, getLabel)).not.toThrow();
    });
});

describe('formatDiffValue', () => {
    it('returns placeholder for null/undefined/empty string', () => {
        expect(formatDiffValue('x', null)).toBe(EMPTY_PLACEHOLDER);
        expect(formatDiffValue('x', undefined)).toBe(EMPTY_PLACEHOLDER);
        expect(formatDiffValue('x', '')).toBe(EMPTY_PLACEHOLDER);
    });

    it('formats boolean as active/inactive with options', () => {
        expect(formatDiffValue('isActive', true, { labelActive: 'Aktiv', labelInactive: 'Inaktiv' })).toBe('Aktiv');
        expect(formatDiffValue('isActive', false, { labelActive: 'Aktiv', labelInactive: 'Inaktiv' })).toBe('Inaktiv');
    });

    it('truncates long strings', () => {
        const long = 'a'.repeat(150);
        const result = formatDiffValue('notes', long, { maxCellLength: 120 });
        expect(result.length).toBe(121);
        expect(result.endsWith('…')).toBe(true);
    });
});
