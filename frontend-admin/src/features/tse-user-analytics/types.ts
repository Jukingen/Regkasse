export interface TseDropoffPoint {
  fromStep: string;
  toStep: string;
  fromCount: number;
  toCount: number;
  dropoffPercent: number;
  severity: string;
}

export interface TseFunnelStep {
  step: string;
  label: string;
  count: number;
  conversionPercent: number;
}

export interface TseUxRecommendation {
  code: string;
  title: string;
  description: string;
  severity: string;
  relatedFeature?: string | null;
}

export interface TseUserBehaviorReport {
  tenantId: string;
  tenantName?: string | null;
  periodStart: string;
  periodEnd: string;
  generatedAt: string;
  totalSessions: number;
  averageSessionDuration: number;
  uniqueUsers: number;
  dailyActiveUsers: number;
  featureUsage: Record<string, number>;
  featureAdoptionRate: Record<string, number>;
  dropoffPoints: TseDropoffPoint[];
  userSatisfactionScores: Record<string, number>;
  funnelSteps: TseFunnelStep[];
  recommendations: TseUxRecommendation[];
  diagnosticOnly: boolean;
}

export interface TseFeatureHeatmapCell {
  feature: string;
  dayOfWeek: string;
  count: number;
}

export interface TseFeatureUsageReport {
  tenantId?: string | null;
  periodStart: string;
  periodEnd: string;
  generatedAt: string;
  uniqueUsers: number;
  featureUsage: Record<string, number>;
  featureAdoptionRate: Record<string, number>;
  heatmap: TseFeatureHeatmapCell[];
  diagnosticOnly: boolean;
}

export interface TseCohortRow {
  cohortWeek: string;
  cohortStart: string;
  cohortSize: number;
  retentionByWeek: number[];
}

export interface TseCohortAnalysisResult {
  tenantId?: string | null;
  periodStart: string;
  periodEnd: string;
  generatedAt: string;
  cohortWeeks: number;
  cohorts: TseCohortRow[];
  diagnosticOnly: boolean;
}
