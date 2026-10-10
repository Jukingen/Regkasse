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
} from '../../services/verticalProfiles/posOnboarding';

export default function OnboardingScreen() {
  const router = useRouter();
  const { t } = useTranslation('verticalProfiles');
  const { t: tAuth } = useTranslation('auth');
  const { profileId, isLoading, source, error } = useVerticalProfileContext();
  const [settled, setSettled] = useState(source === 'cache' || source === 'network' || error != null);
  const [index, setIndex] = useState(0);
  const slides = useMemo(() => buildOnboardingSlides(profileId), [profileId]);
  const slide = slides[Math.min(index, slides.length - 1)];
  const isLast = index >= slides.length - 1;

  useEffect(() => {
    if (isLoading) return;
    if (source === 'cache' || source === 'network' || error != null) {
      setSettled(true);
    }
  }, [error, isLoading, source]);

  useEffect(() => {
    if (!settled) return;
    let cancelled = false;
    void hasSeenPosOnboarding().then((seen) => {
      if (!cancelled && seen) {
        router.replace('/(tabs)/cash-register');
      }
    });
    return () => {
      cancelled = true;
    };
  }, [router, settled]);

  const finish = useCallback(() => {
    void markPosOnboardingSeen().finally(() => {
      router.replace('/(tabs)/cash-register');
    });
  }, [router]);

  if (!settled || isLoading) {
    return (
      <View style={styles.loading}>
        <StatusBar style="dark" />
        <WaveLoader size={32} color="#007AFF" />
      </View>
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
        <View
          style={styles.preview}
          accessibilityRole="image"
          accessibilityLabel={t(slide.previewKey)}>
          <View style={styles.previewBar} />
          <Text style={styles.previewLabel}>{t(slide.previewKey)}</Text>
          <View style={styles.previewRow} />
          <View style={styles.previewRowShort} />
        </View>
      </View>
      <View style={styles.actions}>
        {isLast ? (
          <Pressable
            accessibilityRole="button"
            onPress={finish}
            style={styles.primary}>
            <Text style={styles.primaryText}>{tAuth('onboarding.done')}</Text>
          </Pressable>
        ) : (
          <>
            <Pressable accessibilityRole="button" onPress={finish} style={styles.secondary}>
              <Text style={styles.secondaryText}>{tAuth('onboarding.skip')}</Text>
            </Pressable>
            <Pressable
              accessibilityRole="button"
              onPress={() => setIndex((current) => Math.min(current + 1, slides.length - 1))}
              style={styles.primary}>
              <Text style={styles.primaryText}>{tAuth('onboarding.next')}</Text>
            </Pressable>
          </>
        )}
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
