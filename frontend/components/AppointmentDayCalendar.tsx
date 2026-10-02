import React, { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import {
  appointmentOccupiesSlot,
  buildAppointmentDaySlots,
} from '../services/appointmentDay';
import { staffInitials } from '../utils/staffInitials';

export interface AppointmentDayStaff {
  id: string;
  name: string;
  role: string;
}

export interface AppointmentDayItem {
  staffId?: string | null;
  startUtc: string;
  endUtc: string;
  status: string;
}

export interface AppointmentDayCalendarProps {
  day: Date;
  staff: AppointmentDayStaff[];
  appointments: AppointmentDayItem[];
  onEmptySlotPress: (staffId: string, time: string) => void;
}

export function AppointmentDayCalendar({
  day,
  staff,
  appointments,
  onEmptySlotPress,
}: AppointmentDayCalendarProps) {
  const { t } = useTranslation('verticalProfiles');
  const slots = useMemo(() => buildAppointmentDaySlots(), []);

  if (staff.length === 0) {
    return <Text style={styles.empty}>{t('screens.appointments.noStaff')}</Text>;
  }

  return (
    <ScrollView horizontal accessibilityLabel={t('screens.appointments.dayView')}>
      <View style={styles.grid}>
        <View style={styles.timeColumn}>
          <View style={styles.headerCell} />
          {slots.map((slot) => (
            <View key={slot} style={styles.slotCell}>
              <Text style={styles.timeLabel}>{slot}</Text>
            </View>
          ))}
        </View>
        {staff.map((member) => {
          const columnAppointments = appointments.filter((row) => row.staffId === member.id);
          return (
            <View key={member.id} style={styles.staffColumn}>
              <View style={styles.headerCell}>
                <View style={styles.avatar}>
                  <Text style={styles.initials}>{staffInitials(member.name)}</Text>
                </View>
                <Text style={styles.headerName} numberOfLines={1}>
                  {member.name}
                </Text>
              </View>
              {slots.map((slot) => {
                const occupying = columnAppointments.find((row) =>
                  appointmentOccupiesSlot(row.startUtc, row.endUtc, row.status, day, slot)
                );
                if (occupying) {
                  return (
                    <View
                      key={slot}
                      accessibilityLabel={`${member.name} ${slot} ${occupying.status}`}
                      style={[styles.slotCell, styles.occupied]}
                    >
                      <Text style={styles.occupiedText} numberOfLines={2}>
                        {occupying.status}
                      </Text>
                    </View>
                  );
                }
                return (
                  <Pressable
                    key={slot}
                    accessibilityRole="button"
                    accessibilityLabel={t('screens.appointments.emptySlot', {
                      staff: member.name,
                      time: slot,
                    })}
                    onPress={() => onEmptySlotPress(member.id, slot)}
                    style={styles.slotCell}
                  >
                    <Text style={styles.emptySlotText}>+</Text>
                  </Pressable>
                );
              })}
            </View>
          );
        })}
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  empty: {
    color: SoftColors.textMuted,
    marginBottom: SoftSpacing.md,
  },
  grid: {
    flexDirection: 'row',
    paddingBottom: SoftSpacing.md,
  },
  timeColumn: {
    width: 52,
  },
  staffColumn: {
    width: 120,
    marginRight: SoftSpacing.sm,
  },
  headerCell: {
    height: 64,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 4,
    marginBottom: SoftSpacing.xs,
  },
  avatar: {
    width: 32,
    height: 32,
    borderRadius: 16,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: SoftColors.accentLight,
  },
  initials: {
    fontSize: 11,
    fontWeight: '700',
    color: SoftColors.textPrimary,
  },
  headerName: {
    fontSize: 12,
    fontWeight: '600',
    color: SoftColors.textPrimary,
    textAlign: 'center',
  },
  slotCell: {
    minHeight: 44,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.sm,
    marginBottom: 4,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: SoftColors.bgCard,
    paddingHorizontal: 4,
  },
  occupied: {
    backgroundColor: SoftColors.bgSecondary,
  },
  occupiedText: {
    fontSize: 11,
    color: SoftColors.textPrimary,
    fontWeight: '600',
  },
  emptySlotText: {
    color: SoftColors.textMuted,
    fontSize: 16,
  },
  timeLabel: {
    fontSize: 11,
    color: SoftColors.textSecondary,
  },
});
