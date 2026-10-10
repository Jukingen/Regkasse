import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import React from 'react';
import { Alert } from 'react-native';

import AppointmentScreen from '../app/(screens)/appointment';
import PatientRecordScreen from '../app/(screens)/patient-record';
import { changeLanguage } from '../i18n';
import { AppointmentConflictError } from '../services/api/appointmentService';

const mockCustomerCreate = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockGetProducts = jest.fn<(...args: unknown[]) => Promise<unknown[]>>();
const mockCreateAppointment = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockListAppointments = jest.fn<(...args: unknown[]) => Promise<unknown[]>>();
const mockListPosStaff = jest.fn<(...args: unknown[]) => Promise<unknown[]>>();
const mockScheduleReminder = jest.fn<(...args: unknown[]) => Promise<unknown>>();

type MockVerticalProfile = {
  posFeatures: Record<string, boolean>;
  requiredFields: Record<string, string[]>;
  optionalFields: Record<string, string[]>;
  posLayout: string;
};

const verticalProfile: { current: MockVerticalProfile } = {
  current: {
    posFeatures: { patientRecord: true, appointment: false },
    requiredFields: { customer: ['name', 'petName'], product: [] },
    optionalFields: {
      customer: ['phone', 'email', 'petSpecies', 'petBreed', 'petBirthDate'],
      product: [],
    },
    posLayout: 'standard',
  },
};

jest.mock('../contexts/VerticalProfileContext', () => ({
  useVerticalFeatures: () => verticalProfile.current,
}));

jest.mock('../services/api/customerService', () => ({
  customerService: {
    create: (...args: unknown[]) => mockCustomerCreate(...args),
  },
}));

jest.mock('../services/api/productService', () => ({
  getAllProducts: (...args: unknown[]) => mockGetProducts(...args),
}));

jest.mock('../services/api/appointmentService', () => {
  class AppointmentConflictError extends Error {
    readonly code = 'APPOINTMENT_CONFLICT';
    constructor(readonly mockAppointment: unknown) {
      super('APPOINTMENT_CONFLICT');
      this.name = 'AppointmentConflictError';
    }
  }
  return {
    AppointmentConflictError,
    combineLocalDateTime: (date: string, time: string) => {
      const dmy = /^(\d{1,2})\.(\d{1,2})\.(\d{4})$/.exec(date.trim());
      const isoDate = dmy
        ? `${dmy[3]}-${dmy[2].padStart(2, '0')}-${dmy[1].padStart(2, '0')}`
        : date.trim();
      const normalizedTime = time.trim().length === 5 ? `${time.trim()}:00` : time.trim();
      return new Date(`${isoDate}T${normalizedTime}`);
    },
    createAppointment: (...args: unknown[]) => mockCreateAppointment(...args),
    listAppointments: (...args: unknown[]) => mockListAppointments(...args),
  };
});

jest.mock('../services/api/staffService', () => ({
  listPosStaff: (...args: unknown[]) => mockListPosStaff(...args),
}));

jest.mock('../services/appointmentNotifications', () => ({
  scheduleAppointmentReminder: (...args: unknown[]) => mockScheduleReminder(...args),
}));

function enableHairSalonProfile() {
  verticalProfile.current = {
    posFeatures: { patientRecord: false, appointment: true, serviceDuration: true },
    requiredFields: { customer: ['name'], product: ['durationMinutes'] },
    optionalFields: { customer: ['phone', 'email'], product: ['staffId'] },
    posLayout: 'appointment',
  };
  mockGetProducts.mockResolvedValue([
    {
      id: 'service-1',
      name: 'Haarschnitt',
      durationMinutes: 45,
      staffId: 'staff-1',
    },
  ]);
}

