import type { LicensePublicStatusDto } from '../../api/license';
import type { PosCashRegisterContextDto } from '../../utils/posCashRegisterReadinessParse';
import type { RksvEnvironmentStatusDto } from './rksvEnvironmentTypes';

export type PosStatusLicenseHealthDto = {
  isValid: boolean;
  isTrial: boolean;
  isExpired: boolean;
  daysRemaining: number;
  expiryDate: string | null;
  machineHash: string;
};

export type PosStatusSettingsSnapshotDto = {
  cashRegisterId: string | null;
  settingsVersion: number;
  updatedAtUtc: string;
};

export type PosStatusOverviewDto = {
  serverTimeUtc: string;
  license: LicensePublicStatusDto;
  healthLicense: PosStatusLicenseHealthDto;
  cashRegister: PosCashRegisterContextDto;
  settings: PosStatusSettingsSnapshotDto;
  rksvEnvironment: RksvEnvironmentStatusDto | null;
};
