export const billingQueryKeys = {
  all: ['billing'] as const,

  sales: () => [...billingQueryKeys.all, 'sales'] as const,
  salesList: (params?: Record<string, unknown>) =>
    [...billingQueryKeys.sales(), 'list', params] as const,
  salesDetail: (id: string) => [...billingQueryKeys.sales(), 'detail', id] as const,
  salesByKey: (key: string) => [...billingQueryKeys.sales(), 'byKey', key] as const,

  stats: () => [...billingQueryKeys.all, 'stats'] as const,
  statsRange: (from?: string, to?: string) => [...billingQueryKeys.stats(), { from, to }] as const,

  expiring: (days?: number) => [...billingQueryKeys.all, 'expiring', { days }] as const,

  tenantLicense: (tenantId: string) =>
    [...billingQueryKeys.all, 'tenant', tenantId, 'license'] as const,

  audit: () => [...billingQueryKeys.all, 'audit'] as const,
  auditList: (params?: Record<string, unknown>) =>
    [...billingQueryKeys.audit(), 'list', params] as const,
  auditForSale: (saleId: string) => [...billingQueryKeys.audit(), 'sale', saleId] as const,

  reminders: (tenantId: string) =>
    [...billingQueryKeys.all, 'tenant', tenantId, 'reminders'] as const,
};
