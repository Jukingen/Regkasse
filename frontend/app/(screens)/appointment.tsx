import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { AppointmentDayCalendar } from '../../components/AppointmentDayCalendar';
import { DynamicField } from '../../components/DynamicField';
import { IfVerticalFeature } from '../../components/IfVerticalFeature';
import { MobileServiceRoutePanel } from '../../components/MobileServiceRoutePanel';
import { StaffPicker } from '../../components/StaffPicker';
import { ToastContainer } from '../../components/ToastNotification';
import { SoftColors, SoftRadius, SoftSpacing } from '../../constants/SoftTheme';
import { useVerticalFeatures } from '../../contexts/VerticalProfileContext';
import {
  AppointmentConflictError,
  combineLocalDateTime,
  createAppointment,
  listAppointments,
  type AppointmentDto,
} from '../../services/api/appointmentService';
import { getAllProducts, type Product } from '../../services/api/productService';
import { listPosStaff, type PosStaffMember } from '../../services/api/staffService';
import { formatLocalDate } from '../../services/appointmentDay';
import { scheduleAppointmentReminder } from '../../services/appointmentNotifications';

type AppointmentView = 'day' | 'form';

export default function AppointmentScreen() {
  const { t } = useTranslation('verticalProfiles');
  const { profileId, posFeatures } = useVerticalFeatures();
  const [view, setView] = useState<AppointmentView>('day');
  const [day] = useState(() => new Date());
  const [customerName, setCustomerName] = useState('');
  const [date, setDate] = useState('');
  const [time, setTime] = useState('');
  const [services, setServices] = useState<Product[]>([]);
  const [selectedService, setSelectedService] = useState<Product | null>(null);
  const [staffId, setStaffId] = useState('');
  const [staff, setStaff] = useState<PosStaffMember[]>([]);
  const [appointments, setAppointments] = useState<AppointmentDto[]>([]);
  const [saving, setSaving] = useState(false);
  const [conflict, setConflict] = useState(false);
  const [toasts, setToasts] = useState<
    { id: string; type: 'success' | 'error' | 'info' | 'warning'; message: string; duration?: number }[]
  >([]);

  const pushToast = useCallback((message: string) => {
    const id = `toast-${Date.now()}`;
    setToasts((prev) => [...prev, { id, type: 'success', message, duration: 3500 }]);
  }, []);

  const removeToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((item) => item.id !== id));
  }, []);

  const reloadDay = useCallback(async () => {
    setConflict(false);
    const from = new Date(day);
    from.setHours(0, 0, 0, 0);
    const to = new Date(day);
    to.setHours(23, 59, 59, 999);
    const [members, rows] = await Promise.all([
      listPosStaff().catch(() => [] as PosStaffMember[]),
      listAppointments({ from: from.toISOString(), to: to.toISOString() }).catch(
        () => [] as AppointmentDto[]
      ),
    ]);
    setStaff(members);
    setAppointments(rows);
  }, [day]);

  useEffect(() => {
    if (!posFeatures.appointment) return;
    let cancelled = false;
    const durationAware = posFeatures.serviceDuration === true;
    getAllProducts(1, 200)
      .then((products) => {
        if (cancelled) return;
        setServices(
          durationAware
            ? products.filter((product) => (product.durationMinutes ?? 0) > 0)
            : products
        );
      })
      .catch(() => {
        if (!cancelled) setServices([]);
      });
    reloadDay().catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [posFeatures.appointment, posFeatures.serviceDuration, reloadDay]);

  const saveBooking = async () => {
    if (!customerName.trim() || !date.trim() || !time.trim() || !selectedService) {
      Alert.alert(t('screens.appointments.validationTitle'), t('screens.appointments.required'));
      return;
    }

    setSaving(true);
    setConflict(false);
    try {
      const start = combineLocalDateTime(date, time);
      const durationMinutes =
        posFeatures.serviceDuration === true ? (selectedService.durationMinutes ?? 30) : 30;
      const end = new Date(start.getTime() + durationMinutes * 60_000);
      const created = await createAppointment({
        customerName: customerName.trim(),
        serviceProductId: selectedService.id,
        staffId:
          staffId.trim().length > 0
            ? staffId.trim()
            : (selectedService.staffId ?? null),
        startUtc: start.toISOString(),
        endUtc: end.toISOString(),
        expectedVersion: 0,
      });
      pushToast(t('screens.appointments.saved'));
      scheduleAppointmentReminder(created.id, created.startUtc ?? start.toISOString(), {
        title: t('screens.appointments.reminderTitle'),
        body: t('screens.appointments.reminderBody', {
          customer: customerName.trim(),
        }),
      }).catch(() => undefined);
      setCustomerName('');
      setDate('');
      setTime('');
      setSelectedService(null);
      setStaffId('');
      setView('day');
      await reloadDay();
    } catch (error) {
      if (error instanceof AppointmentConflictError) {
        setConflict(true);
        return;
      }
      Alert.alert(t('screens.appointments.saveFailedTitle'), t('screens.appointments.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <IfVerticalFeature
      feature="appointment"
      fallback={
        <SafeAreaView style={styles.container}>
          <Text style={styles.empty}>{t('screens.unavailable')}</Text>
        </SafeAreaView>
      }
    >
      <SafeAreaView style={styles.container}>
        <ToastContainer toasts={toasts} onRemove={removeToast} />
        <View style={styles.tabs}>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={t('screens.appointments.dayView')}
            onPress={() => setView('day')}
            style={[styles.tab, view === 'day' && styles.tabActive]}
          >
            <Text style={[styles.tabText, view === 'day' && styles.tabTextActive]}>
              {t('screens.appointments.dayView')}
            </Text>
          </Pressable>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={t('screens.appointments.newView')}
            onPress={() => setView('form')}
            style={[styles.tab, view === 'form' && styles.tabActive]}
          >
            <Text style={[styles.tabText, view === 'form' && styles.tabTextActive]}>
              {t('screens.appointments.newView')}
            </Text>
          </Pressable>
        </View>
        {view === 'day' ? (
          <View style={styles.dayPane}>
            <Text style={styles.title}>{t('screens.appointments.title')}</Text>
            <Text style={styles.subtitle}>{formatLocalDate(day)}</Text>
            <AppointmentDayCalendar
              day={day}
              staff={staff}
              appointments={appointments}
              onEmptySlotPress={(nextStaffId, nextTime) => {
                setStaffId(nextStaffId);
                setTime(nextTime);
                setDate(formatLocalDate(day));
                setView('form');
              }}
            />
          </View>
        ) : (
          <ScrollView contentContainerStyle={styles.content}>
            <Text style={styles.title}>{t('screens.appointments.title')}</Text>
            <Text style={styles.subtitle}>{t('screens.appointments.subtitle')}</Text>
            {conflict ? (
              <View
                accessibilityRole="alert"
                accessibilityLabel={t('appointments.conflict.title')}
                style={styles.conflictBanner}
              >
                <Text style={styles.conflictTitle}>{t('appointments.conflict.title')}</Text>
                <Text style={styles.conflictBody}>{t('appointments.conflict.body')}</Text>
                <Pressable
                  accessibilityRole="button"
                  accessibilityLabel={t('appointments.conflict.reload')}
                  onPress={() => {
                    reloadDay().catch(() => undefined);
                  }}
                  style={styles.reloadButton}
                >
                  <Text style={styles.reloadButtonText}>{t('appointments.conflict.reload')}</Text>
                </Pressable>
              </View>
            ) : null}
            <DynamicField
              name="name"
              entity="customer"
              value={customerName}
              onChangeText={setCustomerName}
            />
            {profileId === 'mobile-services' ? (
              <MobileServiceRoutePanel customerName={customerName} />
            ) : null}
            <View style={styles.field}>
              <Text style={styles.label}>{t('fields.appointmentDate')}</Text>
              <TextInput
                accessibilityLabel={t('fields.appointmentDate')}
                value={date}
                onChangeText={setDate}
                placeholder={t('placeholders.appointmentDate')}
                placeholderTextColor={SoftColors.textMuted}
                style={styles.input}
              />
            </View>
            <View style={styles.field}>
              <Text style={styles.label}>{t('fields.appointmentTime')}</Text>
              <TextInput
                accessibilityLabel={t('fields.appointmentTime')}
                value={time}
                onChangeText={setTime}
                placeholder={t('placeholders.appointmentTime')}
                placeholderTextColor={SoftColors.textMuted}
                style={styles.input}
              />
            </View>
            <View style={styles.field}>
              <Text style={styles.label}>{t('fields.service')}</Text>
              <View style={styles.serviceList}>
                {services.map((service) => (
                  <Pressable
                    key={service.id}
                    accessibilityRole="button"
                    accessibilityLabel={service.name}
                    onPress={() => {
                      setSelectedService(service);
                      setStaffId((current) => current || service.staffId || '');
                    }}
                    style={[
                      styles.serviceChip,
                      selectedService?.id === service.id && styles.serviceChipSelected,
                    ]}
                  >
                    <Text
                      style={[
                        styles.serviceText,
                        selectedService?.id === service.id && styles.serviceTextSelected,
                      ]}
                    >
                      {service.name}
                      {posFeatures.serviceDuration === true && service.durationMinutes
                        ? ` · ${t('screens.appointments.duration', {
                            minutes: service.durationMinutes,
                          })}`
                        : ''}
                    </Text>
                  </Pressable>
                ))}
                {services.length === 0 ? (
                  <Text style={styles.emptyInline}>{t('screens.appointments.noServices')}</Text>
                ) : null}
              </View>
            </View>
            <View style={styles.field}>
              <Text style={styles.label}>{t('fields.staff')}</Text>
              <StaffPicker
                selectedId={staffId}
                onSelect={(member) => setStaffId(member.id)}
              />
            </View>
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={t('screens.appointments.save')}
              disabled={saving}
              onPress={() => {
                saveBooking().catch(() => undefined);
              }}
              style={[styles.saveButton, saving && styles.disabled]}
            >
              <Text style={styles.saveButtonText}>
                {saving ? t('screens.appointments.saving') : t('screens.appointments.save')}
              </Text>
            </Pressable>
          </ScrollView>
        )}
      </SafeAreaView>
    </IfVerticalFeature>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
  },
  tabs: {
    flexDirection: 'row',
    gap: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.lg,
    paddingTop: SoftSpacing.md,
  },
  tab: {
    flex: 1,
    minHeight: 40,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: SoftRadius.md,
    borderWidth: 1,
    borderColor: SoftColors.border,
    backgroundColor: SoftColors.bgCard,
  },
  tabActive: {
    borderColor: SoftColors.accent,
    backgroundColor: SoftColors.accent,
  },
  tabText: {
    color: SoftColors.textPrimary,
    fontWeight: '600',
  },
  tabTextActive: {
    color: SoftColors.textInverse,
  },
  dayPane: {
    flex: 1,
    padding: SoftSpacing.lg,
  },
  content: {
    padding: SoftSpacing.lg,
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.xs,
  },
  subtitle: {
    color: SoftColors.textSecondary,
    lineHeight: 20,
    marginBottom: SoftSpacing.lg,
  },
  field: {
    gap: 6,
    marginBottom: SoftSpacing.md,
  },
  label: {
    color: SoftColors.textSecondary,
    fontSize: 13,
    fontWeight: '600',
  },
  input: {
    minHeight: 44,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.md,
    color: SoftColors.textPrimary,
    backgroundColor: SoftColors.bgCard,
  },
  serviceList: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: SoftSpacing.sm,
  },
  serviceChip: {
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    backgroundColor: SoftColors.bgCard,
  },
  serviceChipSelected: {
    borderColor: SoftColors.accent,
    backgroundColor: SoftColors.accent,
  },
  serviceText: {
    color: SoftColors.textPrimary,
  },
  serviceTextSelected: {
    color: SoftColors.textInverse,
    fontWeight: '700',
  },
  saveButton: {
    marginTop: SoftSpacing.sm,
    minHeight: 48,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.accent,
  },
  saveButtonText: {
    color: SoftColors.textInverse,
    fontWeight: '700',
    fontSize: 16,
  },
  disabled: {
    opacity: 0.6,
  },
  empty: {
    color: SoftColors.textMuted,
    textAlign: 'center',
    marginTop: SoftSpacing.xl,
  },
  emptyInline: {
    color: SoftColors.textMuted,
  },
  conflictBanner: {
    borderWidth: 1,
    borderColor: SoftColors.error,
    backgroundColor: SoftColors.errorBg,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.md,
    marginBottom: SoftSpacing.lg,
    gap: SoftSpacing.sm,
  },
  conflictTitle: {
    color: SoftColors.textPrimary,
    fontWeight: '700',
    fontSize: 16,
  },
  conflictBody: {
    color: SoftColors.textSecondary,
    lineHeight: 20,
  },
  reloadButton: {
    alignSelf: 'flex-start',
    minHeight: 40,
    justifyContent: 'center',
    paddingHorizontal: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.accent,
  },
  reloadButtonText: {
    color: SoftColors.textInverse,
    fontWeight: '700',
  },
});
