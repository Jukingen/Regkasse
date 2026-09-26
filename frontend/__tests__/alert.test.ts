/**
 * @jest-environment node
 */
import { Alert, Platform } from 'react-native';

import { showAlert } from '../utils/alert';

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
