import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen } from '@testing-library/react-native';
import React from 'react';

import { AppointmentDayCalendar } from '../components/AppointmentDayCalendar';
import { changeLanguage } from '../i18n';
import type { AppointmentDayItem, AppointmentDayStaff } from '../components/AppointmentDayCalendar';

describe('AppointmentDayCalendar', () => {
  beforeEach(async () => {
    await changeLanguage('de');
  });

  it('shows today appointments by staff column and prefills empty slots', async () => {
    const day = new Date(2026, 9, 10);
    const start = new Date(day);
    start.setHours(10, 0, 0, 0);
    const end = new Date(start.getTime() + 45 * 60_000);

    const staff: AppointmentDayStaff[] = [
      { id: 'staff-1', name: 'Anna Kasse', role: 'Cashier' },
    ];
    const appointments: AppointmentDayItem[] = [
      {
        staffId: 'staff-1',
        startUtc: start.toISOString(),
        endUtc: end.toISOString(),
        status: 'Booked',
      },
    ];
    const onEmptySlotPress = jest.fn();

    await render(
      <AppointmentDayCalendar
        day={day}
        staff={staff}
        appointments={appointments}
        onEmptySlotPress={onEmptySlotPress}
      />
    );

    expect(screen.getByText('Anna Kasse')).toBeTruthy();
    expect(screen.getByLabelText('Anna Kasse 10:00 Booked')).toBeTruthy();
    expect(screen.getByLabelText('Anna Kasse 10:30 Booked')).toBeTruthy();

    await fireEvent.press(screen.getByLabelText('Freier Slot 09:00 für Anna Kasse'));
    expect(onEmptySlotPress).toHaveBeenCalledWith('staff-1', '09:00');
  });
});
