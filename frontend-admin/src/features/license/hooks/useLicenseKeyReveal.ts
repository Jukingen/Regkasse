'use client';

import { useCallback, useEffect, useState } from 'react';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

/**
 * Super Admin can reveal full REGK keys. Default is visible for support;
 * hiding remains available for screenshots. Enabling again asks for confirmation.
 */
export function useLicenseKeyReveal(canReveal: boolean) {
  const { t } = useI18n();
  const { modal } = useAntdApp();
  const [showKeys, setShowKeys] = useState(canReveal);

  useEffect(() => {
    setShowKeys(canReveal);
  }, [canReveal]);

  const onShowKeysChange = useCallback(
    (checked: boolean) => {
      if (!canReveal) {
        setShowKeys(false);
        return;
      }
      if (!checked) {
        setShowKeys(false);
        return;
      }
      modal.confirm({
        title: t('license.management.showKeysConfirmTitle'),
        content: t('license.management.showKeysConfirmContent'),
        okText: t('license.management.showKeys'),
        cancelText: t('common.buttons.cancel'),
        onOk: () => {
          setShowKeys(true);
        },
      });
    },
    [canReveal, modal, t]
  );

  return {
    canReveal,
    showKeys: canReveal && showKeys,
    onShowKeysChange,
  };
}
