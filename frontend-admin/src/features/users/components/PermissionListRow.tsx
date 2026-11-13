'use client';

import {
  CheckCircleFilled,
  CloseCircleFilled,
  ThunderboltFilled,
} from '@ant-design/icons';
import { Checkbox, Switch, Tag, Tooltip, Typography } from 'antd';
import React, { useMemo } from 'react';

import { HighlightedText } from '@/features/users/components/HighlightedText';
import { ImpliedPermissionTag } from '@/features/users/components/ImpliedPermissionTag';
import { PermissionHelpTooltip, PermissionHoverHelp } from '@/features/users/components/PermissionHelpTooltip';
import { PermissionMenuTags } from '@/features/users/components/PermissionMenuTags';
import { resolvePermissionDisplayLabel } from '@/features/users/utils/permissionDisplayLabel';
import { getMenuItemsAffectedByPermission } from '@/features/users/utils/permissionMenuImpact';
import { useI18n } from '@/i18n';
import { isPermissionImpliedOnly } from '@/shared/auth/permissionImplications';

export type PermissionRowSource = 'role' | 'custom' | 'implied' | 'none';

export type PermissionListRowProps = {
  permission: string;
  /** Effective / draft checked state (allowed). */
  checked: boolean;
  disabled?: boolean;
  /** When set, shows Switch (user overrides). Otherwise Checkbox (role draft). */
  mode: 'switch' | 'checkbox';
  onChange: (checked: boolean) => void;
  searchQuery?: string;
  focused?: boolean;
  onFocus?: () => void;
  /** Visual source: Rolle / Individuell / Impliziert. */
  source?: PermissionRowSource;
  /** Direct holds for implication detection (role draft or effective set). */
  heldPermissions?: Iterable<string>;
  catalogDescription?: string | null;
  /** Show status icon (✅ / 🚫 / ⚡). */
  showStatusIcon?: boolean;
  /** Multi-select for batch actions. */
  selected?: boolean;
  onSelectedChange?: (selected: boolean) => void;
  selectionEnabled?: boolean;
  /** Visual diff vs compare role. */
  diffHighlight?: 'added' | 'removed' | 'same' | 'changed';
  /** Show related sidebar menu tags (Menü column). */
  showAffectedMenus?: boolean;
  /** Highlight menu tags (e.g. when row focused / toggle active). */
  highlightAffectedMenus?: boolean;
  /** Dim row (unrelated to active menu filter). */
  dimmed?: boolean;
};
function StatusIcon({
  checked,
  source,
  labels,
}: {
  checked: boolean;
  source: PermissionRowSource;
  labels: { allowed: string; denied: string; custom: string };
}) {
  if (source === 'custom') {
    return (
      <Tooltip title={labels.custom}>
        <ThunderboltFilled
          style={{ color: checked ? '#1677ff' : '#fa8c16', fontSize: 15 }}
          aria-label={labels.custom}
        />
      </Tooltip>
    );
  }
  if (source === 'implied' || checked) {
    return (
      <Tooltip title={labels.allowed}>
        <CheckCircleFilled
          style={{ color: source === 'implied' ? '#722ed1' : '#52c41a', fontSize: 15 }}
          aria-label={labels.allowed}
        />
      </Tooltip>
    );
  }
  return (
    <Tooltip title={labels.denied}>
      <CloseCircleFilled style={{ color: '#ff4d4f', fontSize: 15 }} aria-label={labels.denied} />
    </Tooltip>
  );
}

function SourceTag({ source, t }: { source: PermissionRowSource; t: (k: string) => string }) {
  if (source === 'custom') {
    return (
      <Tag color="blue" style={{ margin: 0, fontSize: 11 }}>
        {t('users.permissionsModal.sourceCustom')}
      </Tag>
    );
  }
  if (source === 'implied') {
    return (
      <Tag color="purple" style={{ margin: 0, fontSize: 11 }}>
        {t('users.permissionsModal.sourceImplied')}
      </Tag>
    );
  }
  if (source === 'role') {
    return (
      <Tag style={{ margin: 0, fontSize: 11 }}>
        {t('users.permissionsModal.sourceRole')}
      </Tag>
    );
  }
  return null;
}

/**
 * Compact permission row: selection + control + status + label + key badge + source + help.
 */