describe('vet and hair-salon vertical screens', () => {
  beforeEach(async () => {
    jest.clearAllMocks();
    await changeLanguage('de');
    jest.spyOn(Alert, 'alert').mockImplementation(() => undefined);
    mockCustomerCreate.mockResolvedValue({});
    mockCreateAppointment.mockResolvedValue({
      id: 'apt-1',
      version: 1,
      startUtc: '2026-10-10T08:30:00.000Z',
    });
    mockListAppointments.mockResolvedValue([]);
    mockListPosStaff.mockResolvedValue([
      { id: 'staff-1', name: 'Anna Kasse', role: 'Cashier' },
    ]);
    mockScheduleReminder.mockResolvedValue('appointment-reminder-apt-1');
  });

  it('renders the vet patient record and pet customer fields', async () => {
    verticalProfile.current = {
      posFeatures: { patientRecord: true, appointment: false },
      requiredFields: { customer: ['name', 'petName'], product: [] },
      optionalFields: {
        customer: ['phone', 'email', 'petSpecies', 'petBreed', 'petBirthDate'],
        product: [],
      },
      posLayout: 'standard',
    };

    await render(<PatientRecordScreen />);

    expect(screen.getByText('Patientenakte')).toBeTruthy();
    expect(screen.getByLabelText('Tiername')).toBeTruthy();
    expect(screen.getByLabelText('Tierart')).toBeTruthy();
    expect(screen.getByLabelText('Rasse')).toBeTruthy();
    expect(screen.getByLabelText('Geburtsdatum')).toBeTruthy();
  });

  it('renders hair-salon appointment fields and saves a booking', async () => {
    enableHairSalonProfile();

    await render(<AppointmentScreen />);

    await fireEvent.press(await screen.findByLabelText('Neuer Termin'));
    const service = await screen.findByLabelText('Haarschnitt');
    await fireEvent.changeText(screen.getByLabelText('Kundenname'), 'Anna Beispiel');
    await fireEvent.changeText(screen.getByLabelText('Datum'), '2026-10-10');
    await fireEvent.changeText(screen.getByLabelText('Uhrzeit'), '10:30');
    await fireEvent.press(service);
    await fireEvent.press(screen.getByLabelText('Termin speichern'));

    await waitFor(() => {
      expect(mockCreateAppointment).toHaveBeenCalledWith(
        expect.objectContaining({
          customerName: 'Anna Beispiel',
          serviceProductId: 'service-1',
          staffId: 'staff-1',
          expectedVersion: 0,
        })
      );
    });
    expect(await screen.findByLabelText('Der Termin wurde gespeichert.')).toBeTruthy();
    expect(mockScheduleReminder).toHaveBeenCalledWith(
      'apt-1',
      '2026-10-10T08:30:00.000Z',
      expect.objectContaining({
        title: 'Termin in 15 Minuten',
      })
    );
  });

  it('renders the conflict banner on 409', async () => {
    enableHairSalonProfile();
    mockCreateAppointment.mockRejectedValue(
      new AppointmentConflictError({
        id: 'apt-existing',
        tenantId: 'tenant-a',
        staffId: 'staff-1',
        startUtc: '2026-10-10T08:30:00.000Z',
        endUtc: '2026-10-10T09:15:00.000Z',
        status: 'Booked',
        version: 1,
        createdAtUtc: '2026-10-10T07:00:00.000Z',
        updatedAtUtc: '2026-10-10T07:00:00.000Z',
      })
    );

    await render(<AppointmentScreen />);

    await fireEvent.press(await screen.findByLabelText('Neuer Termin'));
    const service = await screen.findByLabelText('Haarschnitt');
    await fireEvent.changeText(screen.getByLabelText('Kundenname'), 'Anna Beispiel');
    await fireEvent.changeText(screen.getByLabelText('Datum'), '2026-10-10');
    await fireEvent.changeText(screen.getByLabelText('Uhrzeit'), '10:30');
    await fireEvent.press(service);
    await fireEvent.press(screen.getByLabelText('Termin speichern'));

    expect(await screen.findByLabelText('Terminkonflikt')).toBeTruthy();
    expect(screen.getByText('Terminkonflikt')).toBeTruthy();
    expect(screen.getByLabelText('Neu laden')).toBeTruthy();
  });

  it('keeps only timed services when serviceDuration is on', async () => {
    enableHairSalonProfile();
    mockGetProducts.mockResolvedValue([
      { id: 'service-1', name: 'Haarschnitt', durationMinutes: 45, staffId: 'staff-1' },
      { id: 'service-2', name: 'Ohne Dauer', durationMinutes: 0 },
    ]);

    await render(<AppointmentScreen />);
    await fireEvent.press(await screen.findByLabelText('Neuer Termin'));

    expect(await screen.findByLabelText('Haarschnitt')).toBeTruthy();
    expect(screen.queryByLabelText('Ohne Dauer')).toBeNull();
  });

  it('lists products without a duration when serviceDuration is off', async () => {
    enableHairSalonProfile();
    verticalProfile.current.posFeatures.serviceDuration = false;
    mockGetProducts.mockResolvedValue([
      { id: 'service-2', name: 'Ohne Dauer', durationMinutes: null },
    ]);

    await render(<AppointmentScreen />);
    await fireEvent.press(await screen.findByLabelText('Neuer Termin'));

    expect(await screen.findByLabelText('Ohne Dauer')).toBeTruthy();
  });
});
