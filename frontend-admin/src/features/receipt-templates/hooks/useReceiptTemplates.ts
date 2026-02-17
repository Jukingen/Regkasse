import { useQueryClient } from '@tanstack/react-query';
import {
    useGetApiMultilingualReceipt,
    useGetApiMultilingualReceiptId,
    useGetApiMultilingualReceiptLanguageLanguage,
    useGetApiMultilingualReceiptTypeType,
    usePostApiMultilingualReceipt,
    usePutApiMultilingualReceiptId,
    useDeleteApiMultilingualReceiptId,
    useGetApiMultilingualReceiptPreviewId,
    usePostApiMultilingualReceiptGenerate,
} from '@/api/generated/multilingual-receipt/multilingual-receipt';
import { useURLFilters } from '@/hooks/useURLFilters';

// 1. Key Factory
export const receiptTemplateKeys = {
    all: ['receipt-templates'] as const,
    lists: () => [...receiptTemplateKeys.all, 'list'] as const,
    list: (filters: string) => [...receiptTemplateKeys.lists(), { filters }] as const,
    details: () => [...receiptTemplateKeys.all, 'detail'] as const,
    detail: (id: string) => [...receiptTemplateKeys.details(), id] as const,
    previews: () => [...receiptTemplateKeys.all, 'preview'] as const,
};

// 2. Filter Hook
export function useReceiptTemplateFilters() {
    return useURLFilters<{
        mode: 'all' | 'language' | 'type';
        value: string;
    }>();
}

// 3. Main Hook
export function useReceiptTemplates() {
    const queryClient = useQueryClient();

    const invalidateList = () => {
        // Invalidate all standard lists
        queryClient.invalidateQueries({ queryKey: receiptTemplateKeys.lists() });
        // Also invalidate the raw orval keys to be safe during transition
        queryClient.invalidateQueries({ queryKey: ['/api/MultilingualReceipt'] });
    };

    return {
        // Queries
        useList: useGetApiMultilingualReceipt,
        useListByLanguage: useGetApiMultilingualReceiptLanguageLanguage,
        useListByType: useGetApiMultilingualReceiptTypeType,
        useDetail: useGetApiMultilingualReceiptId,
        usePreview: useGetApiMultilingualReceiptPreviewId,

        // Mutations
        useGenerate: usePostApiMultilingualReceiptGenerate,
        useCreate: usePostApiMultilingualReceipt,
        useUpdate: usePutApiMultilingualReceiptId,
        useDelete: useDeleteApiMultilingualReceiptId,

        // Utils
        invalidateList,
        keys: receiptTemplateKeys,
    };
}
