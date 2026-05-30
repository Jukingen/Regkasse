'use client';

import React, { useEffect, useState } from 'react';
import { Button, Input, Space } from 'antd';
import { CheckOutlined, CloseOutlined, EditOutlined } from '@ant-design/icons';

interface EditableCellProps {
  value: string;
  onSave: (value: string) => Promise<void>;
  disabled?: boolean;
}

export default function EditableCell({ value, onSave, disabled = false }: EditableCellProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(value);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!editing) {
      setDraft(value);
    }
  }, [value, editing]);

  const cancel = () => {
    setEditing(false);
    setDraft(value);
  };

  const save = async () => {
    const trimmed = draft.trim();
    if (!trimmed) {
      cancel();
      return;
    }
    if (trimmed === value) {
      setEditing(false);
      return;
    }
    setSaving(true);
    try {
      await onSave(trimmed);
      setEditing(false);
    } finally {
      setSaving(false);
    }
  };

  if (disabled || !editing) {
    return (
      <Space size="small">
        <span style={{ fontWeight: 500 }}>{value}</span>
        {!disabled ? (
          <Button
            type="text"
            size="small"
            icon={<EditOutlined />}
            aria-label="Edit name"
            onClick={() => {
              setDraft(value);
              setEditing(true);
            }}
          />
        ) : null}
      </Space>
    );
  }

  return (
    <Space.Compact style={{ width: '100%', maxWidth: 280 }}>
      <Input
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onPressEnter={() => void save()}
        disabled={saving}
        autoFocus
      />
      <Button icon={<CheckOutlined />} loading={saving} onClick={() => void save()} />
      <Button icon={<CloseOutlined />} disabled={saving} onClick={cancel} />
    </Space.Compact>
  );
}
