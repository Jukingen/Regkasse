'use client';

import { Tag, Typography } from 'antd';

import { FORMAT_EMPTY_DISPLAY } from '@/i18n/formatting';

export function ProductCategoryBadge({
  name,
  color,
  onClick,
  title,
}: {
  name?: string | null;
  color?: string | null;
  onClick?: () => void;
  title?: string;
}) {
  const label = name?.trim();
  if (!label) {
    return <Typography.Text type="secondary">{FORMAT_EMPTY_DISPLAY}</Typography.Text>;
  }

  return (
    <Tag
      color={color || undefined}
      title={title}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      onClick={
        onClick
          ? (event) => {
              event.preventDefault();
              event.stopPropagation();
              onClick();
            }
          : undefined
      }
      onKeyDown={
        onClick
          ? (event) => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                onClick();
              }
            }
          : undefined
      }
      style={{
        marginInlineEnd: 0,
        maxWidth: '100%',
        cursor: onClick ? 'pointer' : undefined,
      }}
    >
      {label}
    </Tag>
  );
}
