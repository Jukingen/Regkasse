import { customInstance } from '@/lib/axios';

export type SupportTicketCategory =
  | 'Technical'
  | 'Billing'
  | 'License'
  | 'FeatureRequest'
  | 'General'
  | string;
export type SupportTicketPriority = 'Low' | 'Medium' | 'High' | 'Urgent' | string;
export type SupportTicketStatus =
  | 'Open'
  | 'InProgress'
  | 'WaitingOnTenant'
  | 'WaitingOnStaff'
  | 'Resolved'
  | 'Closed'
  | string;

export type SupportTicketMessageDto = {
  id: string;
  authorUserId: string;
  authorDisplayName?: string | null;
  body: string;
  isStaffReply: boolean;
  isInternal: boolean;
  createdAtUtc: string;
};

export type SupportTicketListItemDto = {
  id: string;
  tenantId: string;
  tenantName?: string | null;
  ticketNumber: string;
  category: SupportTicketCategory;
  priority: SupportTicketPriority;
  status: SupportTicketStatus;
  title: string;
  createdByUserId: string;
  createdByDisplayName?: string | null;
  assignedToUserId?: string | null;
  assignedToDisplayName?: string | null;
  resolvedAtUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  messageCount: number;
};

export type SupportTicketDetailDto = SupportTicketListItemDto & {
  messages: SupportTicketMessageDto[];
};

export type SupportTicketListResponse = {
  items: SupportTicketListItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  openCount: number;
};

export type SupportTicketInboxSummary = {
  openCount: number;
  inProgressCount: number;
  resolvedCount: number;
  resolvedLast30DaysCount: number;
  closedCount: number;
  totalCount: number;
  byCategory: Record<string, number>;
  byPriority: Record<string, number>;
};

export type SupportTicketListParams = {
  page?: number;
  pageSize?: number;
  status?: string;
  category?: string;
  priority?: string;
  search?: string;
  fromUtc?: string;
  toUtc?: string;
};

export const supportTicketQueryKeys = {
  all: ['admin', 'support', 'tickets'] as const,
  mine: (params?: SupportTicketListParams) =>
    [...supportTicketQueryKeys.all, 'mine', params ?? {}] as const,
  inbox: (params?: SupportTicketListParams) =>
    [...supportTicketQueryKeys.all, 'inbox', params ?? {}] as const,
  summary: () => [...supportTicketQueryKeys.all, 'summary'] as const,
  detail: (id: string) => [...supportTicketQueryKeys.all, 'detail', id] as const,
  openCount: () => [...supportTicketQueryKeys.all, 'open-count'] as const,
};

function withListParams(url: string, params?: SupportTicketListParams): string {
  if (!params) return url;
  const search = new URLSearchParams();
  if (params.page) search.set('page', String(params.page));
  if (params.pageSize) search.set('pageSize', String(params.pageSize));
  if (params.status) search.set('status', params.status);
  if (params.category) search.set('category', params.category);
  if (params.priority) search.set('priority', params.priority);
  if (params.search) search.set('search', params.search);
  if (params.fromUtc) search.set('fromUtc', params.fromUtc);
  if (params.toUtc) search.set('toUtc', params.toUtc);
  const qs = search.toString();
  return qs ? `${url}?${qs}` : url;
}

export async function fetchMySupportTickets(
  params?: SupportTicketListParams,
  signal?: AbortSignal
): Promise<SupportTicketListResponse> {
  return customInstance<SupportTicketListResponse>({
    url: withListParams('/api/admin/support/tickets', params),
    method: 'GET',
    signal,
  });
}

export async function fetchOpenSupportTicketCount(
  signal?: AbortSignal
): Promise<{ openCount: number }> {
  return customInstance<{ openCount: number }>({
    url: '/api/admin/support/tickets/open-count',
    method: 'GET',
    signal,
  });
}

export async function fetchAllSupportTickets(
  params?: SupportTicketListParams,
  signal?: AbortSignal
): Promise<SupportTicketListResponse> {
  return customInstance<SupportTicketListResponse>({
    url: withListParams('/api/admin/support/admin/tickets', params),
    method: 'GET',
    signal,
  });
}

export async function fetchSupportInboxSummary(
  signal?: AbortSignal
): Promise<SupportTicketInboxSummary> {
  return customInstance<SupportTicketInboxSummary>({
    url: '/api/admin/support/admin/tickets/summary',
    method: 'GET',
    signal,
  });
}

export async function fetchSupportTicket(
  id: string,
  signal?: AbortSignal
): Promise<SupportTicketDetailDto> {
  return customInstance<SupportTicketDetailDto>({
    url: `/api/admin/support/tickets/${id}`,
    method: 'GET',
    signal,
  });
}

export async function fetchAdminSupportTicket(
  id: string,
  signal?: AbortSignal
): Promise<SupportTicketDetailDto> {
  return customInstance<SupportTicketDetailDto>({
    url: `/api/admin/support/admin/tickets/${id}`,
    method: 'GET',
    signal,
  });
}

export async function createSupportTicket(body: {
  category: string;
  priority: string;
  title: string;
  message: string;
}): Promise<SupportTicketDetailDto> {
  return customInstance<SupportTicketDetailDto>({
    url: '/api/admin/support/tickets',
    method: 'POST',
    data: body,
  });
}

export async function addSupportTicketMessage(
  id: string,
  body: string,
  isInternal = false
): Promise<SupportTicketDetailDto> {
  return customInstance<SupportTicketDetailDto>({
    url: `/api/admin/support/tickets/${id}/messages`,
    method: 'POST',
    data: { body, isInternal },
  });
}

export async function addAdminSupportTicketMessage(
  id: string,
  body: string,
  isInternal = false
): Promise<SupportTicketDetailDto> {
  return customInstance<SupportTicketDetailDto>({
    url: `/api/admin/support/admin/tickets/${id}/messages`,
    method: 'POST',
    data: { body, isInternal },
  });
}

export async function updateOwnSupportTicketStatus(
  id: string,
  status: string
): Promise<SupportTicketDetailDto> {
  return customInstance<SupportTicketDetailDto>({
    url: `/api/admin/support/tickets/${id}/status`,
    method: 'PUT',
    data: { status },
  });
}

export async function updateAdminSupportTicketStatus(
  id: string,
  status: string
): Promise<SupportTicketDetailDto> {
  return customInstance<SupportTicketDetailDto>({
    url: `/api/admin/support/admin/tickets/${id}/status`,
    method: 'PUT',
    data: { status },
  });
}

export async function assignSupportTicket(
  id: string,
  assignedToUserId?: string
): Promise<SupportTicketDetailDto> {
  return customInstance<SupportTicketDetailDto>({
    url: `/api/admin/support/admin/tickets/${id}/assign`,
    method: 'PUT',
    data: { assignedToUserId: assignedToUserId ?? '' },
  });
}
