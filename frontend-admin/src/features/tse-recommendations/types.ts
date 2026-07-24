export type TseRecommendationCategory = 'Performance' | 'Cost' | 'Security' | 'Reliability' | string;
export type TseRecommendationImpact = 'High' | 'Medium' | 'Low' | string;

export interface TseRecommendation {
  id: string;
  tenantId: string;
  code: string;
  category: TseRecommendationCategory;
  title: string;
  description: string;
  impact: TseRecommendationImpact;
  estimatedSavings: number;
  effortScore: number;
  createdAt: string;
  isApplied: boolean;
  appliedAt?: string | null;
  isDismissed: boolean;
  rating: number;
  diagnosticOnly: boolean;
}

export interface TseRecommendationResult {
  recommendationId: string;
  success: boolean;
  message: string;
  recommendation?: TseRecommendation | null;
}

export interface TseRecommendationFeedback {
  recommendationId: string;
  rating: number;
  ratedAt: string;
  success: boolean;
  message: string;
  recommendation?: TseRecommendation | null;
}
