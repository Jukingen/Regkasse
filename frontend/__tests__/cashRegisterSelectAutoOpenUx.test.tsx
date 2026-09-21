import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { StyleSheet } from 'react-native';

import CashRegisterSelectScreen from '../app/(screens)/cash-register-select';
import { SoftColors } from '../constants/SoftTheme';
import deAuth from '../i18n/locales/de/auth.json';
import deCommon from '../i18n/locales/de/common.json';
import deSettings from '../i18n/locales/de/settings.json';
import deShift from '../i18n/locales/de/shift.json';
import {
  POS_MONATSBELEG_NOTIFY_MANAGER_PATH,
  POS_OPEN_REQUESTS_PATH,
  POS_SELECTABLE_REGISTERS_PATH,
} from '../services/api/cashRegisterService';
import { apiClient } from '../services/api/config';

jest.mock('../services/api/config', () => ({
  apiClient: {
    get: jest.fn(),
    post: jest.fn(),
  },
}));

jest.mock('expo-router', () => ({
  Redirect: () => null,
  router: { replace: jest.fn() },
}));

jest.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({
    isAuthenticated: true,
    isAuthReady: true,
    user: {
      role: 'Cashier',
      permissions: ['shift.open'],
      currentCashRegisterId: null,
      mustChangePasswordOnNextLogin: false,
    },
    logout: jest.fn(),
    setCurrentCashRegisterId: jest.fn(async () => undefined),
  }),
}));

jest.mock('../src/components/common/WaveLoader', () => ({
  WaveLoader: () => null,
}));

const REGISTER_ID = '11111111-1111-4111-8111-111111111111';

class AutoOpenHttpError extends Error {
  readonly status: number;
  readonly data: { success: false; code: string; message: string };

  constructor(status: number, code: string) {
    super('fail');
    this.name = 'AutoOpenHttpError';
    this.status = status;
    this.data = { success: false, code, message: 'fail' };
  }
}

type BannerCase = {
  code: string;
  httpStatus?: number;
  tone: 'error' | 'info';
  text: RegExp;
  button: string | null;
};

const CASES: BannerCase[] = [
  {
    code: 'REGISTER_MAINTENANCE',
    tone: 'error',
    text: /Wartungsmodus/,
    button: null,
  },
  {
    code: 'REGISTER_DISABLED',
    tone: 'error',
    text: /deaktiviert/,
    button: null,
  },
  {
    code: 'REGISTER_INACTIVE',
    tone: 'error',
    text: /inaktiv/,
    button: null,
  },
  {
    code: 'REGISTER_ASSIGNED_TO_OTHER_USER',
    tone: 'error',
    text: /anderen Kassierer zugewiesen/,
    button: 'Öffnung anfordern',
  },
  {
    code: 'REGISTER_CONFLICT_OTHER_USER',
    tone: 'info',
    text: /bereits von einem anderen Kassierer geöffnet/,
    button: 'Bereits geöffnet',
  },
  {
    code: 'REGISTER_ACTOR_HAS_OTHER_OPEN',
    tone: 'info',
    text: /bereits eine andere Kasse geöffnet/,
    button: 'Andere Kasse schließen',
  },
  {
    code: 'REGISTER_MONATSBELEG_REQUIRED',
    tone: 'info',
    text: /Monatsbeleg fehlt/,
    button: 'Manager kontaktieren',
  },
  {
    code: 'REGISTER_STARTBELEG_REQUIRED',
    tone: 'info',
    text: /Startbeleg fehlt/,
    button: 'Manager kontaktieren',
  },
  {
    code: 'REGISTER_INVALID_STATE',
    tone: 'error',
    text: /diesem Zustand nicht geöffnet/,
    button: 'Erneut laden',
  },
  {
    code: 'REGISTER_INACTIVE',
    httpStatus: 403,
    tone: 'error',
    text: /Mandanten-Admin geöffnet/,
    button: null,
  },
  {
    code: 'REGISTER_UNAVAILABLE',
    tone: 'error',
    text: /nicht verfügbar/,
    button: null,
  },
];

async function showFailure(code: string, httpStatus = 400) {
  jest.mocked(apiClient.get).mockImplementation((async (path: string) => {
    if (path === POS_SELECTABLE_REGISTERS_PATH) {
      return {
        registers: [
          {
            id: REGISTER_ID,
            registerNumber: '1',
            status: 'Closed',
            location: 'Theke',
          },
        ],
      };
    }
    return [];
  }) as typeof apiClient.get);
  jest.mocked(apiClient.post).mockImplementation((async (path: string) => {
    if (path === '/pos/cash-register/default') {
      return { cashRegisterId: REGISTER_ID };
    }
    if (path === '/pos/shift/auto-open') {
      throw new AutoOpenHttpError(httpStatus, code);
    }
    if (path === '/pos/shift/auto-close') return null;
    if (path === POS_OPEN_REQUESTS_PATH) {
      return {
        succeeded: true,
        request: {
          id: 'req-1',
          cashRegisterId: REGISTER_ID,
          status: 'Pending',
          requestedAt: '2026-09-21T12:00:00Z',
        },
      };
    }
    if (path === POS_MONATSBELEG_NOTIFY_MANAGER_PATH) {
      return { ok: true, code: 'OK', message: '' };
    }
    throw new Error(`unexpected POST ${path}`);
  }) as typeof apiClient.post);

  await render(<CashRegisterSelectScreen />);
  await fireEvent.press(await screen.findByTestId('cash-register-select-option'));
}

