import { customInstance } from '@/lib/axios';

export const monatsbelegPolicyQueryKey = ['admin', 'rksv', 'monatsbeleg-policy'] as const;

export type MonatsbelegBlockingMode = 'Strict' | 'GracePeriod' | 'WarningOnly';

export type MonatsbelegPolicyDto = {
  blockingMode: MonatsbelegBlockingMode;
  autoMonatsbelegEnabled: boolean;
  monatsbelegRetryCount: number;
  useDecemberMonatsbelegAsJahresbeleg: boolean;
};

export type UpdateMonatsbelegPolicyRequest = {
  blockingMode?: MonatsbelegBlockingMode;
  autoMonatsbelegEnabled?: boolean;
  monatsbelegRetryCount?: number;
};

function normalizeMode(value: unknown): MonatsbelegBlockingMode {
  const raw = String(value ?? '').trim();
  if (raw === 'GracePeriod' || raw === 'WarningOnly' || raw === 'Strict') return raw;
  return 'Strict';
}

function clampRetry(value: unknown): number {
  const n = Number(value);
  if (!Number.isFinite(n)) return 3;
  return Math.min(5, Math.max(1, Math.round(n)));
}

function mapDto(dto: MonatsbelegPolicyDto): MonatsbelegPolicyDto {
  return {
    blockingMode: normalizeMode(dto.blockingMode),
    autoMonatsbelegEnabled: Boolean(dto.autoMonatsbelegEnabled),
    monatsbelegRetryCount: clampRetry(dto.monatsbelegRetryCount),
    useDecemberMonatsbelegAsJahresbeleg: dto.useDecemberMonatsbelegAsJahresbeleg !== false,
  };
}

export function fetchMonatsbelegPolicy(signal?: AbortSignal): Promise<MonatsbelegPolicyDto> {
  return customInstance<MonatsbelegPolicyDto>({
    url: '/api/admin/rksv/monatsbeleg-policy',
    method: 'GET',
    signal,
  }).then(mapDto);
}

export function putMonatsbelegPolicy(
  body: UpdateMonatsbelegPolicyRequest
): Promise<MonatsbelegPolicyDto> {
  return customInstance<MonatsbelegPolicyDto>({
    url: '/api/admin/rksv/monatsbeleg-policy',
    method: 'PUT',
    data: body,
  }).then(mapDto);
}
