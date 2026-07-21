import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  NOTIFY_DEFAULTS,
  NotificationService,
  notifyError,
  notifyErrorSafe,
  notifyInfo,
  notifySuccess,
  notifyWarning,
  registerNotificationApis,
  unregisterNotificationApis,
} from '@/lib/notificationService';

describe('notificationService', () => {
  const message = {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    info: vi.fn(),
  };
  const notification = {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    info: vi.fn(),
  };

  afterEach(() => {
    unregisterNotificationApis();
    vi.clearAllMocks();
  });

  it('exposes defaults for duration and topRight placement', () => {
    expect(NOTIFY_DEFAULTS.placement).toBe('topRight');
    expect(NOTIFY_DEFAULTS.messageDuration).toBe(3);
    expect(NOTIFY_DEFAULTS.notificationDuration).toBe(8);
    expect(NotificationService.defaults).toEqual(NOTIFY_DEFAULTS);
  });

  it('queues toasts until App APIs are registered', () => {
    notifySuccess('Queued ok');
    expect(message.success).not.toHaveBeenCalled();

    registerNotificationApis({
      message: message as never,
      notification: notification as never,
    });

    expect(message.success).toHaveBeenCalledWith(
      expect.objectContaining({ content: 'Queued ok', duration: 3 })
    );
  });

  it('routes success/error/warning/info to message API by default', () => {
    registerNotificationApis({
      message: message as never,
      notification: notification as never,
    });

    notifySuccess('S');
    notifyError('E');
    notifyWarning('W');
    notifyInfo('I');

    expect(message.success).toHaveBeenCalledWith(expect.objectContaining({ content: 'S' }));
    expect(message.error).toHaveBeenCalledWith(expect.objectContaining({ content: 'E' }));
    expect(message.warning).toHaveBeenCalledWith(expect.objectContaining({ content: 'W' }));
    expect(message.info).toHaveBeenCalledWith(expect.objectContaining({ content: 'I' }));
  });

  it('uses notification panel when mode or description is set', () => {
    registerNotificationApis({
      message: message as never,
      notification: notification as never,
    });

    notifyError('Title', {
      mode: 'notification',
      description: 'Detail',
      placement: 'bottomLeft',
    });

    expect(notification.error).toHaveBeenCalledWith(
      expect.objectContaining({
        message: 'Title',
        description: 'Detail',
        duration: 8,
        placement: 'bottomLeft',
      })
    );
    expect(message.error).not.toHaveBeenCalled();
  });

  it('notifyErrorSafe suppresses canceled requests', async () => {
    registerNotificationApis({
      message: message as never,
      notification: notification as never,
    });

    const { CanceledError } = await import('axios');
    notifyErrorSafe('Should not show', new CanceledError('canceled'));
    expect(message.error).not.toHaveBeenCalled();

    notifyErrorSafe('Should show', new Error('real'));
    expect(message.error).toHaveBeenCalledWith(expect.objectContaining({ content: 'Should show' }));
  });
});
