import { customInstance } from '@/lib/axios';

import type {
  CreateTseBackupRequest,
  CreateTseBackupResponse,
  ProvisionTseRequest,
  ProvisionTseResponse,
  RestoreTseBackupRequest,
  RestoreTseBackupResponse,
  TseBackupListItem,
  TseBackupRestorePreview,
  TseCertificateInfo,
  TseCertificateRenewalResult,
  TseCertificateValidationResult,
  TseDeviceFleetItem,
  TseFleetOverview,
} from '../types';

export async function getTseFleetOverview(signal?: AbortSignal): Promise<TseFleetOverview> {
  return customInstance<TseFleetOverview>({
    url: '/api/admin/tse-management',
    method: 'GET',
    signal,
  });
}

export async function getTseDevices(signal?: AbortSignal): Promise<TseDeviceFleetItem[]> {
  return customInstance<TseDeviceFleetItem[]>({
    url: '/api/admin/tse-management/devices',
    method: 'GET',
    signal,
  });
}

export async function provisionTse(
  body: ProvisionTseRequest,
  signal?: AbortSignal
): Promise<ProvisionTseResponse> {
  return customInstance<ProvisionTseResponse>({
    url: '/api/admin/tse-management/provision',
    method: 'POST',
    data: body,
    signal,
  });
}

export async function revokeTse(deviceId: string, signal?: AbortSignal): Promise<ProvisionTseResponse> {
  return customInstance<ProvisionTseResponse>({
    url: `/api/admin/tse-management/devices/${deviceId}/revoke`,
    method: 'POST',
    signal,
  });
}

export async function createTseBackup(
  body: CreateTseBackupRequest,
  signal?: AbortSignal
): Promise<CreateTseBackupResponse> {
  return customInstance<CreateTseBackupResponse>({
    url: '/api/admin/tse-management/backups',
    method: 'POST',
    data: body,
    signal,
  });
}

export async function listTseBackups(
  tenantId?: string,
  signal?: AbortSignal
): Promise<TseBackupListItem[]> {
  return customInstance<TseBackupListItem[]>({
    url: '/api/admin/tse-management/backups',
    method: 'GET',
    params: tenantId ? { tenantId } : undefined,
    signal,
  });
}

export async function previewTseBackupRestore(
  backupId: string,
  signal?: AbortSignal
): Promise<TseBackupRestorePreview> {
  return customInstance<TseBackupRestorePreview>({
    url: `/api/admin/tse-management/backups/${backupId}/preview`,
    method: 'GET',
    signal,
  });
}

export async function restoreTseBackup(
  backupId: string,
  body: RestoreTseBackupRequest,
  signal?: AbortSignal
): Promise<RestoreTseBackupResponse> {
  return customInstance<RestoreTseBackupResponse>({
    url: `/api/admin/tse-management/backups/${backupId}/restore`,
    method: 'POST',
    data: body,
    signal,
  });
}

export async function getTseCertificate(
  deviceId: string,
  signal?: AbortSignal
): Promise<TseCertificateInfo> {
  return customInstance<TseCertificateInfo>({
    url: `/api/admin/tse-management/devices/${deviceId}/certificate`,
    method: 'GET',
    signal,
  });
}

export async function validateTseCertificate(
  deviceId: string,
  signal?: AbortSignal
): Promise<TseCertificateValidationResult> {
  return customInstance<TseCertificateValidationResult>({
    url: `/api/admin/tse-management/devices/${deviceId}/certificate/validate`,
    method: 'POST',
    signal,
  });
}

export async function renewTseCertificate(
  deviceId: string,
  signal?: AbortSignal
): Promise<TseCertificateRenewalResult> {
  return customInstance<TseCertificateRenewalResult>({
    url: `/api/admin/tse-management/devices/${deviceId}/certificate/renew`,
    method: 'POST',
    signal,
  });
}

export async function scheduleTseCertificateRenewal(
  deviceId: string,
  renewalDateUtc: string,
  signal?: AbortSignal
): Promise<TseCertificateRenewalResult> {
  return customInstance<TseCertificateRenewalResult>({
    url: `/api/admin/tse-management/devices/${deviceId}/certificate/schedule-renewal`,
    method: 'POST',
    data: { renewalDateUtc },
    signal,
  });
}

export type TseSimulatorFailureType =
  | 'NetworkTimeout'
  | 'ConnectionLost'
  | 'CertificateInvalid'
  | 'SignatureError'
  | 'RateLimitExceeded'
  | 'InternalServerError';

export interface TseSimulationResult {
  success: boolean;
  error?: string | null;
  deviceId: string;
  scenarioId?: string | null;
  message: string;
  device?: {
    id: string;
    serialNumber: string;
    isConnected: boolean;
    canCreateInvoices: boolean;
    certificateStatus: string;
    expiresAt?: string | null;
    errorMessage?: string | null;
    healthScore: number;
    healthStatus: string;
    simulatedLatencyMs: number;
    activeScenarioId?: string | null;
  } | null;
}

export async function simulateTseFailure(
  deviceId: string,
  failureType: TseSimulatorFailureType,
  signal?: AbortSignal
): Promise<TseSimulationResult> {
  return customInstance<TseSimulationResult>({
    url: `/api/admin/tse-management/simulator/devices/${deviceId}/failure`,
    method: 'POST',
    data: { failureType },
    signal,
  });
}

export async function simulateTseLatency(
  deviceId: string,
  latencyMs: number,
  signal?: AbortSignal
): Promise<TseSimulationResult> {
  return customInstance<TseSimulationResult>({
    url: `/api/admin/tse-management/simulator/devices/${deviceId}/latency`,
    method: 'POST',
    data: { latencyMs },
    signal,
  });
}

export async function simulateTseCertificateExpiry(
  deviceId: string,
  expiryDateUtc: string,
  signal?: AbortSignal
): Promise<TseSimulationResult> {
  return customInstance<TseSimulationResult>({
    url: `/api/admin/tse-management/simulator/devices/${deviceId}/certificate-expiry`,
    method: 'POST',
    data: { expiryDateUtc },
    signal,
  });
}

export async function resetTseSimulation(
  deviceId: string,
  signal?: AbortSignal
): Promise<TseSimulationResult> {
  return customInstance<TseSimulationResult>({
    url: `/api/admin/tse-management/simulator/devices/${deviceId}/reset`,
    method: 'POST',
    signal,
  });
}
