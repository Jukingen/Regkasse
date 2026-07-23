export type TseDeviceFleetStatus = 'Active' | 'Degraded' | 'Inactive' | 'Expired';

export interface TseDeviceFleetItem {
  id: string;
  serialNumber: string;
  deviceType: string;
  cashRegisterId: string;
  cashRegisterNumber?: string | null;
  tenantId?: string | null;
  tenantName?: string | null;
  tenantSlug?: string | null;
  status: TseDeviceFleetStatus | string;
  isConnected: boolean;
  canCreateInvoices: boolean;
  isActive: boolean;
  certificateStatus: string;
  memoryStatus: string;
  errorMessage?: string | null;
  healthScore: number;
  createdAt: string;
  lastConnectionTime: string;
  lastSignatureTime: string;
}

export interface TseFleetOverview {
  totalDevices: number;
  activeDevices: number;
  degradedDevices: number;
  inactiveDevices: number;
  expiredCertificateDevices: number;
  processHealthScore: number;
  processHealthStatus: string;
  processLastCheckUtc?: string | null;
  processLastErrorSafe?: string | null;
  tseMode: string;
  signingMode: string;
  devices: TseDeviceFleetItem[];
}

export interface ProvisionTseRequest {
  tenantId?: string;
  cashRegisterId?: string;
}

export interface ProvisionTseResponse {
  success: boolean;
  outcome: string;
  error?: string | null;
  detail?: string | null;
  deviceId?: string | null;
  serialNumber?: string | null;
  cashRegisterId?: string | null;
}

export interface TseBackupListItem {
  id: string;
  tenantId: string;
  tenantName?: string | null;
  tenantSlug?: string | null;
  createdAt: string;
  createdBy?: string | null;
  deviceCount: number;
  chainCount: number;
  receiptSequenceCount: number;
  encryptionKind: string;
  schemaVersion: number;
  notes?: string | null;
}

export interface CreateTseBackupRequest {
  tenantId: string;
  notes?: string;
}

export interface CreateTseBackupResponse {
  success: boolean;
  backupId?: string | null;
  error?: string | null;
  backup?: TseBackupListItem | null;
}

export interface TseBackupRestorePreview {
  backupId: string;
  tenantId: string;
  backupCreatedAt: string;
  backupDeviceCount: number;
  backupChainCount: number;
  liveDeviceCount: number;
  liveChainCount: number;
  warnings: string[];
  wouldRequireForceDowngrade: boolean;
  cryptoMaterialNote: string;
}

export interface RestoreTseBackupRequest {
  confirmToken: string;
  forceChainDowngrade?: boolean;
}

export interface RestoreTseBackupResponse {
  success: boolean;
  error?: string | null;
  detail?: string | null;
  devicesUpserted: number;
  chainsUpserted: number;
  chainsSkipped: number;
  receiptSequencesUpserted: number;
  warnings: string[];
}

export type TseCertLifecycleStatus =
  | 'Valid'
  | 'ExpiringSoon'
  | 'Expired'
  | 'Revoked'
  | 'Invalid'
  | string;

export interface TseCertificateWarning {
  code: string;
  severity: string;
  message: string;
}

export interface TseCertificateInfo {
  deviceRowId: string;
  vendorDeviceId?: string | null;
  serialNumber: string;
  certificateSerialNumber?: string | null;
  thumbprint?: string | null;
  issuer?: string | null;
  subject?: string | null;
  issuedAt?: string | null;
  expiresAt?: string | null;
  timeUntilExpiryDays?: number | null;
  isExpired: boolean;
  isRevoked: boolean;
  status: TseCertLifecycleStatus;
  source?: string | null;
  scheduledRenewalAt?: string | null;
  warnings: TseCertificateWarning[];
}

export interface TseCertificateValidationResult {
  isValid: boolean;
  status: TseCertLifecycleStatus;
  message?: string | null;
  certificate?: TseCertificateInfo | null;
  errors: string[];
}

export interface TseCertificateRenewalResult {
  success: boolean;
  outcome: string;
  message?: string | null;
  certificate?: TseCertificateInfo | null;
}
