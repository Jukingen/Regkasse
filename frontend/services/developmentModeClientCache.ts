export interface DevelopmentModeSettings {
  enabled: boolean;
  bypassLicense: boolean;
  bypassNtpCheck: boolean;
  bypassTseCheck: boolean;
  simulateOffline: boolean;
  forceOnline: boolean;
  validDays: number;
  features: string[];
}

let snapshot: DevelopmentModeSettings | null = null;

/** Updated by `useDevelopmentMode` after each successful poll. */
export function setDevelopmentModeClientSnapshot(data: DevelopmentModeSettings | null): void {
  snapshot = data;
}

export function getDevelopmentModeClientSnapshot(): DevelopmentModeSettings | null {
  return snapshot;
}
