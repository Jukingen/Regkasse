import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { CriticalActionModal } from '@/components/CriticalActionModal';

const deLabels: Record<string, string> = {
  'common.criticalAction.alertTitle': 'Kritische Aktion',
  'common.criticalAction.irreversibleAck':
    'Ich verstehe, dass diese Aktion nicht rückgängig gemacht werden kann',
  'common.criticalAction.typeToConfirm': 'Zur Bestätigung „{{phrase}}“ eingeben',
  'common.criticalAction.next': 'Weiter',
  'common.criticalAction.back': 'Zurück',
  'common.criticalAction.confirmAction': 'Aktion bestätigen',
  'common.criticalAction.secondAuthTitle': 'Zweite Authentifizierung erforderlich',
  'common.criticalAction.secondAuthAlertTitle': 'Zweite Authentifizierung erforderlich',
  'common.criticalAction.secondAuthAlertBody':
    'Geben Sie Ihren 2FA-Code ein oder lassen Sie die Aktion von einem Super-Admin freigeben.',
  'common.criticalAction.secondAuthPlaceholder': '2FA-Code eingeben',
  'common.criticalAction.superAdminHint':
    'Alternativ kann ein Super-Admin diese Aktion im Admin-Panel freigeben.',
  'common.buttons.cancel': 'Abbrechen',
};

vi.mock('@/i18n/I18nProvider', () => ({
  useI18n: () => ({
    t: (key: string, params?: Record<string, string>) => {
      let value = deLabels[key] ?? key;
      if (params?.phrase) {
        value = value.replace('{{phrase}}', params.phrase);
      }
      return value;
    },
  }),
}));

describe('CriticalActionModal', () => {
  it('keeps confirm disabled until ack and phrase match', () => {
    render(
      <CriticalActionModal
        open
        title="Löschen"
        description="Alle Produkte deaktivieren"
        warning="Irreversibel"
        confirmText="DEAKTIVIEREN"
        requireSecondAuth={false}
        onConfirm={() => undefined}
        onCancel={() => undefined}
      />
    );

    const confirmBtn = screen.getByRole('button', { name: 'DEAKTIVIEREN' });
    expect(confirmBtn).toBeDisabled();

    fireEvent.click(screen.getByRole('checkbox'));
    fireEvent.change(screen.getByPlaceholderText(/DEAKTIVIEREN/), {
      target: { value: 'DEAKTIVIEREN' },
    });

    expect(confirmBtn).not.toBeDisabled();
  });

  it('advances to second auth step when required', () => {
    const onConfirm = vi.fn();
    render(
      <CriticalActionModal
        open
        title="Löschen"
        description="Desc"
        warning="Warn"
        confirmText="CONFIRM"
        requireSecondAuth
        onConfirm={onConfirm}
        onCancel={() => undefined}
      />
    );

    fireEvent.click(screen.getByRole('checkbox'));
    fireEvent.change(screen.getByPlaceholderText(/CONFIRM/), {
      target: { value: 'CONFIRM' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Weiter' }));

    expect(screen.getByText('Zweite Authentifizierung erforderlich')).toBeTruthy();
    expect(onConfirm).not.toHaveBeenCalled();

    fireEvent.change(screen.getByPlaceholderText(/2FA/), {
      target: { value: '123456' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Aktion bestätigen' }));

    expect(onConfirm).toHaveBeenCalledWith({ secondAuthCode: '123456' });
  });
});
