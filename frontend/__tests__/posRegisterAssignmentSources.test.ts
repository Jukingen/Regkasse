import { describe, expect, it } from '@jest/globals';
import { readFileSync } from 'fs';
import { join } from 'path';

/**
 * Guardrail: POS payment/register picker must not call inventory GET /api/CashRegister.
 * (Avoid importing services/api/cashRegisterService here — it pulls react-native via api config.)
 */
describe('POS register assignment sources', () => {
  const root = join(__dirname, '..');

  function read(rel: string): string {
    return readFileSync(join(root, rel), 'utf8');
  }

  it('cashRegisterService keeps POS selectable path aligned with backend ListSelectableForPosPickerAsync', () => {
    const svc = read('services/api/cashRegisterService.ts');
    expect(svc).toMatch(
      /POS_SELECTABLE_REGISTERS_PATH\s*=\s*['"]\/pos\/cash-register\/selectable['"]/
    );
  });

  it('assignment hook loads list only via fetchPosSelectableRegisters (no inventory GET)', () => {
    const src = read('hooks/usePosCashRegisterAssignment.ts');
    expect(src).toContain('fetchPosSelectableRegisters');
    expect(src).not.toMatch(/apiClient\.get\(\s*['"]\/?CashRegister/);
    expect(src).not.toMatch(/['"]\/api\/CashRegister['"]/);
  });

  it('settings assignment section uses selectable fetch, not inventory GET', () => {
    const src = read('components/CashRegisterAssignmentSection.tsx');
    expect(src).toContain('fetchPosSelectableRegisters');
    expect(src).not.toMatch(/apiClient\.get\(\s*['"]\/?CashRegister/);
    expect(src).not.toMatch(/['"]\/api\/CashRegister['"]/);
  });

  it('assignment hook awaits refreshAsync after successful cash-register persist (stale nextAction fix)', () => {
    const src = read('hooks/usePosCashRegisterAssignment.ts');
    expect(src).toMatch(/await\s+updateCashRegisterConfig\(/);
    expect(src).toMatch(/await\s+posReadiness\.refreshAsync\(\)/);
  });

  it('settings assignment section awaits refreshAsync after successful persist', () => {
    const src = read('components/CashRegisterAssignmentSection.tsx');
    expect(src).toMatch(/await\s+updateCashRegisterConfig\(/);
    expect(src).toMatch(/await\s+posReadiness\.refreshAsync\(\)/);
  });

  it('readiness provider exposes refreshAsync for awaited ensure-ready round-trip', () => {
    const src = read('contexts/PosRegisterReadinessContext.tsx');
    expect(src).toMatch(/refreshAsync:\s*\(\)\s*=>\s*Promise<void>/);
    expect(src).toContain('refreshWaitersRef');
  });
});
