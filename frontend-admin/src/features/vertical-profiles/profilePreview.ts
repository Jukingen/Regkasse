export const VERTICAL_FEATURE_GROUPS = [
  { id: 'kitchen', features: ['kitchenDisplay'] },
  { id: 'tables', features: ['tables'] },
  { id: 'patientRecord', features: ['patientRecord'] },
  { id: 'appointment', features: ['appointment', 'serviceDuration'] },
  { id: 'imei', features: ['imeiTracking'] },
  { id: 'route', features: ['routeTracking'] },
  { id: 'ticket', features: ['ticketScan'] },
  { id: 'rooms', features: ['roomTracking'] },
] as const;

export const VERTICAL_LAYOUTS = [
  'standard',
  'tables',
  'appointment',
  'queue',
  'taxi',
  'ticket',
  'rooms',
] as const;

export const FIELD_ENTITIES = ['customer', 'product', 'order'] as const;

const FEATURE_PREVIEW: Record<string, { item: string; screen?: string; tab?: string }> = {
  kitchenDisplay: {
    item: 'admin.verticalProfiles.preview.items.kds',
    screen: 'admin.verticalProfiles.preview.screens.kds',
    tab: 'admin.verticalProfiles.preview.tabs.kitchen',
  },
  tables: {
    item: 'admin.verticalProfiles.preview.items.tables',
    screen: 'admin.verticalProfiles.preview.screens.tables',
  },
  patientRecord: {
    item: 'admin.verticalProfiles.preview.items.patientRecord',
    screen: 'admin.verticalProfiles.preview.screens.patient',
  },
  appointment: {
    item: 'admin.verticalProfiles.preview.items.appointment',
    screen: 'admin.verticalProfiles.preview.screens.appointment',
  },
  serviceDuration: {
    item: 'admin.verticalProfiles.preview.items.serviceDuration',
    tab: 'admin.verticalProfiles.preview.tabs.productDuration',
  },
  imeiTracking: {
    item: 'admin.verticalProfiles.preview.items.imei',
    screen: 'admin.verticalProfiles.preview.screens.imei',
    tab: 'admin.verticalProfiles.preview.tabs.imei',
  },
  routeTracking: {
    item: 'admin.verticalProfiles.preview.items.route',
    screen: 'admin.verticalProfiles.preview.screens.route',
  },
  ticketScan: {
    item: 'admin.verticalProfiles.preview.items.ticket',
    screen: 'admin.verticalProfiles.preview.screens.ticket',
    tab: 'admin.verticalProfiles.preview.tabs.tickets',
  },
  roomTracking: {
    item: 'admin.verticalProfiles.preview.items.rooms',
    screen: 'admin.verticalProfiles.preview.screens.rooms',
    tab: 'admin.verticalProfiles.preview.tabs.rooms',
  },
};

const LAYOUT_SCREENS: Record<string, string> = {
  standard: 'admin.verticalProfiles.preview.screens.standard',
  tables: 'admin.verticalProfiles.preview.screens.tables',
  appointment: 'admin.verticalProfiles.preview.screens.appointment',
  queue: 'admin.verticalProfiles.preview.screens.queue',
  taxi: 'admin.verticalProfiles.preview.screens.taxi',
  ticket: 'admin.verticalProfiles.preview.screens.ticket',
  rooms: 'admin.verticalProfiles.preview.screens.rooms',
};

export type ProfilePreview = {
  enabled: string[];
  screens: string[];
  tabs: string[];
};

export function buildProfilePreview(
  features: Record<string, boolean>,
  posLayout: string
): ProfilePreview {
  const enabled: string[] = [];
  const screens = new Set<string>();
  const tabs = new Set<string>();

  for (const [feature, on] of Object.entries(features)) {
    if (!on) continue;
    const preview = FEATURE_PREVIEW[feature];
    if (!preview) continue;
    enabled.push(preview.item);
    if (preview.screen) screens.add(preview.screen);
    if (preview.tab) tabs.add(preview.tab);
  }

  const layoutScreen = LAYOUT_SCREENS[posLayout];
  if (layoutScreen) screens.add(layoutScreen);

  return {
    enabled,
    screens: [...screens],
    tabs: [...tabs],
  };
}
