/**
 * Manual admin API client for FA user feedback loop.
 */
import { AXIOS_INSTANCE } from '@/lib/axios';

export type AdminFeedbackCategory = 'EaseOfUse' | 'Performance' | 'FeatureRequest' | 'Bug';

export type AdminFeedbackStatus =
  | 'UnderReview'
  | 'InProgress'
  | 'Implemented'
  | 'Declined'
  | 'Duplicate';

export type AdminFeedbackDto = {
  id: string;
  tenantId: string;
  tenantName?: string | null;
  category: AdminFeedbackCategory | string;
  status: AdminFeedbackStatus | string;
  title: string;
  message: string;
  rating?: number | null;
  pagePath?: string | null;
  submittedByUserId: string;
  submittedByDisplayName?: string | null;
  /** Login username from Identity (Super Admin inbox). */
  submittedByUsername?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  reviewedByUserId?: string | null;
  reviewedAtUtc?: string | null;
  reviewerNote?: string | null;
};

export type AdminFeedbackListResponse = {
  items: AdminFeedbackDto[];
  total: number;
};

export type CreateAdminFeedbackRequest = {
  category: AdminFeedbackCategory;
  title: string;
  message: string;
  rating?: number;
  pagePath?: string;
};

export type UpdateAdminFeedbackStatusRequest = {
  status: AdminFeedbackStatus;
  reviewerNote?: string;
};

export async function createAdminFeedback(
  body: CreateAdminFeedbackRequest,
): Promise<AdminFeedbackDto> {
  const { data } = await AXIOS_INSTANCE.post<AdminFeedbackDto>('/api/admin/feedback', body);
  return data;
}

export async function fetchMyAdminFeedback(params?: {
  limit?: number;
  offset?: number;
}): Promise<AdminFeedbackListResponse> {
  const { data } = await AXIOS_INSTANCE.get<AdminFeedbackListResponse>('/api/admin/feedback/mine', {
    params: { limit: params?.limit ?? 50, offset: params?.offset ?? 0 },
  });
  return data;
}

export async function fetchAllAdminFeedback(params?: {
  status?: string;
  category?: string;
  limit?: number;
  offset?: number;
}): Promise<AdminFeedbackListResponse> {
  const { data } = await AXIOS_INSTANCE.get<AdminFeedbackListResponse>('/api/admin/feedback', {
    params: {
      status: params?.status,
      category: params?.category,
      limit: params?.limit ?? 50,
      offset: params?.offset ?? 0,
    },
  });
  return data;
}

export async function updateAdminFeedbackStatus(
  id: string,
  body: UpdateAdminFeedbackStatusRequest,
): Promise<AdminFeedbackDto> {
  const { data } = await AXIOS_INSTANCE.patch<AdminFeedbackDto>(
    `/api/admin/feedback/${id}/status`,
    body,
  );
  return data;
}
