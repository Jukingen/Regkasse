'use client';

import { useMutation } from '@tanstack/react-query';

import type {
  DepExportLiveMeta,
  DepExportRequestParams,
  RksvDepExportEnvelope,
  RksvDepExportRoot,
} from '@/features/rksv/types/depExport';
import { AXIOS_INSTANCE } from '@/lib/axios';

export type DepExportMutationResult = {
  dep: RksvDepExportRoot;
  meta: DepExportLiveMeta;
};

export const useDepExport = () => {
  return useMutation({
    mutationFn: async (params: DepExportRequestParams): Promise<DepExportMutationResult> => {
      const response = await AXIOS_INSTANCE.get<RksvDepExportEnvelope | RksvDepExportRoot>(
        '/api/admin/rksv/dep-export',
        {
          params: { ...params, includeEnvelope: true },
        }
      );

      const body = response.data;
      if (body && typeof body === 'object' && 'dep' in body && body.dep) {
        const envelope = body as RksvDepExportEnvelope;
        return {
          dep: envelope.dep,
          meta: {
            isSimulated: envelope.isSimulated ?? envelope.isDemo ?? false,
            simulationNote: envelope.simulationNote ?? null,
            environment: envelope.environment ?? null,
            legacyJwsCount: envelope.legacyJwsCount ?? 0,
            f5CompliantJwsCount: envelope.f5CompliantJwsCount ?? 0,
            legacyJwsWarning: envelope.legacyJwsWarning ?? null,
            prueftoolCompatible: envelope.prueftoolCompatible ?? (envelope.legacyJwsCount ?? 0) === 0,
            historyId: envelope.historyId ?? envelope.exportId ?? null,
            exportId: envelope.exportId ?? envelope.historyId ?? null,
            fileName: envelope.fileName ?? null,
            downloadUrl: envelope.downloadUrl ?? null,
            expiresAt: envelope.expiresAt ?? null,
            fileSizeBytes: envelope.fileSizeBytes ?? null,
          },
        };
      }

      return {
        dep: body as RksvDepExportRoot,
        meta: {
          isSimulated: false,
          simulationNote: null,
          environment: null,
          legacyJwsCount: 0,
          f5CompliantJwsCount: 0,
          legacyJwsWarning: null,
          prueftoolCompatible: true,
          historyId: null,
          exportId: null,
          fileName: null,
          downloadUrl: null,
          expiresAt: null,
          fileSizeBytes: null,
        },
      };
    },
  });
};
