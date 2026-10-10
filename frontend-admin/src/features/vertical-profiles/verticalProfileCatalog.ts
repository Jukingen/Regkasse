/**
 * Catalog defaults used when Super Admin simulates a profile.
 * Mirrors `VerticalProfileSeedData` feature flags and layouts. Tenant overrides
 * apply only to the live "actual" profile, not to a simulated catalog id.
 */
export type VerticalProfileCatalogEntry = {
  id: string;
  labelKey: string;
  posLayout: string;
  posFeatures: Record<string, boolean>;
};

const FALSE_FEATURES = {
  tables: false,
  kitchenDisplay: false,
  patientRecord: false,
  serviceDuration: false,
  appointment: false,
  imeiTracking: false,
  routeTracking: false,
  roomTracking: false,
  ticketScan: false,
} as const;

function features(
  overrides: Partial<Record<keyof typeof FALSE_FEATURES, boolean>>
): Record<string, boolean> {
  return { ...FALSE_FEATURES, ...overrides };
}

export const VERTICAL_PROFILE_CATALOG: readonly VerticalProfileCatalogEntry[] = [
  {
    id: 'gastronomy',
    labelKey: 'verticalProfiles.gastronomy.name',
    posLayout: 'standard',
    posFeatures: features({ kitchenDisplay: true }),
  },
  {
    id: 'gastronomy-tables',
    labelKey: 'verticalProfiles.gastronomyTables.name',
    posLayout: 'tables',
    posFeatures: features({ tables: true, kitchenDisplay: true }),
  },
  {
    id: 'hair-salon',
    labelKey: 'verticalProfiles.hairSalon.name',
    posLayout: 'appointment',
    posFeatures: features({ serviceDuration: true, appointment: true }),
  },
  {
    id: 'vet',
    labelKey: 'verticalProfiles.vet.name',
    posLayout: 'standard',
    posFeatures: features({ patientRecord: true }),
  },
  {
    id: 'mobile-services',
    labelKey: 'verticalProfiles.mobileServices.name',
    posLayout: 'appointment',
    posFeatures: features({ serviceDuration: true, appointment: true, routeTracking: true }),
  },
  {
    id: 'handy-shop',
    labelKey: 'verticalProfiles.handyShop.name',
    posLayout: 'standard',
    posFeatures: features({ imeiTracking: true }),
  },
  {
    id: 'taxi',
    labelKey: 'verticalProfiles.taxi.name',
    posLayout: 'taxi',
    posFeatures: features({ routeTracking: true }),
  },
  {
    id: 'ticket-sales',
    labelKey: 'verticalProfiles.ticketSales.name',
    posLayout: 'ticket',
    posFeatures: features({ ticketScan: true }),
  },
  {
    id: 'beherbergung',
    labelKey: 'verticalProfiles.beherbergung.name',
    posLayout: 'rooms',
    posFeatures: features({ kitchenDisplay: true, roomTracking: true }),
  },
];

const CATALOG_BY_ID = new Map(VERTICAL_PROFILE_CATALOG.map((entry) => [entry.id, entry]));

export function findVerticalProfileCatalogEntry(
  profileId: string
): VerticalProfileCatalogEntry | undefined {
  return CATALOG_BY_ID.get(profileId);
}
