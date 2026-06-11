'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { postApiRksvSpecialReceiptsMonatsbeleg } from '@/api/generated/rksv-special-receipts/rksv-special-receipts';
import type { CreateMonatsbelegRequest, CreateMonatsbelegResponse } from '@/api/generated/model';
import { monatsbelegQueryKeys } from '@/features/rksv/hooks/useMonatsbeleg';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';

export type CreateMonatsbelegVariables = {
    data: CreateMonatsbelegRequest;
    /** Admin override for past Vienna calendar months (query: force=true). */
    force?: boolean;
};

export function useCreateMonatsbeleg() {
    const queryClient = useQueryClient();

    return useMutation<CreateMonatsbelegResponse, unknown, CreateMonatsbelegVariables>({
        mutationFn: async ({ data, force }) =>
            postApiRksvSpecialReceiptsMonatsbeleg(data, {
                params: force ? { force: true } : undefined,
            }),
        onSuccess: async () => {
            await Promise.all([
                queryClient.invalidateQueries({ queryKey: monatsbelegQueryKeys.statusOverview }),
                queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.operations.reminderOverview }),
                queryClient.invalidateQueries({ queryKey: ['rksv-sonderbelege-recent-special'] }),
                queryClient.invalidateQueries({ queryKey: ['/api/Receipts/list'] }),
            ]);
        },
    });
}

export type CreateMonatsbelegInput = CreateMonatsbelegRequest;

export type CreateMonatsbelegResult = CreateMonatsbelegResponse;
