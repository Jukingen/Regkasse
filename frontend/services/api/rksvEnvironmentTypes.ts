export type RksvEnvironmentStatusDto = {
  environment: 'Demo' | 'Production' | string;
  isSimulated: boolean;
  showDemoLabel: boolean;
  tseStatusDisplay: string;
  tseStatusBadge: string;
  environmentDisplayName: string;
};

export function isRksvDemoEnvironment(status: RksvEnvironmentStatusDto | null | undefined): boolean {
  if (!status) return false;
  return status.isSimulated;
}
