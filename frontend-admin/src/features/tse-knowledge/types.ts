export interface TseKnowledgeArticle {
  id: string;
  slug: string;
  title: string;
  description: string;
  body: string;
  category: string;
  isFaq: boolean;
  viewCount: number;
  rating: number;
  ratingCount: number;
  sortOrder: number;
  createdAt: string;
  updatedAt?: string | null;
  diagnosticOnly: boolean;
}

export interface TseKnowledgeFeedback {
  articleId: string;
  feedbackId: string;
  rating: number;
  articleAverageRating: number;
  articleRatingCount: number;
  submittedAt: string;
}
