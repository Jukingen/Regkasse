'use client';

import { useCallback, useRef, useState } from 'react';

import { SensitiveExportConfirmModal } from '@/components/ui/SensitiveExportConfirmModal';
import { SensitiveExportTwoFactorModal } from '@/components/ui/SensitiveExportTwoFactorModal';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import {
  buildSensitiveExportHeaders,
  resolveSensitiveExportErrorCode,
  requestSensitiveExportApproval,
  requiresCriticalTwoFactor,
  type SensitiveExportKind,
  type SensitiveExportSecurityHeaders,
} from '@/lib/download/sensitiveExportSecurity';


export type SensitiveExportGateRunOptions = {
  kind: SensitiveExportKind;
  resourceId?: string;
  /** When true (Super Admin), server may skip separate approval. */
  isSuperAdmin?: boolean;
  /** Optional pre-known approval id from a prior Super Admin grant. */
  approvalId?: string;
  execute: (headers: SensitiveExportSecurityHeaders) => Promise<void>;
};

/**
 * Orchestrates privacy ack (+ optional 2FA) before sensitive downloads.
 * On approval-required errors, creates a pending Super Admin request.
 */
export function useSensitiveExportGate() {
  const { t } = useI18n();
  const notify = useNotify();
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [twoFactorOpen, setTwoFactorOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const pendingRef = useRef<SensitiveExportGateRunOptions | null>(null);
  const headersRef = useRef<SensitiveExportSecurityHeaders>({});

  const reset = useCallback(() => {
    setConfirmOpen(false);
    setTwoFactorOpen(false);
    setBusy(false);
    pendingRef.current = null;
    headersRef.current = {};
  }, []);

  const tryExecute = useCallback(async () => {
    const pending = pendingRef.current;
    if (!pending) return;
    setBusy(true);
    try {
      await pending.execute(headersRef.current);
      notify.successKey('common.sensitiveExport.downloadSuccess');
      reset();
    } catch (err) {
      const code = await resolveSensitiveExportErrorCode(err);
      if (code === 'SENSITIVE_EXPORT_APPROVAL_REQUIRED') {
        try {
          await requestSensitiveExportApproval({
            exportKind: pending.kind,
            resourceId: pending.resourceId,
            reason: t('common.sensitiveExport.approvalReasonDefault'),
          });
          notify.info(t('common.sensitiveExport.approvalRequested'));
        } catch {
          notify.errorKey('common.sensitiveExport.approvalRequestFailed');
        }
        reset();
        return;
      }
      if (code === 'SENSITIVE_EXPORT_2FA_REQUIRED' || code === 'SENSITIVE_EXPORT_2FA_INVALID') {
        setBusy(false);
        setTwoFactorOpen(true);
        if (code === 'SENSITIVE_EXPORT_2FA_INVALID') {
          notify.errorKey('common.sensitiveExport.twoFactorInvalid');
        }
        return;
      }
      if (code === 'DOWNLOAD_DAILY_LIMIT') {
        notify.errorKey('common.sensitiveExport.dailyLimit');
        reset();
        return;
      }
      if (code === 'DOWNLOAD_FILE_TOO_LARGE') {
        notify.errorKey('common.sensitiveExport.fileTooLarge');
        reset();
        return;
      }
      notify.apiError(err, {
        logContext: 'SensitiveExportGate.execute',
        fallbackKey: 'common.sensitiveExport.downloadFailed',
      });
      reset();
    }
  }, [notify, reset, t]);

  const run = useCallback((opts: SensitiveExportGateRunOptions) => {
    pendingRef.current = opts;
    headersRef.current = buildSensitiveExportHeaders({
      privacyAck: false,
      approvalId: opts.approvalId,
    });
    setConfirmOpen(true);
  }, []);

  const onConfirmPrivacy = useCallback(() => {
    const pending = pendingRef.current;
    if (!pending) return;
    headersRef.current = buildSensitiveExportHeaders({
      privacyAck: true,
      approvalId: pending.approvalId,
      twoFactorCode: headersRef.current['X-2FA-Code'],
    });
    setConfirmOpen(false);
    if (requiresCriticalTwoFactor(pending.kind) && !headersRef.current['X-2FA-Code']) {
      setTwoFactorOpen(true);
      return;
    }
    void tryExecute();
  }, [tryExecute]);

  const onConfirmTwoFactor = useCallback(
    (code: string) => {
      const pending = pendingRef.current;
      if (!pending) return;
      headersRef.current = buildSensitiveExportHeaders({
        privacyAck: true,
        approvalId: pending.approvalId,
        twoFactorCode: code,
      });
      setTwoFactorOpen(false);
      void tryExecute();
    },
    [tryExecute]
  );

  const modals = (
    <>
      <SensitiveExportConfirmModal
        open={confirmOpen}
        confirmLoading={busy}
        onConfirm={onConfirmPrivacy}
        onCancel={reset}
      />
      <SensitiveExportTwoFactorModal
        open={twoFactorOpen}
        confirmLoading={busy}
        onConfirm={onConfirmTwoFactor}
        onCancel={reset}
      />
    </>
  );

  return { run, modals, busy };
}
