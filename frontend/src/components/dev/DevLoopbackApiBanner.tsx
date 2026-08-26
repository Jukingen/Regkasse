/**
 * Development-only warning when the POS API URL is loopback on native (Expo Go / device).
 */
import React from 'react';
import { Platform, StyleSheet, Text, View } from 'react-native';

import { API_BASE_URL } from '../../../config';
import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../../../constants/SoftTheme';
import {
  DEV_LOOPBACK_API_WARNING_DE,
  shouldShowDevLoopbackApiWarning,
} from '../../../utils/devApiHostWarning';

const isDev = typeof __DEV__ !== 'undefined' && __DEV__;

export function DevLoopbackApiBanner() {
  if (
    !shouldShowDevLoopbackApiWarning({
      isDev,
      platformOS: Platform.OS,
      apiBaseUrl: API_BASE_URL,
    })
  ) {
    return null;
  }

  return (
    <View style={styles.banner} accessibilityRole="alert">
      <Text style={styles.text}>{DEV_LOOPBACK_API_WARNING_DE}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  banner: {
    marginHorizontal: SoftSpacing.md,
    marginBottom: SoftSpacing.sm,
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    borderRadius: SoftRadius.sm,
    backgroundColor: '#7c2d12',
  },
  text: {
    ...SoftTypography.caption,
    color: SoftColors.textInverse,
    fontWeight: '600',
    textAlign: 'center',
  },
});
