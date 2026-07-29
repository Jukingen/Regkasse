export type RksvEnvironmentStatusDto = {
  environment: 'Demo' | 'Production' | string;
  isSimulated: boolean;
  showDemoLabel: boolean;
  tseStatusDisplay: string;
  tseStatusBadge: string;
  environmentDisplayName: string;
  hostEnvironment?: string;
  isHostDevelopment?: boolean;
  isHostStaging?: boolean;
  releaseStage?: string;
  isCanary?: boolean;
  isFinanzOnlineSimulated?: boolean;
  isSimulationMode?: boolean;
};

export function isRksvDemoEnvironment(
  status: RksvEnvironmentStatusDto | null | undefined
): boolean {
  if (!status) return false;
  return status.isSimulated;
}

export function isFiscalSimulationMode(
  status: RksvEnvironmentStatusDto | null | undefined
): boolean {
  if (!status) return false;
  return (
    status.isSimulationMode === true ||
    status.isSimulated === true ||
    status.isFinanzOnlineSimulated === true
  );
}
