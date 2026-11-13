/**
 * Profile-specific first-login slides and the login industry line.
 * POS copy lives in the verticalProfiles i18n namespace (de/en/tr).
 */
import { secureStorage } from '../secureStorage';

/** One device flag. Set after the cashier finishes or skips the deck. */
export const POS_ONBOARDING_SEEN_KEY = 'pos_onboarding_seen_v1';

/**
 * Gastronomy cashiers already land on the register. The deck is not useful there.
 * Other profiles (taxi, rooms, vet, …) keep their slides. The seen-flag stays unset
 * so a later login on another profile on the same device still shows that deck.
 */
const ONBOARDING_SKIP_PROFILE_IDS = new Set(['gastronomy', 'gastronomy-tables']);

/** Cache key shared with VerticalProfileContext (`vertical_profile_v1_*`). */
export function verticalProfileCacheKey(scope: string): string {
  return `vertical_profile_v1_${scope.replace(/[^a-zA-Z0-9_-]/g, '_')}`;
}

export function shouldSkipPosOnboarding(profileId: string | null | undefined): boolean {
  const id = profileId?.trim();
  return !!id && ONBOARDING_SKIP_PROFILE_IDS.has(id);
}

export type OnboardingSlide = {
  id: string;
  titleKey: string;
  bodyKey: string;
  previewKey: string;
};

const DEFAULT_SLIDES: OnboardingSlide[] = [
  {
    id: 'welcome',
    titleKey: 'onboarding.default.welcomeTitle',
    bodyKey: 'onboarding.default.welcomeBody',
    previewKey: 'onboarding.default.salePreview',
  },
  {
    id: 'sale',
    titleKey: 'onboarding.default.saleTitle',
    bodyKey: 'onboarding.default.saleBody',
    previewKey: 'onboarding.default.salePreview',
  },
];

const PROFILE_SLIDES: Record<string, OnboardingSlide[]> = {
  vet: [
    {
      id: 'welcome',
      titleKey: 'onboarding.vet.welcomeTitle',
      bodyKey: 'onboarding.vet.welcomeBody',
      previewKey: 'onboarding.vet.recordPreview',
    },
    {
      id: 'record',
      titleKey: 'onboarding.vet.recordTitle',
      bodyKey: 'onboarding.vet.recordBody',
      previewKey: 'onboarding.vet.recordPreview',
    },
    {
      id: 'sale',
      titleKey: 'onboarding.vet.saleTitle',
      bodyKey: 'onboarding.vet.saleBody',
      previewKey: 'onboarding.vet.salePreview',
    },
  ],
  'hair-salon': [
    {
      id: 'welcome',
      titleKey: 'onboarding.hairSalon.welcomeTitle',
      bodyKey: 'onboarding.hairSalon.welcomeBody',
      previewKey: 'onboarding.hairSalon.calendarPreview',
    },
    {
      id: 'calendar',
      titleKey: 'onboarding.hairSalon.calendarTitle',
      bodyKey: 'onboarding.hairSalon.calendarBody',
      previewKey: 'onboarding.hairSalon.calendarPreview',
    },
    {
      id: 'service',
      titleKey: 'onboarding.hairSalon.serviceTitle',
      bodyKey: 'onboarding.hairSalon.serviceBody',
      previewKey: 'onboarding.hairSalon.servicePreview',
    },
  ],
  taxi: [
    {
      id: 'welcome',
      titleKey: 'onboarding.taxi.welcomeTitle',
      bodyKey: 'onboarding.taxi.welcomeBody',
      previewKey: 'onboarding.taxi.tripPreview',
    },
    {
      id: 'trip',
      titleKey: 'onboarding.taxi.tripTitle',
      bodyKey: 'onboarding.taxi.tripBody',
      previewKey: 'onboarding.taxi.tripPreview',
    },
    {
      id: 'tariff',
      titleKey: 'onboarding.taxi.tariffTitle',
      bodyKey: 'onboarding.taxi.tariffBody',
      previewKey: 'onboarding.taxi.tariffPreview',
    },
  ],
  'handy-shop': [
    {
      id: 'welcome',
      titleKey: 'onboarding.handyShop.welcomeTitle',
      bodyKey: 'onboarding.handyShop.welcomeBody',
      previewKey: 'onboarding.handyShop.imeiPreview',
    },
    {
      id: 'imei',
      titleKey: 'onboarding.handyShop.imeiTitle',
      bodyKey: 'onboarding.handyShop.imeiBody',
      previewKey: 'onboarding.handyShop.imeiPreview',
    },
  ],
  'mobile-services': [
    {
      id: 'welcome',
      titleKey: 'onboarding.mobileServices.welcomeTitle',
      bodyKey: 'onboarding.mobileServices.welcomeBody',
      previewKey: 'onboarding.mobileServices.routePreview',
    },
    {
      id: 'route',
      titleKey: 'onboarding.mobileServices.routeTitle',
      bodyKey: 'onboarding.mobileServices.routeBody',
      previewKey: 'onboarding.mobileServices.routePreview',
    },
  ],
  'ticket-sales': [
    {
      id: 'welcome',
      titleKey: 'onboarding.ticketSales.welcomeTitle',
      bodyKey: 'onboarding.ticketSales.welcomeBody',
      previewKey: 'onboarding.ticketSales.scanPreview',
    },
    {
      id: 'scan',
      titleKey: 'onboarding.ticketSales.scanTitle',
      bodyKey: 'onboarding.ticketSales.scanBody',
      previewKey: 'onboarding.ticketSales.scanPreview',
    },
  ],
  beherbergung: [
    {
      id: 'welcome',
      titleKey: 'onboarding.lodging.welcomeTitle',
      bodyKey: 'onboarding.lodging.welcomeBody',
      previewKey: 'onboarding.lodging.roomsPreview',
    },
    {
      id: 'rooms',
      titleKey: 'onboarding.lodging.roomsTitle',
      bodyKey: 'onboarding.lodging.roomsBody',
      previewKey: 'onboarding.lodging.roomsPreview',
    },
  ],
};

