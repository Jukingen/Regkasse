export type TseResourcePoolType = 'Shared' | 'Dedicated' | 'Hybrid' | string;

export interface TsePoolRule {
  id: string;
  ruleType: string;
  ruleValue?: string | null;
  description?: string | null;
  isEnabled: boolean;
}

export interface TsePoolTenantSummary {
  tenantId: string;
  tenantName?: string | null;
  tenantSlug?: string | null;
  reservedCapacity: number;
  assignedAt: string;
}

export interface TseResourcePool {
  id: string;
  name: string;
  type: TseResourcePoolType;
  totalCapacity: number;
  usedCapacity: number;
  availableCapacity: number;
  isActive: boolean;
  description?: string | null;
  assignedTenants: string[];
  tenantSummaries: TsePoolTenantSummary[];
  rules: TsePoolRule[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateTseResourcePoolRequest {
  name: string;
  type: TseResourcePoolType;
  totalCapacity: number;
  description?: string;
}

export interface AssignTenantToTsePoolRequest {
  tenantId: string;
  poolId: string;
  reservedCapacity?: number;
}

export interface TsePoolAssignmentResult {
  success: boolean;
  message: string;
  poolId?: string | null;
  tenantId?: string | null;
  previousPoolId?: string | null;
  pool?: TseResourcePool | null;
}

export interface TsePoolStatus {
  poolId: string;
  name: string;
  type: TseResourcePoolType;
  isActive: boolean;
  totalCapacity: number;
  usedCapacity: number;
  availableCapacity: number;
  utilizationPercent: number;
  assignedTenantCount: number;
  healthLabel: string;
  warnings: string[];
}

export interface TsePoolMetrics {
  poolId: string;
  name: string;
  generatedAt: string;
  totalCapacity: number;
  usedCapacity: number;
  availableCapacity: number;
  utilizationPercent: number;
  assignedTenantCount: number;
  activeDeviceCount: number;
  healthyDeviceCount: number;
  averageHealthScore: number;
  signedTransactionsLast30Days: number;
  devicesByProvider: Record<string, number>;
}
