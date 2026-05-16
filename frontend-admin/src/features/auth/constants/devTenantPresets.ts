/** Preset tenant slugs for local development (X-Tenant-Id / ?tenant= / *.regkasse.local). */
export const DEV_TENANT_PRESETS = [
  { value: 'dev', label: 'Development (dev.regkasse.local)' },
  { value: 'cafe', label: 'Test Cafe (cafe.regkasse.local)' },
  { value: 'bar', label: 'Test Bar (bar.regkasse.local)' },
] as const;

export type DevTenantPresetValue = (typeof DEV_TENANT_PRESETS)[number]['value'];
