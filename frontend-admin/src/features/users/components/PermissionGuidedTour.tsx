'use client';

import { Tour } from 'antd';
import type { TourProps } from 'antd';
import React, { useMemo } from 'react';

import { useI18n } from '@/i18n';

export type PermissionGuidedTourProps = {
  open: boolean;
  onClose: () => void;
  /** When false, skip the save step (e.g. user override modal). */
  includeSaveStep?: boolean;
};

function tourTarget(selector: string): HTMLElement | null {
  if (typeof document === 'undefined') return null;
  return document.querySelector(selector);
}

/**
 * Ant Design Tour for permission editor surfaces (role drawer / user modal).
 * Targets: [data-permission-tour="search|toggle|reset|save"]
 */
export function PermissionGuidedTour({
  open,
  onClose,
  includeSaveStep = true,
}: PermissionGuidedTourProps) {
  const { t } = useI18n();

  const steps = useMemo((): TourProps['steps'] => {
    const list: NonNullable<TourProps['steps']> = [
      {
        title: t('users.permissionOnboarding.tourSearchTitle'),
        description: t('users.permissionOnboarding.tourSearchBody'),
        target: () => tourTarget('[data-permission-tour="search"]'),
        placement: 'bottom',
      },
      {
        title: t('users.permissionOnboarding.tourToggleTitle'),
        description: t('users.permissionOnboarding.tourToggleBody'),
        target: () => tourTarget('[data-permission-tour="toggle"]'),
        placement: 'left',
      },
      {
        title: t('users.permissionOnboarding.tourResetTitle'),
        description: t('users.permissionOnboarding.tourResetBody'),
        target: () => tourTarget('[data-permission-tour="reset"]'),
        placement: 'top',
      },
    ];
    if (includeSaveStep) {
      list.push({
        title: t('users.permissionOnboarding.tourSaveTitle'),
        description: t('users.permissionOnboarding.tourSaveBody'),
        target: () => tourTarget('[data-permission-tour="save"]'),
        placement: 'top',
      });
    }
    return list;
  }, [t, includeSaveStep]);

  return (
    <Tour
      open={open}
      onClose={onClose}
      onFinish={onClose}
      steps={steps}
      zIndex={1100}
      type="primary"
    />
  );
}
