import { customInstance } from '@/lib/axios';

export type OperationLogListItem = {
  id: string;
  tenantId: string;
  userId: string;
  userEmail?: string | null;
  userDisplayName?: string | null;
  operationType: string;
  entityType: string;
  entityId: string;
  isUndone: boolean;
  undoneAt?: string | null;
  reason?: string | null;
  createdAt: string;
  canUndo: boolean;
};

export type OperationLogDetail = OperationLogListItem & {
  beforeState?: string | null;
  afterState?: string | null;
  undoneBy?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
};

export type OperationLogListResponse = {
  items: OperationLogListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type UndoOperationResponse = {
  success: boolean;
  errorCode?: string | null;
  message?: string | null;
  operationId?: string | null;
};

export async function listOperationLogs(params?: {
  page?: number;
  pageSize?: number;
  operationType?: string;
  isUndone?: boolean;
}): Promise<OperationLogListResponse> {
  return customInstance<OperationLogListResponse>({
    url: '/api/admin/operation-logs',
    method: 'GET',
    params,
  });
}

export async function getOperationLog(id: string): Promise<OperationLogDetail> {
  return customInstance<OperationLogDetail>({
    url: `/api/admin/operation-logs/${id}`,
    method: 'GET',
  });
}

export async function undoOperation(
  id: string,
  reason?: string
): Promise<UndoOperationResponse> {
  return customInstance<UndoOperationResponse>({
    url: `/api/admin/operation-logs/${id}/undo`,
    method: 'POST',
    data: { reason },
  });
}
