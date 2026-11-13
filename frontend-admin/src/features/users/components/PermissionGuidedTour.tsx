'use client';

import { Tour } from 'antd';
import type { TourProps } from 'antd';
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { useI18n } from '@/i18n';

export type PermissionGuidedTourProps = {
  open: boolean;
  onClose: () => void;
  /** When false, skip the save step (e.g. user override modal). */
  includeSaveStep?: boolean;
};

function tourTarget(selector: string): HTMLElement {
  return document.querySelector<HTMLElement>(selector) ?? document.body;
}

/**
 * Ant Design Tour for permission editor surfaces (role drawer / user modal).
 * Targets: [data-permission-tour="search|toggle|reset|save"]
 *
 * Controlled open/current + unmount-when-closed avoids rc-tour layout/scroll
 * update loops inside Modal/Drawer.
 */
export function PermissionGuidedTour({
  open,
  onClose,
  includeSaveStep = true,
}: PermissionGuidedTourProps) {
  const { t } = useI18n();
  const [current, setCurrent] = useState(0);
  const [hasCompleted, setHasCompleted] = useState(false);
  const onCloseRef = useRef(onClose);
  const closingRef = useRef(false);

  onCloseRef.current = onClose;

  useEffect(() => {
    if (open) {
      closingRef.current = false;
      setHasCompleted(false);
      setCurrent(0);
    }
  }, [open]);

  const handleClose = useCallback(() => {
    if (closingRef.current) return;
    closingRef.current = true;
    setCurrent(0);
    setHasCompleted(true);
    onCloseRef.current();
  }, []);

  const handleChange = useCallback((next: number) => {
    setCurrent(next);
  }, []);

  const steps = useMemo((): TourProps['steps'] => {
    const list: NonNullable<TourProps['steps']> = [
      {
        title: t('users.permissionOnboarding.tourSearchTitle'),
        description: t('users.permissionOnboarding.tourSearchBody'),
        target: () => tourTarget('[data-permission-tour="search"]'),
        placement: 'bottom',
        scrollIntoViewOptions: { block: 'nearest', inline: 'nearest' },
      },
      {
        title: t('users.permissionOnboarding.tourToggleTitle'),
        description: t('users.permissionOnboarding.tourToggleBody'),
        target: () => tourTarget('[data-permission-tour="toggle"]'),
        placement: 'left',
        scrollIntoViewOptions: { block: 'nearest', inline: 'nearest' },
      },
      {
        title: t('users.permissionOnboarding.tourResetTitle'),
        description: t('users.permissionOnboarding.tourResetBody'),
        target: () => tourTarget('[data-permission-tour="reset"]'),
        placement: 'top',
        scrollIntoViewOptions: { block: 'nearest', inline: 'nearest' },
      },
    ];
    if (includeSaveStep) {
      list.push({
        title: t('users.permissionOnboarding.tourSaveTitle'),
        description: t('users.permissionOnboarding.tourSaveBody'),
        target: () => tourTarget('[data-permission-tour="save"]'),
        placement: 'top',
        scrollIntoViewOptions: { block: 'nearest', inline: 'nearest' },
      });
    }
    return list;
  }, [t, includeSaveStep]);

  // Unmount when closed/completed so rc-tour useTarget layout effects cannot keep updating.
  // Note: Tour has no maskClosable/closeOnMaskClick — mask clicks do not close via those props.
  if (!open || hasCompleted) {
    return null;
  }

  return (
    <Tour
      open
      current={current}
      onChange={handleChange}
      onClose={handleClose}
      onFinish={handleClose}
      steps={steps}
      zIndex={1100}
      type="primary"
      disabledInteraction
      mask
    />
  );
}
