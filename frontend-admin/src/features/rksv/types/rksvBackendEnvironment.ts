export type RksvBackendEnvironmentStatus = {
  environment: 'Demo' | 'Production' | string;
  isSimulated: boolean;
  showDemoLabel: boolean;
  tseStatusDisplay: string;
  tseStatusBadge: string;
  environmentDisplayName: string;
};

export function isRksvBackendDemo(status: RksvBackendEnvironmentStatus | null | undefined): boolean {
  if (!status) return false;
  return status.isSimulated;
}
