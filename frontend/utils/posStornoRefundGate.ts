/**
 * POS-only: who may open the Storno / Teilrückerstattung entry point next to "Zahlen".
 * Uses JWT permission claims (same ideas as backend payment.cancel / refund.create).
 */

export type PosStornoRefundGateUser = {
  role?: string;
  roles?: string[];
  permissions?: string[];
};

export function canShowPosStornoRefundButton(user: PosStornoRefundGateUser | null | undefined): boolean {
  if (!user) return false;
  if (user.role === 'SuperAdmin') return true;
  const roles = user.roles ?? [];
  if (roles.some((r) => r === 'SuperAdmin')) return true;

  const p = new Set((user.permissions ?? []).map((x) => x.toLowerCase()));
  return p.has('payment.cancel') && p.has('refund.create');
}
