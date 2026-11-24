import { afterEach, beforeEach, describe, expect, it, jest } from '@jest/globals';
import { renderHook } from '@testing-library/react-native';
import { Platform } from 'react-native';

import { usePosKeyboardShortcuts } from '../hooks/usePosKeyboardShortcuts';

type Listener = (event: Record<string, unknown>) => void;

function installFakeDocument() {
  const listeners = new Set<Listener>();
  const fake = {
    addEventListener: jest.fn((type: string, listener: Listener) => {
      if (type === 'keydown') listeners.add(listener);
    }),
    removeEventListener: jest.fn((type: string, listener: Listener) => {
      if (type === 'keydown') listeners.delete(listener);
    }),
  };
  (globalThis as { document?: unknown }).document = fake;

  return {
    fake,
    listenerCount: () => listeners.size,
    press(key: string, extra: Record<string, unknown> = {}) {
      const event = { key, target: { tagName: 'DIV' }, preventDefault: jest.fn(), ...extra };
      listeners.forEach((listener) => { listener(event); });
      return event;
    },
  };
}

function setPlatform(os: string) {
  Object.defineProperty(Platform, 'OS', { configurable: true, value: os });
}

describe('usePosKeyboardShortcuts', () => {
  const originalOs = Platform.OS;
  const originalDocument = (globalThis as { document?: unknown }).document;

  beforeEach(() => {
    setPlatform('web');
  });

  afterEach(() => {
    setPlatform(originalOs);
    (globalThis as { document?: unknown }).document = originalDocument;
  });

  it('runs the handler and blocks the browser default', async () => {
    const dom = installFakeDocument();
    const pay = jest.fn<() => void>();
    await renderHook(() => { usePosKeyboardShortcuts({ pay }); });

    const event = dom.press('F6');

    expect(pay).toHaveBeenCalledTimes(1);
    expect(event.preventDefault).toHaveBeenCalledTimes(1);
  });

  it('leaves keys without a handler to the browser (F5 reload)', async () => {
    const dom = installFakeDocument();
    await renderHook(() => { usePosKeyboardShortcuts({ pay: jest.fn<() => void>() }); });

    const event = dom.press('F5');

    expect(event.preventDefault).not.toHaveBeenCalled();
  });

  it('keeps the default when a handler reports it did not handle the key', async () => {
    const dom = installFakeDocument();
    const escape = jest.fn<() => boolean>(() => false);
    await renderHook(() => { usePosKeyboardShortcuts({ escape }); });

    const event = dom.press('Escape');

    expect(escape).toHaveBeenCalledTimes(1);
    expect(event.preventDefault).not.toHaveBeenCalled();
  });

  it('does not open help while typing but still reacts to F-keys', async () => {
    const dom = installFakeDocument();
    const help = jest.fn<() => void>();
    const pay = jest.fn<() => void>();
    await renderHook(() => { usePosKeyboardShortcuts({ help, pay }); });

    dom.press('?', { target: { tagName: 'INPUT' } });
    expect(help).not.toHaveBeenCalled();

    dom.press('F6', { target: { tagName: 'INPUT' } });
    expect(pay).toHaveBeenCalledTimes(1);
  });

  it('uses the latest handlers without re-registering the listener', async () => {
    const dom = installFakeDocument();
    const first = jest.fn<() => void>();
    const second = jest.fn<() => void>();
    const { rerender } = await renderHook(
      (props: { handler: () => void }) => { usePosKeyboardShortcuts({ customer: props.handler }); },
      { initialProps: { handler: first } }
    );
    await rerender({ handler: second });

    dom.press('F2');

    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledTimes(1);
    expect(dom.fake.addEventListener).toHaveBeenCalledTimes(1);
  });

  it('does nothing while disabled and cleans up on unmount', async () => {
    const dom = installFakeDocument();
    const pay = jest.fn<() => void>();
    const { rerender, unmount } = await renderHook(
      (props: { enabled: boolean }) => { usePosKeyboardShortcuts({ pay }, props.enabled); },
      { initialProps: { enabled: false } }
    );

    dom.press('F6');
    expect(pay).not.toHaveBeenCalled();
    expect(dom.listenerCount()).toBe(0);

    await rerender({ enabled: true });
    expect(dom.listenerCount()).toBe(1);

    await unmount();
    expect(dom.listenerCount()).toBe(0);
  });

  it('is a no-op on native', async () => {
    setPlatform('ios');
    const dom = installFakeDocument();
    await renderHook(() => { usePosKeyboardShortcuts({ pay: jest.fn<() => void>() }); });

    expect(dom.fake.addEventListener).not.toHaveBeenCalled();
  });
});
