import type { AxiosRequestConfig } from 'axios';

import { AXIOS_INSTANCE } from '@/lib/axios';

export type DeploymentStage = 'staging' | 'canary' | 'production';

export type DeploymentStatus =
  | 'pending'
  | 'deploying'
  | 'smoke_running'
  | 'succeeded'
  | 'failed'
  | 'rolled_back'
  | 'canary_soak'
  | 'promoted';

export interface DeploymentRunDto {
  id: string;
  stage: DeploymentStage | string;
  status: DeploymentStatus | string;
  gitSha?: string | null;
  gitRef?: string | null;
  imageTag?: string | null;
  previousImageTag?: string | null;
  tenantIds: string[];
  errorMessage?: string | null;
  runUrl?: string | null;
  triggeredBy?: string | null;
  smokePassed?: boolean | null;
  smokeSummary?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface DeploymentRunListResponseDto {
  items: DeploymentRunDto[];
  total: number;
  latestByStage: Partial<Record<string, DeploymentRunDto | null>>;
}

export interface DeploymentRollbackResultDto {
  invoked: boolean;
  stage: string;
  previousImageTag?: string | null;
  message: string;
}

export interface TenantDeploymentHistoryDto {
  id: string;
  tenantId: string;
  tenantSlug?: string | null;
  tenantName?: string | null;
  version: string;
  previousVersion?: string | null;
  stage: string;
  status: string;
  gitSha?: string | null;
  runUrl?: string | null;
  triggeredBy?: string | null;
  errorMessage?: string | null;
  smokePassed?: boolean | null;
  deployedAtUtc: string;
  soakUntilUtc?: string | null;
  updatedAtUtc: string;
  isCanarySoaking: boolean;
}

export interface DeploymentOverallStatusDto {
  checkedAtUtc: string;
  tenants: TenantDeploymentHistoryDto[];
  canarySoakingCount: number;
  failedCount: number;
  recommendedNextCanaryTenantSlug?: string | null;
  strategyDoc?: string;
}

export async function fetchDeployments(
  params?: { stage?: string; take?: number },
  signal?: AbortSignal,
): Promise<DeploymentRunListResponseDto> {
  const config: AxiosRequestConfig = {
    signal,
    params: {
      stage: params?.stage || undefined,
      take: params?.take ?? 50,
    },
  };
  const { data } = await AXIOS_INSTANCE.get<DeploymentRunListResponseDto>(
    '/api/admin/deployments',
    config,
  );
  return data;
}

export async function fetchDeploymentOverallStatus(
  signal?: AbortSignal,
): Promise<DeploymentOverallStatusDto> {
  const { data } = await AXIOS_INSTANCE.get<DeploymentOverallStatusDto>(
    '/api/admin/deployments/status',
    { signal },
  );
  return data;
}

export async function fetchTenantDeployments(
  signal?: AbortSignal,
): Promise<TenantDeploymentHistoryDto[]> {
  const { data } = await AXIOS_INSTANCE.get<TenantDeploymentHistoryDto[]>(
    '/api/admin/deployments/tenants',
    { signal },
  );
  return data;
}

export async function requestDeploymentRollback(body: {
  stage: string;
  confirm: 'rollback';
  previousImageTag?: string | null;
}): Promise<DeploymentRollbackResultDto> {
  const { data } = await AXIOS_INSTANCE.post<DeploymentRollbackResultDto>(
    '/api/admin/deployments/rollback',
    body,
  );
  return data;
}

export async function requestTenantDeploymentRollback(
  tenantId: string,
  body: { confirm: 'rollback'; previousVersion?: string | null },
): Promise<DeploymentRollbackResultDto> {
  const { data } = await AXIOS_INSTANCE.post<DeploymentRollbackResultDto>(
    `/api/admin/deployments/tenants/${encodeURIComponent(tenantId)}/rollback`,
    body,
  );
  return data;
}

export interface DeploymentComplianceChecklist {
  depExportTested: boolean;
  tseSignatureTested: boolean;
  finanzOnlineTestSubmission: boolean;
  ntpTimeSyncChecked: boolean;
  tenantIsolationVerified: boolean;
}

export interface DeploymentComplianceSignoffDto {
  id: string;
  imageTag: string;
  gitSha?: string | null;
  stage: string;
  checklist: DeploymentComplianceChecklist;
  signedByUserId: string;
  signedByRole?: string | null;
  signedByDisplayName?: string | null;
  notes?: string | null;
  signedAtUtc: string;
  expiresAtUtc?: string | null;
  isValid: boolean;
}

export interface DeploymentComplianceGateStatusDto {
  checkedAtUtc: string;
  imageTag: string;
  stage: string;
  signoffPresent: boolean;
  signoffValid: boolean;
  checklistComplete: boolean;
  gatePassed: boolean;
  latestSignoff?: DeploymentComplianceSignoffDto | null;
  missingChecklistItems: string[];
  strategyDoc?: string;
}

export async function fetchComplianceGate(
  imageTag: string,
  stage = 'production',
  signal?: AbortSignal,
): Promise<DeploymentComplianceGateStatusDto> {
  const { data } = await AXIOS_INSTANCE.get<DeploymentComplianceGateStatusDto>(
    '/api/admin/deployments/compliance/gate',
    { signal, params: { imageTag, stage } },
  );
  return data;
}

export async function submitComplianceSignoff(body: {
  imageTag: string;
  stage?: string;
  notes?: string;
  checklist: DeploymentComplianceChecklist;
  gitSha?: string;
  validHours?: number;
}): Promise<DeploymentComplianceSignoffDto> {
  const { data } = await AXIOS_INSTANCE.post<DeploymentComplianceSignoffDto>(
    '/api/admin/deployments/compliance/signoff',
    body,
  );
  return data;
}
