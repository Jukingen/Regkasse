'use client';

/**
 * Ant Design message.open: short localized summary + raw server text block; technical log in English (technicalConsole).
 */
import type { MessageInstance } from 'antd/es/message/interface';
import { BackendRawTextBlock } from '@/components/admin-layout/BackendRawTextBlock';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { extractRawApiErrorMessage } from './extractRawApiErrorMessage';
import { normalizeApiError } from './normalizedApiError';
import { buildTechnicalApiErrorPayload } from './technicalApiErrorLog';
import { getUserFacingApiErrorMessage, type TranslateFn, type UserFacingApiErrorOptions } from './userFacingApiError';

export function openApiErrorMessage(
  messageOpen: MessageInstance['open'],
  t: TranslateFn,
  error: unknown,
  options: UserFacingApiErrorOptions,
): void {
  technicalConsole.error(`[API Error] ${options.logContext}`, buildTechnicalApiErrorPayload(normalizeApiError(error)));
  const short = getUserFacingApiErrorMessage(t, error, { ...options, skipLog: true });
  const raw = extractRawApiErrorMessage(error);
  messageOpen({
    type: 'error',
    content: (
      <div>
        <div>{short}</div>
        <BackendRawTextBlock introKey="common.backend.serverHintIntro" body={raw} />
      </div>
    ),
    duration: raw ? 10 : 6,
  });
}