function selectableGetCount(): number {
  return jest
    .mocked(apiClient.get)
    .mock.calls.filter((call) => call[0] === POS_SELECTABLE_REGISTERS_PATH).length;
}

describe('cash register select auto-open UX', () => {
  beforeAll(async () => {
    if (!i18n.isInitialized) {
      await i18n.use(initReactI18next).init({
        lng: 'de',
        fallbackLng: 'de',
        ns: ['settings', 'shift', 'auth', 'common'],
        defaultNS: 'common',
        resources: {
          de: {
            settings: deSettings,
            shift: deShift,
            auth: deAuth,
            common: deCommon,
          },
        },
        interpolation: { escapeValue: false },
      });
    }
    await i18n.changeLanguage('de');
  });

  beforeEach(() => {
    jest.mocked(apiClient.get).mockReset();
    jest.mocked(apiClient.post).mockReset();
  });

  it.each(CASES)('$code http $httpStatus → $tone banner', async (row) => {
    await showFailure(row.code, row.httpStatus ?? 400);
    const banner = await screen.findByTestId(
      row.tone === 'info' ? 'register-select-info-banner' : 'register-select-error-banner'
    );
    expect(screen.getByText(row.text)).toBeTruthy();
    const flat = StyleSheet.flatten(banner.props.style);
    if (row.tone === 'info') {
      expect(flat?.backgroundColor).toBe(SoftColors.infoBg);
      expect(flat?.borderColor).toBe(SoftColors.info);
      expect(screen.queryByTestId('register-select-error-banner')).toBeNull();
    } else {
      expect(flat?.backgroundColor).toBe(SoftColors.errorBg);
      expect(flat?.borderColor).toBe(SoftColors.error);
      expect(screen.queryByTestId('register-select-info-banner')).toBeNull();
    }
    if (row.button) {
      expect(screen.getByText(row.button)).toBeTruthy();
    } else {
      expect(screen.queryByTestId('register-select-banner-action')).toBeNull();
    }
  });

  it('REGISTER_ASSIGNED_TO_OTHER_USER posts an open request', async () => {
    await showFailure('REGISTER_ASSIGNED_TO_OTHER_USER');
    await fireEvent.press(await screen.findByText('Öffnung anfordern'));
    await waitFor(() => {
      expect(apiClient.post).toHaveBeenCalledWith(POS_OPEN_REQUESTS_PATH, {
        registerId: REGISTER_ID,
      });
    });
  });

  it('REGISTER_STARTBELEG_REQUIRED does not post', async () => {
    await showFailure('REGISTER_STARTBELEG_REQUIRED');
    await screen.findByText(/Startbeleg fehlt/);
    const postsBefore = jest.mocked(apiClient.post).mock.calls.length;
    await fireEvent.press(screen.getByText('Manager kontaktieren'));
    await waitFor(() => {
      expect(jest.mocked(apiClient.post).mock.calls.length).toBe(postsBefore);
    });
    expect(apiClient.post).not.toHaveBeenCalledWith(
      POS_OPEN_REQUESTS_PATH,
      expect.anything()
    );
    expect(apiClient.post).not.toHaveBeenCalledWith(
      POS_MONATSBELEG_NOTIFY_MANAGER_PATH,
      expect.anything()
    );
    expect(apiClient.post).not.toHaveBeenCalledWith('/pos/shift/auto-close', expect.anything());
  });

  it('REGISTER_INVALID_STATE reloads the list and does not post', async () => {
    await showFailure('REGISTER_INVALID_STATE');
    await screen.findByText('Erneut laden');
    const getsBefore = selectableGetCount();
    const postsBefore = jest.mocked(apiClient.post).mock.calls.length;
    await fireEvent.press(screen.getByText('Erneut laden'));
    await waitFor(() => {
      expect(selectableGetCount()).toBeGreaterThan(getsBefore);
    });
    expect(jest.mocked(apiClient.post).mock.calls.length).toBe(postsBefore);
    expect(apiClient.post).not.toHaveBeenCalledWith(
      POS_OPEN_REQUESTS_PATH,
      expect.anything()
    );
  });
});
