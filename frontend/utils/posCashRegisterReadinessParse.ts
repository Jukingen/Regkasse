/** Normalized POST /api/pos/cash-register/ensure-ready body (no HTTP deps — safe for Jest). */

export type PosCashRegisterContextDto = {
  /** Echo of persisted UserSettings preference (GUID); not payment authorization by itself. */
  preferredRegisterId?: string | null;
  effectiveRegisterId?: string | null;
  resolution?: string;
  registerStatus?: string | null;
  autoOpened?: boolean;
  nextAction?: string;
  messageCode?: string;
};

function isRecord(v: unknown): v is Record<string, unknown> {
  return v != null && typeof v === 'object' && !Array.isArray(v);
}

function pickString(
  r: Record<string, unknown>,
  camel: string,
  pascal: string
): string | null | undefined {
  const v = r[camel] ?? r[pascal];
  if (v == null) return v;
  const s = String(v).trim();
  return s.length ? s : null;
}

export function parsePosCashRegisterContextDto(body: unknown): PosCashRegisterContextDto {
  if (!isRecord(body)) {
    return {};
  }
  return {
    preferredRegisterId: pickString(body, 'preferredRegisterId', 'PreferredRegisterId'),
    effectiveRegisterId: pickString(body, 'effectiveRegisterId', 'EffectiveRegisterId'),
    resolution: pickString(body, 'resolution', 'Resolution') ?? 'none',
    registerStatus: pickString(body, 'registerStatus', 'RegisterStatus'),
    autoOpened: Boolean(body.autoOpened ?? body.AutoOpened),
    nextAction: pickString(body, 'nextAction', 'NextAction') ?? 'none',
    messageCode: pickString(body, 'messageCode', 'MessageCode') ?? '',
  };
}
