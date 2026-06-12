import type { PaymentMethodDefinitionAdmin } from '@/api/admin/payment-method-definitions';
import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';

export type RegisterMethodCell = {
  definitionId: string;
  isActive: boolean;
  isDefault: boolean;
  name: string;
  displayOrder: number;
  definition: PaymentMethodDefinitionAdmin;
};

export type PaymentMethodMatrixRow = {
  code: string;
  label: string;
  sortOrder: number;
  byRegister: Record<string, RegisterMethodCell | null>;
};

export type PaymentMethodRegisterSummary = {
  registerId: string;
  registerNumber: string;
  location?: string | null;
  activeCodes: string[];
  inactiveCodes: string[];
  defaultCode: string | null;
};

export function buildPaymentMethodMatrix(
  registers: AdminCashRegisterListItem[],
  methodsByRegisterId: Record<string, PaymentMethodDefinitionAdmin[] | undefined>,
): { rows: PaymentMethodMatrixRow[]; summaries: PaymentMethodRegisterSummary[] } {
  const codeMeta = new Map<string, { label: string; sortOrder: number }>();

  for (const register of registers) {
    const methods = methodsByRegisterId[register.id] ?? [];
    for (const m of methods) {
      const existing = codeMeta.get(m.code);
      if (!existing) {
        codeMeta.set(m.code, { label: m.name, sortOrder: m.displayOrder });
      } else {
        codeMeta.set(m.code, {
          label: existing.label || m.name,
          sortOrder: Math.min(existing.sortOrder, m.displayOrder),
        });
      }
    }
  }

  const sortedCodes = [...codeMeta.entries()].sort(
    (a, b) => a[1].sortOrder - b[1].sortOrder || a[0].localeCompare(b[0]),
  );

  const rows: PaymentMethodMatrixRow[] = sortedCodes.map(([code, meta]) => {
    const byRegister: Record<string, RegisterMethodCell | null> = {};
    for (const register of registers) {
      const match = (methodsByRegisterId[register.id] ?? []).find((m) => m.code === code);
      byRegister[register.id] = match
        ? {
            definitionId: match.id,
            isActive: match.isActive,
            isDefault: match.isDefault,
            name: match.name,
            displayOrder: match.displayOrder,
            definition: match,
          }
        : null;
    }
    return { code, label: meta.label, sortOrder: meta.sortOrder, byRegister };
  });

  const summaries: PaymentMethodRegisterSummary[] = registers.map((register) => {
    const methods = methodsByRegisterId[register.id] ?? [];
    const active = methods.filter((m) => m.isActive).sort((a, b) => a.displayOrder - b.displayOrder);
    const inactive = methods.filter((m) => !m.isActive).sort((a, b) => a.displayOrder - b.displayOrder);
    return {
      registerId: register.id,
      registerNumber: register.registerNumber,
      location: register.location,
      activeCodes: active.map((m) => m.code),
      inactiveCodes: inactive.map((m) => m.code),
      defaultCode: active.find((m) => m.isDefault)?.code ?? null,
    };
  });

  return { rows, summaries };
}
