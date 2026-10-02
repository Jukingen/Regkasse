import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import {
  parseTaxiAmount,
  parseTaxiKm,
  useTaxiTrip,
} from '../contexts/TaxiTripContext';
import { useVerticalProfileContext } from '../contexts/VerticalProfileContext';
import { useProductDisplayLocale } from '../hooks/useProductDisplayLocale';
import type { Product } from '../services/api/productService';
import { computeTaxiSuggestedAmount } from '../services/taxiFare';
import { formatPrice } from '../utils/formatPrice';
import { resolveProductDisplayName } from '../utils/productLocalization';

export interface TaxiSalePanelProps {
  products: Product[];
  selectedProductId: string | null;
  onSelectProduct: (product: Product) => void;
  onPayment: () => void;
  canPay: boolean;
}

function formatElapsed(startedAtUtc: string | null, nowMs: number): string {
  if (!startedAtUtc) return '00:00';
  const started = Date.parse(startedAtUtc);
  if (!Number.isFinite(started)) return '00:00';
  const totalSeconds = Math.max(0, Math.floor((nowMs - started) / 1000));
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

export function TaxiSalePanel({
  products,
  selectedProductId,
  onSelectProduct,
  onPayment,
  canPay,
}: TaxiSalePanelProps) {
  const { t } = useTranslation('verticalProfiles');
  const { taxiTariffPerKm } = useVerticalProfileContext();
  const productDisplayLocale = useProductDisplayLocale();
  const trip = useTaxiTrip();
  const [nowMs, setNowMs] = useState(() => Date.now());

  useEffect(() => {
    if (!trip.running) return;
    const timer = setInterval(() => setNowMs(Date.now()), 1000);
    return () => clearInterval(timer);
  }, [trip.running]);

  const km = parseTaxiKm(trip.routeKm);
  const manual = parseTaxiAmount(trip.manualAmount);
  const suggested = computeTaxiSuggestedAmount(km, taxiTariffPerKm, manual);

  const idle = !trip.running && !trip.startedAtUtc;
  const ended = !trip.running && Boolean(trip.startedAtUtc);

  const productChips = useMemo(
    () => products.filter((product) => product.isActive !== false).slice(0, 12),
    [products]
  );

  return (
    <ScrollView contentContainerStyle={styles.content}>
      <Text style={styles.title}>{t('screens.taxi.title')}</Text>
      <Text style={styles.subtitle}>{t('screens.taxi.subtitle')}</Text>
      {taxiTariffPerKm != null && taxiTariffPerKm > 0 ? (
        <Text style={styles.tariff}>
          {t('screens.taxi.tariff', { amount: formatPrice(taxiTariffPerKm) })}
        </Text>
      ) : null}

      {idle ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('screens.taxi.startTrip')}
          onPress={trip.startTrip}
          style={styles.startButton}
        >
          <Text style={styles.startButtonText}>{t('screens.taxi.startTrip')}</Text>
        </Pressable>
      ) : null}

      {trip.running ? (
        <View style={styles.runningCard}>
          <Text style={styles.runningLabel}>{t('screens.taxi.running')}</Text>
          <Text style={styles.timer}>{formatElapsed(trip.startedAtUtc, nowMs)}</Text>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={t('screens.taxi.endTrip')}
            onPress={trip.endTrip}
            style={styles.endButton}
          >
            <Text style={styles.endButtonText}>{t('screens.taxi.endTrip')}</Text>
          </Pressable>
        </View>
      ) : null}

      {ended ? (
        <View style={styles.form}>
          <Text style={styles.label}>{t('screens.taxi.routeFrom')}</Text>
          <TextInput
            accessibilityLabel={t('screens.taxi.routeFrom')}
            value={trip.routeFrom}
            onChangeText={trip.setRouteFrom}
            placeholder={t('screens.taxi.routeFromPlaceholder')}
            placeholderTextColor={SoftColors.textMuted}
            style={styles.input}
          />
          <Text style={styles.label}>{t('screens.taxi.routeTo')}</Text>
          <TextInput
            accessibilityLabel={t('screens.taxi.routeTo')}
            value={trip.routeTo}
            onChangeText={trip.setRouteTo}
            placeholder={t('screens.taxi.routeToPlaceholder')}
            placeholderTextColor={SoftColors.textMuted}
            style={styles.input}
          />
          <Text style={styles.label}>{t('screens.taxi.routeKm')}</Text>
          <TextInput
            accessibilityLabel={t('screens.taxi.routeKm')}
            value={trip.routeKm}
            onChangeText={trip.setRouteKm}
            keyboardType="decimal-pad"
            placeholder={t('screens.taxi.routeKmPlaceholder')}
            placeholderTextColor={SoftColors.textMuted}
            style={styles.input}
          />
          <Text style={styles.label}>{t('screens.taxi.fare')}</Text>
          <TextInput
            accessibilityLabel={t('screens.taxi.fare')}
            value={trip.manualAmount}
            onChangeText={trip.setManualAmount}
            keyboardType="decimal-pad"
            placeholder={t('screens.taxi.farePlaceholder')}
            placeholderTextColor={SoftColors.textMuted}
            style={styles.input}
          />
          <Text style={styles.hint}>{t('screens.taxi.fareHint')}</Text>
          {suggested != null ? (
            <Text style={styles.suggested}>
              {t('screens.taxi.suggestedFare', { amount: formatPrice(suggested) })}
            </Text>
          ) : null}

          <Text style={styles.label}>{t('screens.taxi.selectService')}</Text>
          <View style={styles.chipRow}>
            {productChips.map((product) => {
              const selected = product.id === selectedProductId;
              return (
                <Pressable
                  key={product.id}
                  accessibilityRole="button"
                  accessibilityLabel={resolveProductDisplayName(product, productDisplayLocale)}
                  onPress={() => onSelectProduct(product)}
                  style={[styles.chip, selected && styles.chipSelected]}
                >
                  <Text style={[styles.chipText, selected && styles.chipTextSelected]}>
                    {resolveProductDisplayName(product, productDisplayLocale)}
                    {product.price != null ? ` · ${formatPrice(product.price)}` : ''}
                  </Text>
                </Pressable>
              );
            })}
          </View>

          <Pressable
            accessibilityRole="button"
            accessibilityLabel={t('screens.taxi.pay')}
            disabled={!canPay}
            onPress={onPayment}
            style={[styles.payButton, !canPay && styles.disabled]}
          >
            <Text style={styles.payButtonText}>{t('screens.taxi.pay')}</Text>
          </Pressable>
        </View>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  content: {
    padding: SoftSpacing.lg,
    paddingBottom: 120,
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.xs,
  },
  subtitle: {
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.md,
    lineHeight: 20,
  },
  tariff: {
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.lg,
  },
  startButton: {
    minHeight: 88,
    borderRadius: SoftRadius.lg,
    backgroundColor: SoftColors.accent,
    alignItems: 'center',
    justifyContent: 'center',
  },
  startButtonText: {
    color: SoftColors.textInverse,
    fontSize: 22,
    fontWeight: '800',
  },
  runningCard: {
    borderRadius: SoftRadius.lg,
    borderWidth: 1,
    borderColor: SoftColors.border,
    backgroundColor: SoftColors.bgCard,
    padding: SoftSpacing.lg,
    alignItems: 'center',
    gap: SoftSpacing.md,
  },
  runningLabel: {
    color: SoftColors.textSecondary,
    fontWeight: '600',
  },
  timer: {
    fontSize: 40,
    fontWeight: '800',
    color: SoftColors.textPrimary,
  },
  endButton: {
    minHeight: 48,
    alignSelf: 'stretch',
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.accent,
    alignItems: 'center',
    justifyContent: 'center',
  },
  endButtonText: {
    color: SoftColors.textInverse,
    fontWeight: '700',
    fontSize: 16,
  },
  form: {
    gap: 6,
  },
  label: {
    marginTop: SoftSpacing.sm,
    color: SoftColors.textSecondary,
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
  hint: {
    color: SoftColors.textMuted,
    marginBottom: SoftSpacing.sm,
  },
  suggested: {
    color: SoftColors.textPrimary,
    fontWeight: '700',
    marginBottom: SoftSpacing.md,
  },
  chipRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: SoftSpacing.sm,
    marginBottom: SoftSpacing.md,
  },
  chip: {
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    backgroundColor: SoftColors.bgCard,
  },
  chipSelected: {
    borderColor: SoftColors.accent,
    backgroundColor: SoftColors.accent,
  },
  chipText: {
    color: SoftColors.textPrimary,
  },
  chipTextSelected: {
    color: SoftColors.textInverse,
    fontWeight: '700',
  },
  payButton: {
    minHeight: 48,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.accent,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: SoftSpacing.md,
  },
  payButtonText: {
    color: SoftColors.textInverse,
    fontWeight: '700',
    fontSize: 16,
  },
  disabled: {
    opacity: 0.6,
  },
});
