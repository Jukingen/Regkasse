import { registerApiErrorCodeTranslation } from '@/shared/errors/apiErrorCodeRegistry';

let registered = false;

/** Map ticket seat save API `code` values to FA i18n keys. */
export function ensureTicketApiErrorTranslations(): void {
  if (registered) return;
  registered = true;
  registerApiErrorCodeTranslation('SEAT_TAKEN', 'products.ticketEvents.seatTaken');
  registerApiErrorCodeTranslation(
    'EVENT_CAPACITY_REACHED',
    'products.ticketEvents.capacityReached'
  );
}
