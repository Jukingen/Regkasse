import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import { useVerticalFeatures } from '../contexts/VerticalProfileContext';
import { chargeFolio, listFolios, type GuestFolioDto } from '../services/api/lodgingService';

export function FolioChargeBar(props: { amount: number; description?: string }) {
  const { profileId } = useVerticalFeatures();
  if (profileId !== 'beherbergung' || props.amount <= 0) {
    return null;
  }
  return <FolioChargeFields {...props} />;
}

function FolioChargeFields({ amount, description }: { amount: number; description?: string }) {
  const { t } = useTranslation('verticalProfiles');
  const [folios, setFolios] = useState<GuestFolioDto[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      const rows = await listFolios(undefined, true);
      setFolios(rows.filter((row) => row.isOpen));
    } catch {
      setFolios([]);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  if (folios.length === 0) return null;

  const post = async () => {
    if (!selectedId) {
      Alert.alert(t('screens.lodging.validationTitle'), t('screens.lodging.folioRequired'));
      return;
    }
    setBusy(true);
    try {
      await chargeFolio(selectedId, {
        description: description?.trim() || t('screens.lodging.chargeDefault'),
        amount,
      });
      Alert.alert(t('screens.lodging.chargedTitle'), t('screens.lodging.charged'));
      await load();
    } catch {
      Alert.alert(t('screens.lodging.saveFailedTitle'), t('screens.lodging.chargeFailed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <View style={styles.wrap} accessibilityLabel={t('screens.lodging.chargeTitle')}>
      <Text style={styles.title}>{t('screens.lodging.chargeToRoom')}</Text>
      {folios.map((folio) => (
        <Pressable
          key={folio.id}
          accessibilityRole="button"
          accessibilityLabel={t('screens.lodging.chargeFolio', {
            room: folio.roomNumber ?? folio.id,
          })}
          onPress={() => setSelectedId(folio.id)}
          style={[styles.row, selectedId === folio.id && styles.selected]}
        >
          <Text>
            {folio.roomNumber} · {folio.customerName} · {folio.balance.toFixed(2)}
          </Text>
        </Pressable>
      ))}
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={t('screens.lodging.chargeToRoom')}
        disabled={busy || !selectedId}
        onPress={() => {
          post().catch(() => undefined);
        }}
        style={[styles.button, (busy || !selectedId) && styles.disabled]}
      >
        <Text style={styles.buttonText}>{t('screens.lodging.chargeToRoom')}</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    marginBottom: SoftSpacing.lg,
    padding: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    borderWidth: 1,
    borderColor: SoftColors.border,
  },
  title: { fontWeight: '700', color: SoftColors.textPrimary, marginBottom: SoftSpacing.sm },
  row: {
    minHeight: 44,
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.sm,
    paddingHorizontal: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
  },
  selected: { borderColor: SoftColors.accent },
  button: {
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.accent,
  },
  buttonText: { color: SoftColors.textInverse, fontWeight: '700' },
  disabled: { opacity: 0.6 },
});
