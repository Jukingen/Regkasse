import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { ActivityDto } from '@/api/manual/activityEvents';
import { CATALOG_ACTIVITY_EVENTS } from '@/features/activity-notifications/activityTypes';
import { ActivityNotificationList } from '@/features/activity-notifications/components/ActivityNotificationList';
import { formatActivityTitle } from '@/features/activity-notifications/formatActivityTitle';
import { I18nProvider } from '@/i18n';
import { technicalConsole } from '@/shared/dev/technicalConsole';

const TRANSLATED_TITLES: Record<(typeof CATALOG_ACTIVITY_EVENTS)[number], string> = {
  TenantCountryChanged: 'Mandantenland geändert',
  TenantCountryChangedHistoricalPreserved: 'Historische Belege unverändert',
  QrRechnungPayloadBuilt: 'QR-Rechnung erstellt',
  QrRechnungPdfGenerated: 'QR-Rechnung-PDF erzeugt',
  EinvoiceValidated: 'E-Rechnung geprüft',
  EinvoiceSubmitted: 'E-Rechnung übermittelt',
  EinvoiceSubmissionFailed: 'E-Rechnung abgelehnt',
  PeppolParticipantRegistered: 'Peppol-Teilnehmer registriert',
};

function activity(type: string): ActivityDto {
  return {
    id: type,
    type,
    severity: 'Info',
    title: 'server-title',
    description: 'server-description',
    isRead: true,
    createdAtUtc: '2026-09-30T10:00:00.000Z',
  };
}

describe('activity feed catalog titles', () => {
  it('renders the translated title for each new catalog event', () => {
    render(
      <I18nProvider>
        <ActivityNotificationList
          items={CATALOG_ACTIVITY_EVENTS.map((type) => activity(type))}
          loading={false}
          emptyLabel="empty"
          onMarkRead={() => undefined}
        />
      </I18nProvider>
    );

    for (const type of CATALOG_ACTIVITY_EVENTS) {
      expect(screen.getByText(TRANSLATED_TITLES[type])).toBeInTheDocument();
    }
    expect(screen.queryByText('server-title')).not.toBeInTheDocument();
  });

  it('warns and shows the generic unknown title when a catalog key is missing', () => {
    const warn = vi.spyOn(technicalConsole, 'warn').mockImplementation(() => ({}) as never);
    const t = (key: string) => (key === 'activity.events.unknown.title' ? 'Unknown event' : key);

    expect(formatActivityTitle(activity('EinvoiceValidated'), t)).toBe('Unknown event');
    expect(warn).toHaveBeenCalledWith(
      'Activity feed translation key is missing',
      expect.objectContaining({ key: 'activity.events.EinvoiceValidated.title' })
    );
    expect(formatActivityTitle(activity('EinvoiceValidated'), t)).not.toBe('EinvoiceValidated');
    warn.mockRestore();
  });
});
