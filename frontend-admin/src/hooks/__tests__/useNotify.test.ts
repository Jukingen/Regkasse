import { act, renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useNotify } from '@/hooks/useNotify';

const message = {
  success: vi.fn(),
  error: vi.fn(),
  warning: vi.fn(),
  info: vi.fn(),
  loading: vi.fn(),
  destroy: vi.fn(),
  open: vi.fn(),
};
const notification = {
  success: vi.fn(),
  error: vi.fn(),
  warning: vi.fn(),
  info: vi.fn(),
};

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({ message, notification, modal: {} }),
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string, values?: Record<string, string | number>) =>
      values ? `${key}:${JSON.stringify(values)}` : `t:${key}`,
  }),
}));

vi.mock('@/shared/errors/openApiErrorMessage', () => ({
  openApiErrorMessage: vi.fn(),
}));

describe('useNotify', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('translates dotted i18n keys for success/error', () => {
    const { result } = renderHook(() => useNotify());

    act(() => {
      result.current.successKey('users.create.success');
      result.current.errorKey('common.errorGeneric');
    });

    expect(message.success).toHaveBeenCalledWith(
      expect.objectContaining({ content: 't:users.create.success' })
    );
    expect(message.error).toHaveBeenCalledWith(
      expect.objectContaining({ content: 't:common.errorGeneric' })
    );
  });

  it('passes through raw (already translated) strings', () => {
    const { result } = renderHook(() => useNotify());

    act(() => {
      result.current.success('Backup queued');
    });

    expect(message.success).toHaveBeenCalledWith(
      expect.objectContaining({ content: 'Backup queued' })
    );
  });

  it('uses notification panel when description is set', () => {
    const { result } = renderHook(() => useNotify());

    act(() => {
      result.current.error('Title', { description: 'Details' });
    });

    expect(notification.error).toHaveBeenCalledWith(
      expect.objectContaining({
        message: 'Title',
        description: 'Details',
        placement: 'topRight',
      })
    );
  });
});
