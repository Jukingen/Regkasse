/** Seat-board notices. Known API `code` values map to i18n; other notices are already translated. */
const TICKET_SEAT_NOTICE_KEYS: Record<string, string> = {
  SEAT_TAKEN: 'screens.tickets.seatTakenHold',
  CATEGORY_CAPACITY_REACHED: 'screens.tickets.seatCategoryCapacity',
  CATEGORY_PRICE_MISSING: 'screens.tickets.seatCategoryPriceMissing',
  COMPANION_TARGET_NOT_SOLD: 'screens.tickets.companionTargetNotSold',
};

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
  return typeof code === 'string' && code in TICKET_SEAT_NOTICE_KEYS ? code : null;
}

export function ticketSeatToastMessage(t: (key: string) => string, notice: string): string {
  const key = TICKET_SEAT_NOTICE_KEYS[notice];
  return key ? t(key) : notice;
}
