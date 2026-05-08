import React, { useCallback, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Platform,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { useTranslation } from 'react-i18next';

import {
  AppUpdateError,
  checkForAppUpdate,
  downloadApk,
  launchApkInstaller,
  type AppUpdateCheckResult,
} from '../services/appUpdate/appUpdateService';

/**
 * Kasa-app güncelleme denetleyicisi (Settings ▸ "App-Aktualisierung" bölümü).
 *
 * Akış (yalnız Android):
 *  1) "Auf Updates prüfen" → backend `GET /api/app/version` çağrılır,
 *  2) Yeni sürüm varsa "Jetzt herunterladen" butonu görünür → APK arka planda indirilir,
 *  3) İndirme bitince sistem yükleyici diyaloğu açılır (kullanıcı "Installieren"e dokunur).
 */
type Phase = 'idle' | 'checking' | 'downloading' | 'ready_to_install' | 'error';

export const AppUpdateChecker: React.FC = () => {
  const { t } = useTranslation(['settings', 'common']);
  const [phase, setPhase] = useState<Phase>('idle');
  const [result, setResult] = useState<AppUpdateCheckResult | null>(null);
  const [downloadedFileUri, setDownloadedFileUri] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const isAndroid = Platform.OS === 'android';
  const tx = (key: string, fallback: string, opts?: Record<string, unknown>) =>
    t(`settings:appUpdate.${key}`, { defaultValue: fallback, ...(opts ?? {}) });

  const runCheck = useCallback(async () => {
    setPhase('checking');
    setErrorMessage(null);
    setDownloadedFileUri(null);
    try {
      const res = await checkForAppUpdate();
      setResult(res);
      setPhase('idle');
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : String(err));
      setResult(null);
      setPhase('error');
    }
  }, []);

  const runDownload = useCallback(async () => {
    if (!result?.remote.downloadUrl) return;
    setPhase('downloading');
    setErrorMessage(null);
    try {
      const fileName = `regkasse-${result.remote.latestVersionName || result.remote.latestVersionCode}.apk`;
      const uri = await downloadApk(result.remote.downloadUrl, fileName);
      setDownloadedFileUri(uri);
      setPhase('ready_to_install');
    } catch (err) {
      const message =
        err instanceof AppUpdateError
          ? err.message
          : err instanceof Error
            ? err.message
            : String(err);
      setErrorMessage(message);
      setPhase('error');
    }
  }, [result]);

  const runInstall = useCallback(async () => {
    if (!downloadedFileUri) return;
    try {
      await launchApkInstaller(downloadedFileUri);
    } catch (err) {
      const message =
        err instanceof AppUpdateError
          ? err.message
          : err instanceof Error
            ? err.message
            : String(err);
      Alert.alert(
        tx('installFailedTitle', 'Installation fehlgeschlagen'),
        message,
      );
    }
  }, [downloadedFileUri, tx]);

  if (!isAndroid) {
    return (
      <View>
        <Text style={styles.description}>
          {tx('androidOnly', 'In-App-Updates sind nur auf Android-Geräten verfügbar.')}
        </Text>
      </View>
    );
  }

  return (
    <View>
      <Text style={styles.description}>
        {tx(
          'description',
          'Prüft, ob eine neuere Version der Regkasse POS App verfügbar ist, lädt die APK herunter und öffnet die Installation.',
        )}
      </Text>

      {result ? (
        <View style={styles.statusBox}>
          <Text style={styles.statusLine}>
            {tx('currentVersion', 'Installierte Version')}:{' '}
            <Text style={styles.statusValue}>
              {result.currentVersionName} (Build {result.currentVersionCode})
            </Text>
          </Text>
          <Text style={styles.statusLine}>
            {tx('latestVersion', 'Aktuellste Version')}:{' '}
            <Text style={styles.statusValue}>
              {result.remote.latestVersionName || '—'} (Build {result.remote.latestVersionCode})
            </Text>
          </Text>
          {result.blocked ? (
            <Text style={styles.blocked}>
              {tx(
                'blocked',
                'Diese Version wird nicht mehr unterstützt. Bitte aktualisieren Sie die App, bevor Sie sie weiter verwenden.',
              )}
            </Text>
          ) : result.mandatory ? (
            <Text style={styles.mandatory}>
              {tx('mandatory', 'Diese Aktualisierung wird vom Anbieter dringend empfohlen.')}
            </Text>
          ) : result.hasUpdate ? (
            <Text style={styles.updateAvailable}>
              {tx('available', 'Eine neuere Version ist verfügbar.')}
            </Text>
          ) : (
            <Text style={styles.upToDate}>
              {tx('upToDate', 'Sie verwenden die neueste Version.')}
            </Text>
          )}
        </View>
      ) : null}

      {errorMessage ? (
        <Text style={styles.errorText}>
          {tx('error', 'Fehler')}: {errorMessage}
        </Text>
      ) : null}

      {phase === 'checking' || phase === 'downloading' ? (
        <View style={styles.spinnerRow}>
          <ActivityIndicator color="#007AFF" />
          <Text style={styles.spinnerText}>
            {phase === 'checking'
              ? tx('checking', 'Wird geprüft …')
              : tx('downloading', 'APK wird heruntergeladen …')}
          </Text>
        </View>
      ) : null}

      <View style={styles.actionsRow}>
        <TouchableOpacity
          style={[styles.button, phase === 'checking' && styles.buttonDisabled]}
          disabled={phase === 'checking' || phase === 'downloading'}
          onPress={runCheck}
        >
          <Text style={styles.buttonText}>{tx('check', 'Auf Updates prüfen')}</Text>
        </TouchableOpacity>

        {result?.hasUpdate && result.remote.downloadUrl && phase !== 'ready_to_install' ? (
          <TouchableOpacity
            style={[
              styles.button,
              styles.buttonPrimary,
              phase === 'downloading' && styles.buttonDisabled,
            ]}
            disabled={phase === 'downloading'}
            onPress={runDownload}
          >
            <Text style={[styles.buttonText, styles.buttonTextPrimary]}>
              {tx('download', 'Jetzt herunterladen')}
            </Text>
          </TouchableOpacity>
        ) : null}

        {phase === 'ready_to_install' && downloadedFileUri ? (
          <TouchableOpacity
            style={[styles.button, styles.buttonPrimary]}
            onPress={runInstall}
          >
            <Text style={[styles.buttonText, styles.buttonTextPrimary]}>
              {tx('install', 'Installation starten')}
            </Text>
          </TouchableOpacity>
        ) : null}
      </View>

      <Text style={styles.note}>
        {tx(
          'unknownSourcesNote',
          'Hinweis: Auf dem Tablet muss „Installation aus unbekannten Quellen" für die Datei-/Share-App erlaubt sein.',
        )}
      </Text>
    </View>
  );
};

