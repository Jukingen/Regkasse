export type RksvBackendEnvironmentStatus = {
  environment: 'Demo' | 'Production' | string;
  isSimulated: boolean;
  showDemoLabel: boolean;
  tseStatusDisplay: string;
  tseStatusBadge: string;
  environmentDisplayName: string;
  /** ASP.NET Core host name (Development / Staging / Production). */
  hostEnvironment: string;
  /** True when ASPNETCORE_ENVIRONMENT=Development. */
  isHostDevelopment: boolean;
  /** True when ASPNETCORE_ENVIRONMENT=Staging. */
  isHostStaging: boolean;
  /** Release stage: dev | staging | canary | production. */
  releaseStage: string;
  /** True when effective release stage is canary (deploy or canary tenant). */
  isCanary: boolean;
  /** True when FinanzOnline UseSimulation (or Mode=Simulation) is active. */
  isFinanzOnlineSimulated: boolean;
  /** Composite simulation banner signal (TSE/RKSV and/or FON). */
  isSimulationMode: boolean;
  /** False when Production/Staging TSE fiscal config violates the lock (or escape hatch is on with violations still listed). */
  fiscalConfigLockOk: boolean;
  fiscalConfigLockEscapeHatchActive: boolean;
  fiscalConfigLockReasons: string[];
};

export function isRksvBackendDemo(
  status: RksvBackendEnvironmentStatus | null | undefined
): boolean {
  if (!status) return false;
  return status.isSimulated;
}

/** True when FA should show a critical/unsafe TSE production lock banner. */
export function isTseFiscalConfigLockUnsafe(
  status: RksvBackendEnvironmentStatus | null | undefined
): boolean {
  if (!status) return false;
  return status.fiscalConfigLockOk === false || status.fiscalConfigLockEscapeHatchActive === true;
}
