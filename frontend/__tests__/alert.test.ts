/**
 * @jest-environment node
 */
import { Alert, Platform } from 'react-native';

import { showAlert, showConfirm } from '../utils/alert';

jest.mock('react-native', () => {
  const osRef = { current: 'ios' as string };
  const alert = jest.fn();
  return {
    Alert: { alert },
    Platform: {
      get OS() {
        return osRef.current;
      },
      setOS(next: string) {
        osRef.current = next;
      },
    },
  };
});

function setPlatform(os: 'ios' | 'android' | 'web') {
  (Platform as unknown as { setOS: (next: string) => void }).setOS(os);
}

describe('showAlert', () => {
  const windowAlert = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    setPlatform('ios');
    Object.defineProperty(globalThis, 'window', {
      configurable: true,
      value: { alert: windowAlert },
    });
  });

  afterEach(() => {
    Reflect.deleteProperty(globalThis, 'window');
  });

  it('uses window.alert on web and does not call React Native Alert', () => {
    setPlatform('web');
    showAlert('Fehler', 'Online-Zahlung fehlgeschlagen.');
    expect(windowAlert).toHaveBeenCalledTimes(1);
    expect(windowAlert).toHaveBeenCalledWith('Fehler\n\nOnline-Zahlung fehlgeschlagen.');
    expect(Alert.alert).not.toHaveBeenCalled();
  });

  it('uses window.alert with the title only when message is omitted', () => {
    setPlatform('web');
    showAlert('Hinweis');
    expect(windowAlert).toHaveBeenCalledWith('Hinweis');
    expect(Alert.alert).not.toHaveBeenCalled();
  });

  it('keeps React Native Alert on native and does not touch window.alert', () => {
    setPlatform('ios');
    showAlert('Fehler', 'Zahlung fehlgeschlagen');
    expect(Alert.alert).toHaveBeenCalledTimes(1);
    expect(Alert.alert).toHaveBeenCalledWith('Fehler', 'Zahlung fehlgeschlagen');
    expect(windowAlert).not.toHaveBeenCalled();
  });
});

describe('showConfirm', () => {
  const windowAlert = jest.fn();
  const windowConfirm = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    setPlatform('ios');
    windowConfirm.mockReturnValue(false);
    Object.defineProperty(globalThis, 'window', {
      configurable: true,
      value: { alert: windowAlert, confirm: windowConfirm },
    });
  });

  afterEach(() => {
    Reflect.deleteProperty(globalThis, 'window');
  });

  it('uses window.confirm on web and runs the clear action only when accepted', () => {
    setPlatform('web');
    windowConfirm.mockReturnValue(true);
    const onConfirm = jest.fn();
    const onCancel = jest.fn();

    showConfirm('Alle Tische leeren?', '2 offene Tische werden geleert.', [
      { text: 'Abbrechen', style: 'cancel', onPress: onCancel },
      { text: 'Alle leeren', style: 'destructive', onPress: onConfirm },
    ]);

    expect(windowConfirm).toHaveBeenCalledWith(
      'Alle Tische leeren?\n\n2 offene Tische werden geleert.'
    );
    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(onCancel).not.toHaveBeenCalled();
    expect(Alert.alert).not.toHaveBeenCalled();
  });

  it('does not clear on web when the cashier cancels', () => {
    setPlatform('web');
    windowConfirm.mockReturnValue(false);
    const onConfirm = jest.fn();
    const onCancel = jest.fn();

    showConfirm('Alle Tische leeren?', '1 offene Tische werden geleert.', [
      { text: 'Abbrechen', style: 'cancel', onPress: onCancel },
      { text: 'Alle leeren', style: 'destructive', onPress: onConfirm },
    ]);

    expect(onConfirm).not.toHaveBeenCalled();
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('keeps React Native Alert buttons on native', () => {
    setPlatform('ios');
    const buttons = [
      { text: 'Abbrechen', style: 'cancel' as const },
      { text: 'Alle leeren', style: 'destructive' as const, onPress: () => undefined },
    ];
    showConfirm('Alle Tische leeren?', '2 offene Tische werden geleert.', buttons);
    expect(Alert.alert).toHaveBeenCalledWith(
      'Alle Tische leeren?',
      '2 offene Tische werden geleert.',
      buttons
    );
    expect(windowConfirm).not.toHaveBeenCalled();
  });
});
