'use client';

import { ReactNode } from 'react';
import { usePermissions } from './usePermissions';
import { Tooltip } from 'antd';

export type PermissionGateMode = 'hide' | 'disable';

interface PermissionGateProps {
  /** One or more permissions; user needs at least one (any). */
  permission: string | string[];
  /** hide = do not render children; disable = render children but disabled + tooltip. */
  mode?: PermissionGateMode;
  /** Shown when mode is 'disable' and user lacks permission. */
  fallbackTooltip?: string;
  children: ReactNode;
}

/**
 * Page-level or component/button-level guard by permission.
 * Use mode="hide" for menu items and sections; mode="disable" for actions (button stays visible, disabled + tooltip).
 */
export function PermissionGate({
  permission,
  mode = 'hide',
  fallbackTooltip = 'Sie haben keine Berechtigung für diese Aktion.',
  children,
}: PermissionGateProps) {
  const { hasPermission, hasAnyPermission } = usePermissions();
  const allowed = Array.isArray(permission)
    ? hasAnyPermission(permission)
    : hasPermission(permission);

  if (allowed) return <>{children}</>;

  if (mode === 'hide') return null;

  return (
    <Tooltip title={fallbackTooltip}>
      <span style={{ display: 'inline-block', cursor: 'not-allowed' }}>
        {children}
      </span>
    </Tooltip>
  );
}

/**
 * Wraps a child (e.g. Button); when user lacks permission, renders it disabled (wrapper span + tooltip).
 * Use when you want the button to stay visible but not clickable. For Ant Design Button, prefer
 * disabled={!hasPermission('x')} + Tooltip for native disabled state.
 */
interface PermissionGateButtonProps {
  permission: string | string[];
  fallbackTooltip?: string;
  children: ReactNode;
}

export function PermissionGateButton({
  permission,
  fallbackTooltip = 'Sie haben keine Berechtigung für diese Aktion.',
  children,
}: PermissionGateButtonProps) {
  const { hasPermission, hasAnyPermission } = usePermissions();
  const allowed = Array.isArray(permission)
    ? hasAnyPermission(permission)
    : hasPermission(permission);

  if (allowed) return <>{children}</>;

  return (
    <Tooltip title={fallbackTooltip}>
      <span style={{ display: 'inline-block', pointerEvents: 'none', opacity: 0.65 }}>
        {children}
      </span>
    </Tooltip>
  );
}
