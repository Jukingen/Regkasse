'use client';

import { Button, Input, Space } from 'antd';
import React from 'react';

const CATEGORY_ICON_PRESETS = [
  '🥗',
  '🍕',
  '🌶️',
  '🍝',
  '🍔',
  '🥙',
  '🥪',
  '🥖',
  '🥟',
  '🍟',
  '🍰',
  '🥤',
  '🥫',
  '🍷',
  '☕',
  '🍿',
  '🍽️',
  '🍦',
  '🍺',
  '🧀',
  '📦',
] as const;

export const DEFAULT_CATEGORY_ICON = '📦';

interface CategoryIconInputProps {
  value?: string | null;
  onChange?: (value: string) => void;
  placeholder?: string;
}

export default function CategoryIconInput({
  value,
  onChange,
  placeholder,
}: CategoryIconInputProps) {
  const current = value ?? '';

  return (
    <Space orientation="vertical" size="small" style={{ width: '100%' }}>
      <Input
        value={current}
        maxLength={50}
        placeholder={placeholder}
        addonBefore={<span aria-hidden>{current.trim() ? current : DEFAULT_CATEGORY_ICON}</span>}
        onChange={(event) => onChange?.(event.target.value)}
      />
      <Space wrap size={4}>
        {CATEGORY_ICON_PRESETS.map((emoji) => (
          <Button
            key={emoji}
            type={current === emoji ? 'primary' : 'default'}
            size="small"
            onClick={() => onChange?.(emoji)}
            aria-label={emoji}
          >
            {emoji}
          </Button>
        ))}
      </Space>
    </Space>
  );
}
