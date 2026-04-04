/**
 * Extension contracts for reservations, KDS / Expo customer display, and third-party integrations.
 * No runtime wiring yet — `registerHospitalityExtensions` is unused; dashboard shortcuts live in
 * `HospitalityQuickLinksCard` (permissions + hardcoded links), not this registry.
 */

/** Reservation origin (e.g. OpenTable, phone, walk-in). */
export type ReservationSource = 'walk_in' | 'phone' | 'web' | 'partner' | 'unknown';

export interface ReservationUpsertPayload {
  id?: string;
  partySize: number;
  scheduledAtUtc: string;
  /** Table or zone reference within the tenant. */
  tableRef?: string | null;
  notes?: string | null;
  source: ReservationSource;
}

export interface ReservationExtension {
  /**
   * Unique plugin id (e.g. "acme-reservations").
   * On conflict, last registered wins.
   */
  readonly id: string;
  /** Optional: when a reservation is created or updated. */
  onReservationCommitted?: (payload: ReservationUpsertPayload) => void | Promise<void>;
}

export type KdsOrderTicketEvent = {
  orderId: string;
  /** Kitchen station or expo line. */
  stationId?: string;
  firedAtUtc: string;
};

export interface KdsExtension {
  readonly id: string;
  onTicketFired?: (event: KdsOrderTicketEvent) => void | Promise<void>;
}

export type CustomerDisplayPayload = {
  orderId: string;
  /** Short copy for the customer-facing display (GDPR-conscious). */
  headline: string;
  locale?: string;
};

export interface CustomerDisplayExtension {
  readonly id: string;
  renderCustomerDisplay?: (payload: CustomerDisplayPayload) => void | Promise<void>;
}

export type HospitalityExtensionBundle = {
  reservations?: ReservationExtension[];
  kds?: KdsExtension[];
  customerDisplay?: CustomerDisplayExtension[];
};

const registry: HospitalityExtensionBundle = {};

export function registerHospitalityExtensions(bundle: HospitalityExtensionBundle): void {
  if (bundle.reservations?.length) {
    registry.reservations = [...(registry.reservations ?? []), ...bundle.reservations];
  }
  if (bundle.kds?.length) {
    registry.kds = [...(registry.kds ?? []), ...bundle.kds];
  }
  if (bundle.customerDisplay?.length) {
    registry.customerDisplay = [...(registry.customerDisplay ?? []), ...bundle.customerDisplay];
  }
}

export function getHospitalityExtensionRegistry(): Readonly<HospitalityExtensionBundle> {
  return registry;
}
