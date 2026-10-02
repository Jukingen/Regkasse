import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActivityIndicator, Alert, Pressable, StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import { useVerticalFeatures } from '../contexts/VerticalProfileContext';
import { createFolio, listRooms, type RoomDto } from '../services/api/lodgingService';

export function RoomPicker(props: {
  customerId?: string | null;
  customerName?: string;
}) {
  const { profileId } = useVerticalFeatures();
  if (profileId !== 'beherbergung') {
    return null;
  }
  return <RoomPickerFields {...props} />;
}

function RoomPickerFields({
  customerId,
  customerName,
}: {
  customerId?: string | null;
  customerName?: string;
}) {
  const { t } = useTranslation('verticalProfiles');
  const [rooms, setRooms] = useState<RoomDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [checkingIn, setCheckingIn] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRooms(await listRooms());
    } catch {
      setRooms([]);
      Alert.alert(t('screens.lodging.title'), t('screens.lodging.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const selected = rooms.find((room) => room.id === selectedId) ?? null;

  const checkIn = async () => {
    if (!customerId) {
      Alert.alert(t('screens.lodging.validationTitle'), t('screens.lodging.customerRequired'));
      return;
    }
    if (!selected || selected.occupied) {
      Alert.alert(t('screens.lodging.validationTitle'), t('screens.lodging.roomOccupied'));
      return;
    }
    setCheckingIn(true);
    try {
      await createFolio({ customerId, roomId: selected.id });
      Alert.alert(t('screens.lodging.savedTitle'), t('screens.lodging.saved'));
      await load();
    } catch {
      Alert.alert(t('screens.lodging.saveFailedTitle'), t('screens.lodging.saveFailed'));
    } finally {
      setCheckingIn(false);
    }
  };

  return (
    <View style={styles.wrap} accessibilityLabel={t('screens.lodging.title')}>
      <Text style={styles.title}>{t('screens.lodging.title')}</Text>
      <Text style={styles.subtitle}>{t('screens.lodging.subtitle')}</Text>
      {customerName ? (
        <Text style={styles.guest}>{t('screens.lodging.guest', { name: customerName })}</Text>
      ) : null}
      {loading ? <ActivityIndicator color={SoftColors.accent} /> : null}
      {!loading && rooms.length === 0 ? <Text style={styles.empty}>{t('screens.lodging.empty')}</Text> : null}
      {rooms.map((room) => {
        const label = t('screens.lodging.select', { number: room.number });
        const status = room.occupied ? t('screens.lodging.occupied') : t('screens.lodging.free');
        return (
          <Pressable
            key={room.id}
            accessibilityRole="button"
            accessibilityLabel={label}
            onPress={() => setSelectedId(room.id)}
            style={[
              styles.room,
              selectedId === room.id && styles.roomSelected,
              room.occupied && styles.roomOccupied,
            ]}
          >
            <Text style={styles.roomNumber}>
              {room.number} · {room.type}
            </Text>
            <Text style={styles.roomMeta}>
              {t('screens.lodging.capacity', { count: room.capacity })} · {status}
            </Text>
          </Pressable>
        );
      })}
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={t('screens.lodging.checkIn')}
        disabled={checkingIn || !selected || selected.occupied}
        onPress={() => {
          checkIn().catch(() => undefined);
        }}
        style={[styles.saveButton, (checkingIn || !selected || selected.occupied) && styles.disabled]}
      >
        <Text style={styles.saveButtonText}>
          {checkingIn ? t('screens.lodging.saving') : t('screens.lodging.checkIn')}
        </Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    marginBottom: SoftSpacing.lg,
    padding: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.bgPrimary,
    borderWidth: 1,
    borderColor: SoftColors.border,
  },
  title: {
    fontSize: 18,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.xs,
  },
  subtitle: {
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.md,
  },
  guest: {
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.sm,
    fontWeight: '600',
  },
  empty: {
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.sm,
  },
  room: {
    minHeight: 48,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.sm,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
  },
  roomSelected: {
    borderColor: SoftColors.accent,
  },
  roomOccupied: {
    opacity: 0.7,
  },
  roomNumber: {
    color: SoftColors.textPrimary,
    fontWeight: '700',
  },
  roomMeta: {
    color: SoftColors.textSecondary,
    marginTop: 2,
  },
  saveButton: {
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.accent,
  },
  saveButtonText: {
    color: SoftColors.textInverse,
    fontWeight: '700',
  },
  disabled: {
    opacity: 0.6,
  },
});
