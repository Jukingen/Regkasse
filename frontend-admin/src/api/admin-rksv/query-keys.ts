export const rksvAdminQueryKeys = {
  base: ['admin', 'rksv'] as const,
  environment: ['admin', 'rksv', 'environment'] as const,
  cashRegisters: ['admin', 'rksv', 'cash-registers'] as const,
  finanzOnline: {
    base: ['admin', 'rksv', 'finanzonline-reconciliation'] as const,
    list: (params: unknown) => ['admin', 'rksv', 'finanzonline-reconciliation', 'list', params] as const,
    metrics: ['admin', 'rksv', 'finanzonline-reconciliation', 'metrics'] as const,
  },
  finanzOnlineOutbox: {
    base: ['admin', 'rksv', 'finanzonline-outbox'] as const,
    list: (params: unknown) => ['admin', 'rksv', 'finanzonline-outbox', 'list', params] as const,
    detail: (id: string) => ['admin', 'rksv', 'finanzonline-outbox', 'detail', id] as const,
    readiness: () => ['admin', 'rksv', 'finanzonline-readiness'] as const,
  },
  finanzOnlineOps: {
    base: ['admin', 'rksv', 'finanzonline-ops'] as const,
    status: ['admin', 'rksv', 'finanzonline-ops', 'status'] as const,
    config: ['admin', 'rksv', 'finanzonline-ops', 'config'] as const,
    errors: ['admin', 'rksv', 'finanzonline-ops', 'errors'] as const,
    history: (invoiceId: string) => ['admin', 'rksv', 'finanzonline-ops', 'history', invoiceId] as const,
  },
  incident: (correlationId: string) => ['admin', 'rksv', 'incident', correlationId] as const,
  replayBatch: (correlationId: string) => ['admin', 'rksv', 'replay-batch', correlationId] as const,
  integrity: (params: unknown) => ['admin', 'rksv', 'integrity', params] as const,
  complianceReport: (params: unknown) => ['admin', 'rksv', 'compliance-report', params] as const,
  offlineIntentCoverage: {
    summary: (params: unknown) => ['admin', 'rksv', 'offline-intent-coverage', 'summary', params] as const,
    topRisk: (params: unknown) => ['admin', 'rksv', 'offline-intent-coverage', 'top-risk', params] as const,
  },
  offlinePayloadHash: {
    analyze: (params: unknown) => ['admin', 'rksv', 'offline-payload-hash', 'analyze', params] as const,
  },
  operations: {
    reminderOverview: ['rksv-reminder-overview'] as const,
    payloadAnalyzeQuick: (maxRows: number) => ['rksv-operations', 'payload-analyze', maxRows] as const,
    foMetrics: ['rksv-operations', 'fo-metrics'] as const,
    coverageSummary: (params: unknown) => ['rksv-operations', 'coverage', params] as const,
    summary: (params: unknown) => ['rksv-operations', 'summary', params] as const,
  },
};
