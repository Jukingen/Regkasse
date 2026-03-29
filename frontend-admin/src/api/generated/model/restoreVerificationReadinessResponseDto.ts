/**
 * Swagger-aligned type (orval regen may replace). Restore worker / lock readiness (HTTP does not run restore).
 */
export interface RestoreVerificationReadinessResponseDto {
  issues?: string[];
  level?: string;
  orchestratorDistributedLockEnabled?: boolean;
  scopeDisclaimer?: string;
  workerEnabled?: boolean;
}
