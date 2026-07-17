/**
 * Backup storage-costs API — GET /api/admin/backup/storage-costs
 */

import { customInstance } from "@/lib/axios";

export const BACKUP_STORAGE_COSTS_PATH = "/api/admin/backup/storage-costs" as const;

export function getBackupStorageCostsQueryKey() {
  return [BACKUP_STORAGE_COSTS_PATH] as const;
}

export type BackupStorageTierCostRowDto = {
  name: string;
  sizeGb: number;
  costEur: number;
  access: string;
  retention: string;
  artifactCount: number;
};

export type BackupStorageCostRecommendationDto = {
  code: string;
  title: string;
  description: string;
  savingsPercent: number;
};

export type BackupStorageCostResponseDto = {
  totalStorageGb: number;
  budgetGb: number;
  usagePercentage: number;
  monthlyCostEur: number;
  costPerGbEur: number;
  backupCount: number;
  averageSizeMb: number;
  retentionSavingsPercent: number;
  projectedMonthlyEur: number;
  smartRetentionEnabled: boolean;
  storageTierManagementEnabled: boolean;
  disclaimer?: string;
  tiers: BackupStorageTierCostRowDto[];
  recommendations: BackupStorageCostRecommendationDto[];
};

export async function getBackupStorageCosts(): Promise<BackupStorageCostResponseDto> {
  return customInstance<BackupStorageCostResponseDto>({
    url: BACKUP_STORAGE_COSTS_PATH,
    method: "GET",
  });
}
