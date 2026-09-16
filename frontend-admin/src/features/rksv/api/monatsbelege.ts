import { customInstance } from '@/lib/axios';

export const monatsbelegeListQueryKey = ['admin', 'rksv', 'monatsbelege'] as const;

export type MonatsbelegListStatusFilter = 'all' | 'created' | 'failed' | 'fonPending';

export type MonatsbelegListRow = {
  paymentId?: string | null;
  cashRegisterId: string;
  registerNumber: string;
  registerLocation?: string | null;
  year: number;
  month: number;
  period: string;
  createdAtUtc?: string | null;
  createdBy: string;
  createdByUserId: string;
  tseSignature: string;
  depStatus: string;
  fonStatus: string;
  isJahresbeleg: boolean;
  autoCreated: boolean;
  status: string;
  lastError?: string | null;
  attemptCount: number;
  correlationId?: string | null;
};

export type MonatsbelegListResponse = {
  year: number;
  total: number;
  hasFailedAutoCreates: boolean;
  items: MonatsbelegListRow[];
};

export type FetchMonatsbelegeParams = {
  year?: number;
  cashRegisterId?: string;
  status?: MonatsbelegListStatusFilter;
  pageNumber?: number;
  pageSize?: number;
};

export function fetchMonatsbelege(
  params: FetchMonatsbelegeParams,
  signal?: AbortSignal
): Promise<MonatsbelegListResponse> {
  return customInstance<MonatsbelegListResponse>({
    url: '/api/admin/rksv/monatsbelege',
    method: 'GET',
    params: {
      year: params.year,
      cashRegisterId: params.cashRegisterId,
      status: params.status === 'all' ? undefined : params.status,
      pageNumber: params.pageNumber,
      pageSize: params.pageSize,
    },
    signal,
  }).then((dto) => ({
    year: dto.year,
    total: dto.total ?? 0,
    hasFailedAutoCreates: Boolean(dto.hasFailedAutoCreates),
    items: Array.isArray(dto.items) ? dto.items : [],
  }));
}