const INDUSTRY_KEYS: Record<string, string> = {
  vet: 'onboarding.industries.vet',
  doctor: 'onboarding.industries.doctor',
  'hair-salon': 'onboarding.industries.hairSalon',
  taxi: 'onboarding.industries.taxi',
  gastronomy: 'onboarding.industries.gastronomy',
  'gastronomy-tables': 'onboarding.industries.gastronomyTables',
  'handy-shop': 'onboarding.industries.handyShop',
  'mobile-services': 'onboarding.industries.mobileServices',
  'ticket-sales': 'onboarding.industries.ticketSales',
  beherbergung: 'onboarding.industries.lodging',
};

export function buildOnboardingSlides(profileId: string | null | undefined): OnboardingSlide[] {
  const id = profileId?.trim();
  if (!id) return DEFAULT_SLIDES;
  if (shouldSkipPosOnboarding(id)) return [];
  return PROFILE_SLIDES[id] ?? DEFAULT_SLIDES;
}

/** i18n key under the verticalProfiles namespace, or null for the generic POS subtitle. */
export function industryLabelKey(profileId: string | null | undefined): string | null {
  const id = profileId?.trim();
  if (!id) return null;
  return INDUSTRY_KEYS[id] ?? null;
}

export function loginIndustrySubtitle(
  profileId: string | null | undefined,
  translateIndustry: (key: string) => string,
  translateBrand: (key: string, options: { industry: string }) => string
): string | null {
  const key = industryLabelKey(profileId);
  if (!key) return null;
  const industry = translateIndustry(key).trim();
  if (!industry) return null;
  return translateBrand('brandIndustry', { industry });
}

export async function hasSeenPosOnboarding(): Promise<boolean> {
  return (await secureStorage.getItem(POS_ONBOARDING_SEEN_KEY)) === '1';
}

export async function markPosOnboardingSeen(): Promise<void> {
  await secureStorage.setItem(POS_ONBOARDING_SEEN_KEY, '1');
}
