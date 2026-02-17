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
    getGetApiMultilingualReceiptQueryKey,
} from '@/api/generated/multilingual-receipt/multilingual-receipt';

export function useReceiptTemplateQueries() {
    const queryClient = useQueryClient();

    const invalidateList = () => {
        queryClient.invalidateQueries({ queryKey: getGetApiMultilingualReceiptQueryKey() });
    };

    return {
        useList: useGetApiMultilingualReceipt,
        useListByLanguage: useGetApiMultilingualReceiptLanguageLanguage,
        useListByType: useGetApiMultilingualReceiptTypeType,
        useDetail: useGetApiMultilingualReceiptId,
        usePreview: useGetApiMultilingualReceiptPreviewId,
        useGenerate: usePostApiMultilingualReceiptGenerate,
        useCreate: usePostApiMultilingualReceipt,
        useUpdate: usePutApiMultilingualReceiptId,
        useDelete: useDeleteApiMultilingualReceiptId,
        invalidateList,
    };
}
