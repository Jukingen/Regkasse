import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';

export type SendExportEmailPayload = {
  to: string;
  subject: string;
  message?: string;
  scheduledForUtc?: string | null;
  sourceKind?: string | null;
  sourceId?: string | null;
  preferLink?: boolean;
  /** When set, uploaded as multipart file. */
  blob?: Blob | null;
  fileName?: string;
};

export type SendExportEmailResponse = {
  id: string;
  status: string;
  deliveryMode: string;
  scheduledForUtc?: string | null;
  sentAtUtc?: string | null;
  recipientEmail: string;
  fileName: string;
  fileSizeBytes: number;
  message?: string | null;
};

export type ExportEmailDeliveryListItem = {
  id: string;
  recipientEmail: string;
  subject: string;
  fileName: string;
  fileSizeBytes: number;
  deliveryMode: string;
  status: string;
  sourceKind?: string | null;
  sourceId?: string | null;
  scheduledForUtc?: string | null;
  sentAtUtc?: string | null;
  createdAtUtc: string;
  errorMessage?: string | null;
};

export type ExportEmailDeliveryListResponse = {
  items: ExportEmailDeliveryListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export async function sendExportEmail(
  payload: SendExportEmailPayload
): Promise<SendExportEmailResponse> {
  const form = new FormData();
  form.append('To', payload.to.trim());
  form.append('Subject', payload.subject.trim());
  if (payload.message?.trim()) form.append('Message', payload.message.trim());
  if (payload.scheduledForUtc) form.append('ScheduledForUtc', payload.scheduledForUtc);
  if (payload.sourceKind) form.append('SourceKind', payload.sourceKind);
  if (payload.sourceId) form.append('SourceId', payload.sourceId);
  if (payload.preferLink) form.append('PreferLink', 'true');
  if (payload.blob) {
    form.append('file', payload.blob, payload.fileName || 'export.bin');
  }

  const res = await AXIOS_INSTANCE.post<SendExportEmailResponse>(
    '/api/admin/export-email/send',
    form,
    { headers: { 'Content-Type': 'multipart/form-data' } }
  );
  return res.data;
}

export async function listExportEmailHistory(params: {
  status?: string;
  page?: number;
  pageSize?: number;
}): Promise<ExportEmailDeliveryListResponse> {
  const res = await AXIOS_INSTANCE.get<ExportEmailDeliveryListResponse>(
    '/api/admin/export-email/history',
    { params }
  );
  return res.data;
}

export async function cancelScheduledExportEmail(
  id: string
): Promise<SendExportEmailResponse> {
  const res = await AXIOS_INSTANCE.post<SendExportEmailResponse>(
    `/api/admin/export-email/history/${id}/cancel`
  );
  return res.data;
}

export function useExportEmailHistory(params: {
  status?: string;
  page?: number;
  pageSize?: number;
  enabled?: boolean;
}) {
  return useQuery({
    queryKey: ['export-email-history', params.status, params.page, params.pageSize],
    queryFn: () =>
      listExportEmailHistory({
        status: params.status,
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
      }),
    enabled: params.enabled !== false,
  });
}

export function useSendExportEmail() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: sendExportEmail,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['export-email-history'] });
    },
  });
}

export function useCancelScheduledExportEmail() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: cancelScheduledExportEmail,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['export-email-history'] });
    },
  });
}