export function PermissionListRow({
  permission,
  checked,
  disabled = false,
  mode,
  onChange,
  searchQuery = '',
  focused = false,
  onFocus,
  source = 'none',
  heldPermissions,
  catalogDescription,
  showStatusIcon = true,
  selected = false,
  onSelectedChange,
  selectionEnabled = false,
  diffHighlight,
  showAffectedMenus = false,
  highlightAffectedMenus = false,
  dimmed = false,
}: PermissionListRowProps) {
  const { t } = useI18n();
  const label = resolvePermissionDisplayLabel(permission, t);
  const impliedOnly =
    heldPermissions != null && isPermissionImpliedOnly(permission, heldPermissions);
  const resolvedSource: PermissionRowSource =
    source !== 'none' ? source : impliedOnly ? 'implied' : checked ? 'role' : 'none';

  const statusLabels = {
    allowed: t('users.permissionsModal.statusIconAllowed'),
    denied: t('users.permissionsModal.statusIconDenied'),
    custom: t('users.permissionsModal.statusIconCustom'),
  };

  const affectedMenus = useMemo(
    () => (showAffectedMenus ? getMenuItemsAffectedByPermission(permission) : []),
    [permission, showAffectedMenus]
  );

  const menuTooltip =
    affectedMenus.length > 0
      ? t('users.roleDrawer.menuColumnTooltip', {
          menus: affectedMenus.map((m) => t(m.labelKey)).join(', '),
        })
      : t('users.permissionsModal.helpNoMenus');

  const diffBackground =
    diffHighlight === 'added'
      ? '#f6ffed'
      : diffHighlight === 'removed'
        ? '#fff2f0'
        : diffHighlight === 'changed'
          ? '#fffbe6'
          : diffHighlight === 'same'
            ? '#f6ffed88'
            : undefined;

  const highlightMenus = highlightAffectedMenus || (showAffectedMenus && focused);

  return (
    <div
      role="listitem"
      tabIndex={0}
      onClick={onFocus}
      onFocus={onFocus}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 10,
        padding: '8px 10px',
        borderRadius: 6,
        borderBottom: '1px solid rgba(0,0,0,0.06)',
        background: selected
          ? '#f0f5ff'
          : focused || highlightAffectedMenus
            ? '#e6f4ff'
            : diffBackground,
        opacity: dimmed ? 0.38 : 1,
        outline: focused ? '2px solid #1677ff' : undefined,
        outlineOffset: focused ? -2 : undefined,
        borderLeft:
          diffHighlight === 'added'
            ? '3px solid #52c41a'
            : diffHighlight === 'removed'
              ? '3px solid #ff4d4f'
              : diffHighlight === 'changed'
                ? '3px solid #faad14'
                : undefined,
      }}
    >
      {selectionEnabled && onSelectedChange ? (
        <Checkbox
          checked={selected}
          disabled={disabled}
          aria-label={t('users.permissionsModal.selectPermission', { permission: label })}
          onClick={(e) => e.stopPropagation()}
          onChange={(e) => onSelectedChange(e.target.checked)}
        />
      ) : null}

      {mode === 'switch' ? (
        <Switch
          size="small"
          checked={checked}
          disabled={disabled}
          onChange={onChange}
          aria-label={label}
        />
      ) : (
        <Checkbox
          checked={checked}
          disabled={disabled}
          onClick={(e) => e.stopPropagation()}
          onChange={(e) => onChange(e.target.checked)}
        />
      )}

      {showStatusIcon ? (
        <StatusIcon checked={checked} source={resolvedSource} labels={statusLabels} />
      ) : null}

      <div style={{ flex: 1, minWidth: 0 }} data-permission-control="toggle">
        <PermissionHoverHelp permission={permission} catalogDescription={catalogDescription}>
          <Typography.Text style={{ fontSize: 13, display: 'block' }}>
            <HighlightedText text={label} query={searchQuery} />
            {heldPermissions && resolvedSource !== 'implied' ? (
              <ImpliedPermissionTag permission={permission} heldPermissions={heldPermissions} />
            ) : null}
          </Typography.Text>
        </PermissionHoverHelp>
      </div>

      {showAffectedMenus ? (
        <div
          style={{ flex: '0 1 220px', minWidth: 120, maxWidth: 260 }}
          data-permission-control="menus"
        >
          <PermissionMenuTags
            items={affectedMenus}
            highlighted={highlightMenus}
            maxVisible={2}
            size="small"
            tooltipTitle={menuTooltip}
          />
        </div>
      ) : null}

      <Tag
        style={{
          margin: 0,
          fontSize: 11,
          fontFamily: 'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace',
          color: 'rgba(0,0,0,0.45)',
          background: 'rgba(0,0,0,0.04)',
          border: 'none',
          maxWidth: 160,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
        }}
      >
        <HighlightedText text={permission} query={searchQuery} />
      </Tag>

      <SourceTag source={resolvedSource} t={t} />

      <PermissionHelpTooltip permission={permission} catalogDescription={catalogDescription} />
    </div>
  );
}
