/** Seat-board notices. Known API `code` values map to i18n; other notices are already translated. */
const KNOWN_TICKET_SEAT_NOTICES: ReadonlySet<string> = new Set([
  'SEAT_TAKEN',
  'CATEGORY_CAPACITY_REACHED',
  'CATEGORY_PRICE_MISSING',
  'COMPANION_TARGET_NOT_SOLD',
]);

export function readTicketSeatErrorCode(error: unknown): string | null {
  if (!error || typeof error !== 'object') return null;
  const body = error as {
    status?: number;
    data?: { code?: string };
    response?: { status?: number; data?: { code?: string } };
  };
  const status = body.status ?? body.response?.status;
  const code = body.data?.code ?? body.response?.data?.code;
  if (status !== 400 && status !== 409) return null;
  return typeof code === 'string' && KNOWN_TICKET_SEAT_NOTICES.has(code) ? code : null;
}

/**
 * Static keys (not a dynamic template) so the i18n usage checker can validate them.
 * Unknown notices are returned as-is (already translated text) and never reach `t`.
 */
export function ticketSeatToastMessage(t: (key: string) => string, notice: string): string {
  switch (notice) {
    case 'SEAT_TAKEN':
      return t('verticalProfiles:screens.tickets.seatTakenHold');
    case 'CATEGORY_CAPACITY_REACHED':
      return t('verticalProfiles:screens.tickets.seatCategoryCapacity');
    case 'CATEGORY_PRICE_MISSING':
      return t('verticalProfiles:screens.tickets.seatCategoryPriceMissing');
    case 'COMPANION_TARGET_NOT_SOLD':
      return t('verticalProfiles:screens.tickets.companionTargetNotSold');
    default:
      return notice;
  }
}
