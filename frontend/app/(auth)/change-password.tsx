import React, { useCallback, useState } from 'react';
import {
  View,
  Text,
  TextInput,
  StyleSheet,
  TouchableOpacity,
  KeyboardAvoidingView,
  Platform,
  Keyboard,
  TouchableWithoutFeedback,
} from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { StatusBar } from 'expo-status-bar';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';

import { WaveLoader } from '../../src/components/common/WaveLoader';
import { useAuth } from '../../contexts/AuthContext';
import { changeMyPassword } from '../../services/api/authService';
import { validatePassword } from '../../utils/validation';

export default function ChangePasswordScreen() {
  const { t } = useTranslation('auth');
  const { refreshUserFromBackend, logout } = useAuth();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  // Web: parent TouchableWithoutFeedback + Keyboard.dismiss steals TextInput focus (login uses same guard).
  const handleDismissKeyboard = useCallback(() => {
    if (Platform.OS !== 'web') {
      Keyboard.dismiss();
    }
  }, []);

  const handleSubmit = useCallback(async () => {
    setError(null);

    const currentCheck = validatePassword(currentPassword);
    if (!currentCheck.isValid) {
      setError(t('changePassword.currentRequired'));
      return;
    }

    const newCheck = validatePassword(newPassword);
    if (!newCheck.isValid) {
      setError(newCheck.message ?? t('validation.passwordMin'));
      return;
    }

    if (newPassword !== confirmPassword) {
      setError(t('changePassword.confirmMismatch'));
      return;
    }

    setIsLoading(true);
    try {
      await changeMyPassword(currentPassword, newPassword);
      await refreshUserFromBackend();
      router.replace('/(tabs)/cash-register');
    } catch {
      setError(t('changePassword.errorFallback'));
    } finally {
      setIsLoading(false);
    }
  }, [confirmPassword, currentPassword, newPassword, refreshUserFromBackend, t]);

  return (
    <TouchableWithoutFeedback onPress={handleDismissKeyboard} accessible={false}>
      <LinearGradient colors={['#1a1a2e', '#16213e', '#0f3460']} style={styles.container}>
        <StatusBar style="light" />
        <KeyboardAvoidingView
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
          style={styles.inner}
        >
          <Text style={styles.title}>{t('changePassword.title')}</Text>
          <Text style={styles.subtitle}>{t('changePassword.subtitle')}</Text>

          <TextInput
            style={styles.input}
            value={currentPassword}
            onChangeText={setCurrentPassword}
            placeholder={t('changePassword.currentPlaceholder')}
            placeholderTextColor="#8892a6"
            secureTextEntry
            autoCapitalize="none"
            editable={!isLoading}
          />
          <TextInput
            style={styles.input}
            value={newPassword}
            onChangeText={setNewPassword}
            placeholder={t('changePassword.newPlaceholder')}
            placeholderTextColor="#8892a6"
            secureTextEntry
            autoCapitalize="none"
            editable={!isLoading}
          />
          <TextInput
            style={styles.input}
            value={confirmPassword}
            onChangeText={setConfirmPassword}
            placeholder={t('changePassword.confirmPlaceholder')}
            placeholderTextColor="#8892a6"
            secureTextEntry
            autoCapitalize="none"
            editable={!isLoading}
          />

          {error ? <Text style={styles.error}>{error}</Text> : null}

          <TouchableOpacity
            style={[styles.button, isLoading && styles.buttonDisabled]}
            onPress={() => void handleSubmit()}
            disabled={isLoading}
          >
            {isLoading ? (
              <WaveLoader size={24} color="#fff" />
            ) : (
              <Text style={styles.buttonText}>{t('changePassword.submit')}</Text>
            )}
          </TouchableOpacity>

          <TouchableOpacity style={styles.logoutLink} onPress={() => void logout()} disabled={isLoading}>
            <Text style={styles.logoutText}>{t('logout')}</Text>
          </TouchableOpacity>
        </KeyboardAvoidingView>
      </LinearGradient>
    </TouchableWithoutFeedback>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  inner: {
    flex: 1,
    justifyContent: 'center',
    paddingHorizontal: 32,
  },
  title: {
    color: '#fff',
    fontSize: 28,
    fontWeight: '700',
    marginBottom: 8,
    textAlign: 'center',
  },
  subtitle: {
    color: '#b8c1d9',
    fontSize: 15,
    marginBottom: 28,
    textAlign: 'center',
    lineHeight: 22,
  },
  input: {
    backgroundColor: 'rgba(255,255,255,0.12)',
    borderRadius: 10,
    paddingHorizontal: 16,
    paddingVertical: 14,
    color: '#fff',
    fontSize: 16,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.15)',
  },
  error: {
    color: '#ff8a80',
    marginBottom: 12,
    textAlign: 'center',
  },
  button: {
    backgroundColor: '#e94560',
    borderRadius: 10,
    paddingVertical: 16,
    alignItems: 'center',
    marginTop: 8,
  },
  buttonDisabled: { opacity: 0.7 },
  buttonText: {
    color: '#fff',
    fontSize: 17,
    fontWeight: '600',
  },
  logoutLink: {
    marginTop: 20,
    alignItems: 'center',
  },
  logoutText: {
    color: '#8892a6',
    fontSize: 15,
  },
});
