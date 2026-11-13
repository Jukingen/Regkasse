export type RiskLevel = 'Low' | 'Medium' | 'High' | 'Critical';

export type RiskScoreDto = {
  id: string;
  tenantId: string;
  tenantName?: string | null;
  userId: string;
  userEmail?: string | null;
  userName?: string | null;
  actionType: string;
  score: number;
  riskLevel: RiskLevel | string;
  reason: string;
  createdAt: string;
  isResolved: boolean;
  resolvedAt?: string | null;
  resolvedBy?: string | null;
  resolution?: string | null;
};

export type RiskScoreSummaryDto = {
  critical: number;
  high: number;
  medium: number;
  low: number;
  open: number;
};

export type RiskScoreListResponse = {
  total: number;
  items: RiskScoreDto[];
  summary: RiskScoreSummaryDto;
};
