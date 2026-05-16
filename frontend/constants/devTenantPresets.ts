/** Preset tenant slugs for local development (backend subdomain / ?tenant= routing). */
export const DEV_TENANT_PRESETS = [
  { value: 'dev', label: 'Entwicklungsmandant' },
  { value: 'test_cafe', label: 'Test Café' },
  { value: 'test_bar', label: 'Test Bar' },
] as const;

export type DevTenantPresetValue = (typeof DEV_TENANT_PRESETS)[number]['value'];
