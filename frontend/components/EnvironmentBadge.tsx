import React, { useState } from 'react';
import { Modal, Pressable, StyleSheet, Text } from 'react-native';

import { SoftColors, SoftSpacing } from '../constants/SoftTheme';
import { useRksvStatus } from '../hooks/useRksvStatus';
import type { DevelopmentModeSettings } from '../services/developmentModeClientCache';
import { isFiscalSimulationMode } from '../services/api/rksvEnvironmentTypes';
import {
  getReleaseStageBannerKind,
  getReleaseStageBannerLabel,
} from '../../shared/constants/environment';

type Props = {
  settings: DevelopmentModeSettings | null;
};

/**
 * POS header chip: release-stage banner (DEVELOPMENT / STAGING / CANARY) + RKSV / Simulation hints.
 * Production release stage shows no stage chip (only fiscal DEMO/PROD when relevant).
 */
export function EnvironmentBadge({ settings }: Props) {
  const [open, setOpen] = useState(false);
  const { data: rksv, isLoading } = useRksvStatus();

  if (!rksv && !settings?.enabled && !isLoading) {
    return null;
  }

  const active: string[] = [];
  if (settings?.bypassLicense) active.push('Lizenz');
  if (settings?.bypassNtpCheck) active.push('NTP');
  if (settings?.bypassTseCheck) active.push('TSE');

  const stageKind = getReleaseStageBannerKind(rksv?.releaseStage, {
    isHostDevelopment: rksv?.isHostDevelopment === true,
    isHostStaging: rksv?.isHostStaging === true,
    isCanary: rksv?.isCanary === true,
  });

  const lines: string[] = [];
  if (stageKind === 'development') lines.push('DEVELOPMENT');
  if (stageKind === 'staging') lines.push('STAGING');
  if (stageKind === 'canary') lines.push('CANARY');
  if (isFiscalSimulationMode(rksv)) lines.push('SIMULATION — nicht fiskalisch gültig');
  if (settings?.bypassLicense) lines.push('✓ Lizenzprüfung umgangen');
  if (settings?.bypassNtpCheck) lines.push('✓ NTP-Prüfung umgangen');
  if (settings?.bypassTseCheck) lines.push('✓ TSE-Prüfung umgangen');
  if (settings?.simulateOffline) lines.push('⚠ Offline-Simulation');
  if (settings?.forceOnline) lines.push('✓ Online erzwungen');
  if (settings?.validDays != null) lines.push(`Gültig: ${settings.validDays} Tage`);
  if (rksv?.tseStatusDisplay) lines.push(rksv.tseStatusDisplay);

  const parts: string[] = [];
  if (stageKind) parts.push(getReleaseStageBannerLabel(stageKind));
  if (isFiscalSimulationMode(rksv)) {
    parts.push('SIM');
  } else if (rksv?.isSimulated && !stageKind) {
    parts.push('DEMO');
  } else if (isLoading && !stageKind) {
    parts.push('…');
  }

  const environmentLabel = parts.length > 0 ? parts.join(' · ') : null;

  const devSuffix =
    settings?.enabled && active.length > 0
      ? ` · (${active.join(', ')})`
      : settings?.enabled
        ? ' · Bypass'
        : '';

  const chipStyle = [
    styles.chip,
    stageKind === 'development'
      ? styles.devBadge
      : stageKind === 'staging'
        ? styles.stagingBadge
        : stageKind === 'canary'
          ? styles.canaryBadge
          : isFiscalSimulationMode(rksv)
            ? styles.demoBadge
            : styles.prodBadge,
  ];

  const chip = (
    <Pressable
      onPress={
        settings?.enabled || rksv
          ? () => {
              setOpen(true);
            }
          : undefined
      }
      style={chipStyle}
      accessibilityRole={settings?.enabled || rksv ? 'button' : 'text'}
      accessibilityLabel={environmentLabel ? `${environmentLabel}${devSuffix}` : 'RKSV-Umgebung'}>
      <Text style={styles.chipText}>
        {environmentLabel}
        {devSuffix}
      </Text>
    </Pressable>
  );

  if (!environmentLabel) {
    return null;
  }

  return (
    <>
      {chip}
      <Modal
        visible={open}
        transparent
        animationType="fade"
        onRequestClose={() => {
          setOpen(false);
        }}>
        <Pressable
          style={styles.backdrop}
          onPress={() => {
            setOpen(false);
          }}>
          <Pressable
            style={styles.sheet}
            onPress={(e) => {
              e.stopPropagation();
            }}>
            <Text style={styles.title}>Umgebung</Text>
            {lines.length === 0 ? (
              <Text style={styles.line}>Keine zusätzlichen Hinweise</Text>
            ) : (
              lines.map((line) => (
                <Text key={line} style={styles.line}>
                  {line}
                </Text>
              ))
            )}
            <Pressable
              style={styles.closeBtn}
              onPress={() => {
                setOpen(false);
              }}>
              <Text style={styles.closeText}>Schließen</Text>
            </Pressable>
          </Pressable>
        </Pressable>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  chip: {
    marginLeft: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 4,
    borderRadius: 6,
  },
  devBadge: {
    backgroundColor: '#389e0d',
  },
  stagingBadge: {
    backgroundColor: '#d48806',
  },
  canaryBadge: {
    backgroundColor: '#fa8c16',
  },
  demoBadge: {
    backgroundColor: '#fa8c16',
  },
  prodBadge: {
    backgroundColor: '#389e0d',
  },
  chipText: {
    color: SoftColors.textInverse,
    fontSize: 11,
    fontWeight: '700',
  },
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'center',
    padding: SoftSpacing.lg,
  },
  sheet: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: 12,
    padding: SoftSpacing.lg,
  },
  title: {
    fontSize: 16,
    fontWeight: '700',
    marginBottom: SoftSpacing.sm,
    color: SoftColors.textPrimary,
  },
  line: {
    fontSize: 14,
    marginVertical: 4,
    color: SoftColors.textPrimary,
  },
  closeBtn: {
    marginTop: SoftSpacing.md,
    alignSelf: 'flex-end',
    paddingVertical: 8,
    paddingHorizontal: 12,
  },
  closeText: {
    color: SoftColors.accent,
    fontWeight: '600',
  },
});