const styles = StyleSheet.create({
  description: {
    fontSize: 14,
    color: '#666',
    lineHeight: 20,
  },
  statusBox: {
    marginTop: 12,
    padding: 12,
    backgroundColor: '#f5f7fa',
    borderRadius: 8,
  },
  statusLine: {
    fontSize: 13,
    color: '#444',
    marginTop: 2,
  },
  statusValue: {
    fontWeight: '600',
    color: '#222',
  },
  upToDate: {
    fontSize: 13,
    color: '#2e7d32',
    marginTop: 8,
    fontWeight: '600',
  },
  updateAvailable: {
    fontSize: 13,
    color: '#1565c0',
    marginTop: 8,
    fontWeight: '600',
  },
  mandatory: {
    fontSize: 13,
    color: '#ef6c00',
    marginTop: 8,
    fontWeight: '600',
  },
  blocked: {
    fontSize: 13,
    color: '#c62828',
    marginTop: 8,
    fontWeight: '700',
  },
  errorText: {
    fontSize: 13,
    color: '#c62828',
    marginTop: 8,
  },
  spinnerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 12,
  },
  spinnerText: {
    marginLeft: 8,
    color: '#444',
    fontSize: 13,
  },
  actionsRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
    marginTop: 12,
  },
  button: {
    backgroundColor: '#e0e0e0',
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 8,
    alignItems: 'center',
    flexGrow: 1,
    minWidth: 160,
  },
  buttonPrimary: {
    backgroundColor: '#007AFF',
  },
  buttonDisabled: {
    opacity: 0.5,
  },
  buttonText: {
    color: '#333',
    fontSize: 15,
    fontWeight: '600',
  },
  buttonTextPrimary: {
    color: '#fff',
  },
  note: {
    fontSize: 12,
    color: '#888',
    marginTop: 12,
    lineHeight: 16,
  },
});
