/**
 * First-login deck for the universal POS app. Slides follow the tenant profile.
 * Skipped once the device flag in SecureStore is set.
 */
import { useRouter } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useVerticalProfileContext } from '../../contexts/VerticalProfileContext';
import { WaveLoader } from '../../src/components/common/WaveLoader';
import {
  buildOnboardingSlides,
  hasSeenPosOnboarding,
  markPosOnboardingSeen,
  shouldSkipPosOnboarding,
} from '../../services/verticalProfiles/posOnboarding';

const ONBOARDING_TABLE_NUMBERS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

export default function OnboardingScreen() {
  const router = useRouter();
  const { t } = useTranslation('verticalProfiles');
  const { t: tAuth } = useTranslation('auth');
  const { t: tCommon } = useTranslation('common');
  const { profileId, isLoading, source, error, refresh } = useVerticalProfileContext();
  const [settled, setSettled] = useState(source === 'cache' || source === 'network' || error != null);
  const [index, setIndex] = useState(0);
  const slides = useMemo(() => buildOnboardingSlides(profileId), [profileId]);
  const skipDeck = shouldSkipPosOnboarding(profileId);
  const slide = slides[Math.min(index, Math.max(slides.length - 1, 0))];
  const isLast = slides.length === 0 || index >= slides.length - 1;

  useEffect(() => {
    if (isLoading) return;
    if (source === 'cache' || source === 'network' || error != null) {
      setSettled(true);
    }
  }, [error, isLoading, source]);

  useEffect(() => {
    if (!settled || isLoading) return;
    if (skipDeck) {
      router.replace('/(tabs)/cash-register');
      return;
    }
    let cancelled = false;
    void hasSeenPosOnboarding().then((seen) => {
      if (!cancelled && seen) {
        router.replace('/(tabs)/cash-register');
      }
    });
    return () => {
      cancelled = true;
    };
  }, [isLoading, router, settled, skipDeck]);

  const finish = useCallback(() => {
    // Leave even if SecureStore is slow. The flag write still runs.
    const marked = markPosOnboardingSeen().catch(() => undefined);
    const giveUpWaiting = new Promise<void>((resolve) => {
      setTimeout(resolve, 800);
    });
    void Promise.race([marked, giveUpWaiting]).finally(() => {
      router.replace('/(tabs)/cash-register');
    });
  }, [router]);

  const goNext = useCallback(() => {
    if (index >= slides.length - 1) {
      finish();
      return;
    }
    setIndex(index + 1);
  }, [finish, index, slides.length]);

  if (!settled || isLoading || skipDeck || !slide) {
    return (
      <View style={styles.loading}>
        <StatusBar style="dark" />
        <WaveLoader size={32} color="#007AFF" />
      </View>
    );
  }

  if (error && source === 'default') {
    return (
      <SafeAreaView style={styles.safe} edges={['top', 'bottom']}>
        <StatusBar style="dark" />
        <View style={styles.body}>
          <Text style={styles.title}>{tAuth('onboarding.loadFailed')}</Text>
        </View>
        <View style={styles.actions}>
          <Pressable
            accessibilityRole="button"
            onPress={() => {
              void refresh();
            }}
            style={styles.primary}>
            <Text style={styles.primaryText}>{tCommon('retry')}</Text>
          </Pressable>
        </View>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top', 'bottom']}>
      <StatusBar style="dark" />
      <View style={styles.body}>
        <Text style={styles.step}>
          {tAuth('onboarding.step', { current: index + 1, total: slides.length })}
        </Text>
        <Text style={styles.title}>{t(slide.titleKey)}</Text>
        <Text style={styles.copy}>{t(slide.bodyKey)}</Text>
        {slide.id === 'tables' ? (
          <View
            style={styles.tableGrid}
            accessibilityRole="image"
            accessibilityLabel={t(slide.previewKey)}>
            {ONBOARDING_TABLE_NUMBERS.map((tableNumber) => (
              <View key={tableNumber} style={styles.tableChip}>
                <Text style={styles.tableChipText}>{tableNumber}</Text>
              </View>
            ))}
          </View>
        ) : (
          <View
            style={styles.preview}
            accessibilityRole="image"
            accessibilityLabel={t(slide.previewKey)}>
            <View style={styles.previewBar} />
            <Text style={styles.previewLabel}>{t(slide.previewKey)}</Text>
            <View style={styles.previewRow} />
            <View style={styles.previewRowShort} />
          </View>
        )}
      </View>
      <View style={styles.actions}>
        <Pressable accessibilityRole="button" onPress={finish} style={styles.secondary}>
          <Text style={styles.secondaryText}>{tAuth('onboarding.skip')}</Text>
        </Pressable>
        <Pressable accessibilityRole="button" onPress={goNext} style={styles.primary}>
          <Text style={styles.primaryText}>
            {isLast ? tAuth('onboarding.done') : tAuth('onboarding.next')}
          </Text>
        </Pressable>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: {
    flex: 1,
    backgroundColor: '#F7F8FA',
  },
  loading: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#F7F8FA',
  },
  body: {
    flex: 1,
    paddingHorizontal: 24,
    paddingTop: 28,
  },
  step: {
    fontSize: 13,
    fontWeight: '600',
    color: '#007AFF',
    marginBottom: 12,
  },
  title: {
    fontSize: 28,
    fontWeight: '700',
    color: '#1C1C1E',
    marginBottom: 12,
  },
  copy: {
    fontSize: 16,
    lineHeight: 22,
    color: '#3A3A3C',
    marginBottom: 24,
  },
  preview: {
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    padding: 16,
    borderWidth: 1,
    borderColor: '#E5E5EA',
  },
  previewBar: {
    height: 8,
    width: 72,
    borderRadius: 4,
    backgroundColor: '#007AFF',
    marginBottom: 16,
  },
  previewLabel: {
    fontSize: 18,
    fontWeight: '700',
    color: '#1C1C1E',
    marginBottom: 16,
  },
  previewRow: {
    height: 12,
    borderRadius: 6,
    backgroundColor: '#E5E5EA',
    marginBottom: 10,
  },
  previewRowShort: {
    height: 12,
    width: '62%',
    borderRadius: 6,
    backgroundColor: '#F2F2F7',
  },
  tableGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  tableChip: {
    width: 56,
    height: 56,
    borderRadius: 12,
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: '#E5E5EA',
    alignItems: 'center',
    justifyContent: 'center',
  },
  tableChipText: {
    fontSize: 18,
    fontWeight: '700',
    color: '#1C1C1E',
  },
  actions: {
    flexDirection: 'row',
    gap: 12,
    paddingHorizontal: 24,
    paddingBottom: 16,
  },
  primary: {
    flex: 1,
    backgroundColor: '#007AFF',
    borderRadius: 12,
    minHeight: 48,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 16,
  },
  primaryText: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '700',
  },
  secondary: {
    flex: 1,
    borderRadius: 12,
    minHeight: 48,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 16,
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: '#D1D1D6',
  },
  secondaryText: {
    color: '#1C1C1E',
    fontSize: 16,
    fontWeight: '600',
  },
});
