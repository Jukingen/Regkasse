'use client';

import { useMutation } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';

export type RksvSignatureVerifyRequest = {
  signature: string;
  certificateThumbprint?: string | null;
};

export type RksvSignatureVerifyResponse = {
  valid: boolean;
  details: string;
  certificateThumbprintUsed?: string | null;
};

export async function verifyRksvSignature(
  request: RksvSignatureVerifyRequest
): Promise<RksvSignatureVerifyResponse> {
  const response = await AXIOS_INSTANCE.post<RksvSignatureVerifyResponse>(
    '/api/admin/rksv/signature/verify',
    {
      signature: request.signature,
      certificateThumbprint: request.certificateThumbprint ?? undefined,
    }
  );
  return response.data;
}

export function useRksvSignatureVerify() {
  return useMutation({
    mutationFn: verifyRksvSignature,
  });
}
