import { Ionicons } from '@expo/vector-icons';
import { LinearGradient } from 'expo-linear-gradient';
import { StatusBar } from 'expo-status-bar';
import React, { useState, useCallback, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  TextInput,
  Image,
  StyleSheet,
  Pressable,
  KeyboardAvoidingView,
  Platform,
  Keyboard,
  TouchableWithoutFeedback,
  Dimensions,
  ScrollView,
  Vibration,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useAuth } from '../../contexts/AuthContext';
import { WaveLoader } from '../../src/components/common/WaveLoader';
import { storage } from '../../utils/storage';
import { validateUsername, validatePassword } from '../../utils/validation';

import { getEnvironmentBadge } from '@/shared/config/environmentBadge';
import { getLoginFailure } from '@/utils/loginErrorHandler';

const { height } = Dimensions.get('window');

interface FormErrors {
  loginIdentifier?: string;
  password?: string;
}

const LAST_USERNAME_KEY = 'lastUsername';
const SAVED_LOGIN_IDENTIFIER_KEY = 'savedLoginIdentifier';
const LEGACY_SAVED_USERNAME_KEY = 'savedUsername';

/** Illustrative usernames showing case-insensitive login (AGENTS.md). */
const USERNAME_CASE_EXAMPLES = ['cashier1', 'MANAGER1', 'AdminUser'] as const;

export default function LoginScreen() {
  const { t } = useTranslation('auth');
  const environmentBadge = getEnvironmentBadge();
  const [loginIdentifier, setLoginIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<FormErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isBootstrapping, setIsBootstrapping] = useState(true);
  const [showPassword, setShowPassword] = useState(false);
  const [capsLockOn, setCapsLockOn] = useState(false);
  const usernameInputRef = useRef<TextInput>(null);
  const passwordInputRef = useRef<TextInput>(null);

  // Auth layout redirects when authenticated; this screen must not call protected APIs.
  const { login, isAuthenticated, isAuthReady } = useAuth();

  useEffect(() => {
    // Wait for AuthContext bootstrap only — no network calls on the login screen.
    if (!isAuthReady) return;
    setIsBootstrapping(false);
  }, [isAuthReady, isAuthenticated]);

  useEffect(() => {
    // Form must be mounted before focusing inputs (bootstrap gate unmounts the fields).
    if (isBootstrapping || !isAuthReady || isAuthenticated) return;

    let cancelled = false;
    const loadSavedLoginIdentifier = async () => {
      try {
        const saved =
          (await storage.getItem(LAST_USERNAME_KEY)) ??
          (await storage.getItem(SAVED_LOGIN_IDENTIFIER_KEY)) ??
          (await storage.getItem(LEGACY_SAVED_USERNAME_KEY));
        if (cancelled) return;

        if (saved) {
          setLoginIdentifier(saved);
          // Returning user: skip username, focus password.
          requestAnimationFrame(() => passwordInputRef.current?.focus());
        } else {
          // First visit: auto-focus username after layout settles.
          setTimeout(() => {
            if (!cancelled) usernameInputRef.current?.focus();
          }, 100);
        }

        // One-shot cleanup: remove legacy plaintext password from storage (local only)
        await storage.removeItem('savedPassword');
      } catch {
        if (!cancelled) {
          setTimeout(() => usernameInputRef.current?.focus(), 100);
        }
      }
    };

    void loadSavedLoginIdentifier();
    return () => {
      cancelled = true;
    };
  }, [isBootstrapping, isAuthReady, isAuthenticated]);

  // Validate form fields
  const validateForm = useCallback((): boolean => {
    const newErrors: FormErrors = {};

    const identifierResult = validateUsername(loginIdentifier);
    if (!identifierResult.isValid) {
      newErrors.loginIdentifier = identifierResult.message;
    }

    const passwordResult = validatePassword(password);
    if (!passwordResult.isValid) {
      newErrors.password = passwordResult.message;
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  }, [loginIdentifier, password]);

  // Handle login
  const handleLogin = useCallback(async () => {
    if (isLoading) {
      return;
    }

    if (Platform.OS === 'ios' || Platform.OS === 'android') {
      Vibration.vibrate(10);
    }

    Keyboard.dismiss();

    if (!validateForm()) {
      return;
    }

    try {
      setIsLoading(true);
      setErrors({});
      setError(null);
      await login(loginIdentifier, password);

      const trimmed = loginIdentifier.trim();
      await storage.setItem(LAST_USERNAME_KEY, trimmed);
      await storage.setItem(SAVED_LOGIN_IDENTIFIER_KEY, trimmed);
      await storage.removeItem(LEGACY_SAVED_USERNAME_KEY);
    } catch (err: unknown) {
      const { userMessage, technicalMessage, errorCode } = getLoginFailure(err);
      setError(userMessage);
      console.error('[Login Technical]', technicalMessage || err);

      if (errorCode === 'INVALID_CREDENTIALS') {
        setErrors({ loginIdentifier: userMessage });
        setPassword('');
      } else if (errorCode === 'POS_UNAUTHORIZED_USER') {
        setPassword('');
      }
    } finally {
      setIsLoading(false);
    }
  }, [isLoading, loginIdentifier, password, login, validateForm]);

  // Handle dismissing keyboard only on mobile (not web)
  const handleDismissKeyboard = useCallback(() => {
    if (Platform.OS !== 'web') {
      Keyboard.dismiss();
    }
  }, []);

  const togglePasswordVisibility = useCallback(() => {
    setShowPassword((prev) => !prev);
  }, []);

  const handlePasswordKeyPress = useCallback(
    ({ nativeEvent }: { nativeEvent: { key: string } }) => {
      // Soft keyboards rarely emit CapsLock; hardware keyboards on native do.
      // Web uses getModifierState via onKeyDown instead (absolute state).
      if (Platform.OS === 'web') return;
      if (nativeEvent.key === 'CapsLock') {
        setCapsLockOn((prev) => !prev);
      }
    },
    []
  );

  /** Web: sync absolute Caps Lock state (avoids toggle desync). */
  const handlePasswordKeyDownWeb = useCallback(
    (e: {
      getModifierState?: (key: string) => boolean;
      nativeEvent?: { getModifierState?: (key: string) => boolean };
    }) => {
      const getModifierState = e.getModifierState ?? e.nativeEvent?.getModifierState;
      if (typeof getModifierState === 'function') {
        setCapsLockOn(getModifierState('CapsLock'));
      }
    },
    []
  );

  const handlePasswordBlur = useCallback(() => {
    setCapsLockOn(false);
  }, []);

  // Authenticated users are redirected by `(auth)/_layout`; keep a short bootstrap gate only.
  if (isBootstrapping || !isAuthReady || isAuthenticated) {
    return (
      <View style={[styles.container, styles.bootstrapGate]}>
        <StatusBar style="light" />
        <WaveLoader size={28} color="#9B59B6" />
      </View>
    );
  }

  return (
    <SafeAreaView style={styles.safeRoot} edges={['top', 'bottom']}>
      <StatusBar style="light" />
      <TouchableWithoutFeedback onPress={handleDismissKeyboard} accessible={false}>
        <KeyboardAvoidingView
          behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
          style={styles.container}>
          {/* Branded gradient header */}
          <LinearGradient
            colors={['#FF8C42', '#F54EA2', '#9B59B6']}
            start={{ x: 0, y: 0 }}
            end={{ x: 1, y: 1 }}
            style={styles.gradientHeader}>
            <View
              style={styles.headerContent}
              accessibilityRole="header"
              accessibilityLabel={`${t('brandName')}. ${t('brandSubtitle')}`}>
              <Image
                source={require('../../assets/images/logo.webp')}
                style={styles.logoImage}
                resizeMode="contain"
                accessibilityIgnoresInvertColors
              />
              <Text style={styles.brandText}>{t('brandName')}</Text>
              <Text style={styles.headerSubtitle}>{t('brandSubtitle')}</Text>
              <View style={styles.headerDivider} />
              {__DEV__ && environmentBadge.text ? (
                <View
                  style={styles.devBadge}
                  accessibilityRole="text"
                  accessibilityLabel={environmentBadge.text}>
                  <Text style={styles.devBadgeText}>{environmentBadge.text}</Text>
                </View>
              ) : null}
            </View>
          </LinearGradient>

          {/* White Bottom Section — form submit is the only API call from this screen */}
          <ScrollView
            style={styles.formSection}
            contentContainerStyle={styles.formContainer}
            keyboardShouldPersistTaps="handled"
            keyboardDismissMode="on-drag"
            showsVerticalScrollIndicator={false}
            bounces={false}>
            <Text style={styles.loginTitle}>{t('loginTitle')}</Text>

            {error && (
              <View style={styles.errorContainer}>
                <Text style={styles.errorBannerText}>❌ {error}</Text>
              </View>
            )}

            {/* Email or username */}
            <View style={styles.inputContainer}>
              <TextInput
                ref={usernameInputRef}
                style={styles.input}
                placeholder={t('loginIdentifierPlaceholder')}
                placeholderTextColor="#999"
                value={loginIdentifier}
                onChangeText={(text) => {
                  setLoginIdentifier(text);
                  setError(null);
                  setErrors((prev) => ({ ...prev, loginIdentifier: undefined }));
                }}
                autoCapitalize="none"
                autoCorrect={false}
                autoComplete="username"
                textContentType="username"
                editable={!isLoading}
                returnKeyType="next"
                blurOnSubmit={false}
                onSubmitEditing={() => passwordInputRef.current?.focus()}
              />
              <View style={styles.inputLine} />
              {errors.loginIdentifier && (
                <Text style={styles.errorText}>{errors.loginIdentifier}</Text>
              )}
              <View
                style={styles.hintContainer}
                accessibilityRole="text"
                accessibilityLabel={t('loginIdentifierCaseHint')}>
                <Ionicons
                  name="information-circle"
                  size={18}
                  color="#475569"
                  style={styles.hintIcon}
                />
                <Text style={styles.hintText}>{t('loginIdentifierCaseHint')}</Text>
              </View>
              <View style={styles.exampleContainer}>
                <Text style={styles.exampleLabel}>{t('loginIdentifierExampleLabel')}</Text>
                <View style={styles.exampleValues}>
                  {USERNAME_CASE_EXAMPLES.map((example) => (
                    <View key={example} style={styles.exampleTag}>
                      <Text style={styles.exampleTagText}>{example}</Text>
                    </View>
                  ))}
                </View>
                <Text style={styles.exampleNote}>{t('loginIdentifierExampleNote')}</Text>
              </View>
            </View>

            {/* Password Input */}
            <View style={styles.inputContainer}>
              <View style={styles.passwordRow}>
                <TextInput
                  ref={passwordInputRef}
                  style={[styles.input, styles.passwordInput]}
                  placeholder={t('passwordPlaceholder')}
                  placeholderTextColor="#999"
                  value={password}
                  onChangeText={(text) => {
                    setPassword(text);
                    setError(null);
                    setErrors((prev) => ({ ...prev, password: undefined }));
                  }}
                  secureTextEntry={!showPassword}
                  autoCapitalize="none"
                  autoCorrect={false}
                  autoComplete="password"
                  textContentType="password"
                  editable={!isLoading}
                  returnKeyType="go"
                  enablesReturnKeyAutomatically
                  onSubmitEditing={() => {
                    void handleLogin();
                  }}
                  onKeyPress={handlePasswordKeyPress}
                  onBlur={handlePasswordBlur}
                  {...(Platform.OS === 'web'
                    ? {
                        // RN Web: absolute Caps Lock via DOM KeyboardEvent
                        onKeyDown: handlePasswordKeyDownWeb,
                      }
                    : {})}
                />
                <Pressable
                  style={({ pressed }) => [styles.eyeButton, pressed && styles.eyeButtonPressed]}
                  onPress={togglePasswordVisibility}
                  hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                  accessibilityRole="button"
                  accessibilityLabel={showPassword ? t('hidePassword') : t('showPassword')}
                  disabled={isLoading}>
                  <Ionicons
                    name={showPassword ? 'eye-off-outline' : 'eye-outline'}
                    size={22}
                    color="#666"
                  />
                </Pressable>
              </View>
              <View style={styles.inputLine} />
              {capsLockOn && (
                <View
                  style={styles.capsLockWarning}
                  accessibilityRole="alert"
                  accessibilityLiveRegion="polite">
                  <Text style={styles.capsLockText}>⚠️ {t('capsLockOn')}</Text>
                </View>
              )}
              {errors.password && <Text style={styles.errorText}>{errors.password}</Text>}
            </View>

            {/* Login Button */}
            <Pressable
              style={({ pressed }) => [
                styles.loginButton,
                pressed && !isLoading && styles.loginButtonPressed,
                isLoading && styles.loginButtonDisabled,
              ]}
              onPress={() => {
                void handleLogin();
              }}
              disabled={isLoading}
              accessibilityRole="button"
              accessibilityState={{ disabled: isLoading, busy: isLoading }}
              accessibilityLabel={isLoading ? t('loginButtonLoading') : t('loginButton')}>
              {({ pressed }) =>
                isLoading ? (
                  <View style={styles.loadingContainer}>
                    <WaveLoader size={18} color="#666" />
                    <Text style={[styles.loginButtonText, styles.loginButtonLoadingText]}>
                      {t('loginButtonLoading')}
                    </Text>
                  </View>
                ) : (
                  <Text style={[styles.loginButtonText, pressed && styles.loginButtonTextPressed]}>
                    {t('loginButton')}
                  </Text>
                )
              }
            </Pressable>

            {/* Footer Text */}
            <Text style={styles.footerText}>{t('signinHint')}</Text>
          </ScrollView>
        </KeyboardAvoidingView>
      </TouchableWithoutFeedback>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safeRoot: {
    flex: 1,
    backgroundColor: '#FFFFFF',
  },
  container: {
    flex: 1,
    backgroundColor: '#FFFFFF',
  },
  bootstrapGate: {
    justifyContent: 'center',
    alignItems: 'center',
  },
  gradientHeader: {
    height: height * 0.35,
    justifyContent: 'center',
    alignItems: 'center',
    borderBottomLeftRadius: 30,
    borderBottomRightRadius: 30,
  },
  headerContent: {
    alignItems: 'center',
    paddingHorizontal: 24,
  },
  logoImage: {
    width: 60,
    height: 60,
    marginBottom: 8,
    borderRadius: 14,
  },
  brandText: {
    fontSize: 30,
    fontWeight: '700',
    color: '#FFFFFF',
    letterSpacing: -0.5,
  },
  headerSubtitle: {
    fontSize: 14,
    fontWeight: '500',
    color: 'rgba(255, 255, 255, 0.9)',
    marginTop: 6,
    letterSpacing: 0.4,
  },
  headerDivider: {
    width: 56,
    height: 3,
    backgroundColor: 'rgba(255, 255, 255, 0.85)',
    borderRadius: 2,
    marginTop: 14,
  },
  devBadge: {
    backgroundColor: '#FEF3C7',
    paddingHorizontal: 10,
    paddingVertical: 3,
    borderRadius: 4,
    marginTop: 8,
  },
  devBadgeText: {
    fontSize: 11,
    color: '#92400E',
    fontWeight: '600',
  },
  formSection: {
    flex: 1,
    backgroundColor: '#FFFFFF',
  },
  formContainer: {
    flexGrow: 1,
    paddingHorizontal: 40,
    paddingTop: 40,
    paddingBottom: 32,
    alignItems: 'center',
  },
  loginTitle: {
    fontSize: 28,
    fontWeight: '600',
    color: '#333',
    letterSpacing: 2,
    marginBottom: 40,
  },
  inputContainer: {
    width: '100%',
    marginBottom: 24,
  },
  input: {
    fontSize: 14,
    color: '#333',
    paddingVertical: 12,
    letterSpacing: 1,
  },
  passwordRow: {
    width: '100%',
    flexDirection: 'row',
    alignItems: 'center',
  },
  passwordInput: {
    flex: 1,
    paddingRight: 8,
  },
  eyeButton: {
    padding: 4,
    justifyContent: 'center',
    alignItems: 'center',
  },
  eyeButtonPressed: {
    opacity: 0.6,
  },
  inputLine: {
    height: 1,
    backgroundColor: '#DDD',
  },
  capsLockWarning: {
    marginTop: 8,
    paddingVertical: 8,
    paddingHorizontal: 12,
    borderRadius: 8,
    backgroundColor: '#FFFBEB',
    borderWidth: 1,
    borderColor: '#F59E0B',
  },
  capsLockText: {
    fontSize: 12,
    color: '#B45309',
    lineHeight: 16,
  },
  errorContainer: {
    width: '100%',
    backgroundColor: '#FDF2F2',
    borderWidth: 1,
    borderColor: '#E74C3C',
    borderRadius: 8,
    paddingVertical: 12,
    paddingHorizontal: 16,
    marginTop: 12,
  },
  errorBannerText: {
    fontSize: 13,
    color: '#C0392B',
    textAlign: 'center',
    lineHeight: 18,
  },
  errorText: {
    fontSize: 12,
    color: '#E74C3C',
    marginTop: 6,
  },
  hintContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#F1F5F9',
    borderRadius: 6,
    paddingHorizontal: 12,
    paddingVertical: 8,
    marginTop: 10,
    marginBottom: 10,
  },
  hintIcon: {
    marginRight: 8,
  },
  hintText: {
    flex: 1,
    fontSize: 13,
    color: '#475569',
    lineHeight: 18,
  },
  exampleContainer: {
    backgroundColor: '#F8FAFC',
    borderRadius: 8,
    padding: 12,
    borderWidth: 1,
    borderColor: '#E2E8F0',
    borderStyle: 'dashed',
  },
  exampleLabel: {
    fontSize: 12,
    color: '#64748B',
    marginBottom: 6,
  },
  exampleValues: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  exampleTag: {
    backgroundColor: '#E2E8F0',
    borderRadius: 4,
    paddingHorizontal: 10,
    paddingVertical: 4,
  },
  exampleTagText: {
    fontSize: 12,
    color: '#1E293B',
    fontFamily: 'monospace',
  },
  exampleNote: {
    fontSize: 12,
    color: '#16A34A',
    marginTop: 8,
    lineHeight: 16,
  },
  loginButton: {
    minWidth: 180,
    height: 50,
    paddingHorizontal: 24,
    borderRadius: 25,
    borderWidth: 1.5,
    borderColor: '#333',
    justifyContent: 'center',
    alignItems: 'center',
    marginTop: 20,
    backgroundColor: 'transparent',
  },
  loginButtonPressed: {
    backgroundColor: '#333',
    opacity: 0.92,
    transform: [{ scale: 0.98 }],
  },
  loginButtonDisabled: {
    borderColor: '#CCC',
    backgroundColor: '#F5F5F5',
    opacity: 0.85,
  },
  loginButtonText: {
    fontSize: 16,
    fontWeight: '500',
    color: '#333',
    letterSpacing: 1,
  },
  loginButtonTextPressed: {
    color: '#FFFFFF',
  },
  loginButtonLoadingText: {
    color: '#666',
    letterSpacing: 0.5,
  },
  loadingContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  footerText: {
    fontSize: 13,
    color: '#999',
    marginTop: 16,
    letterSpacing: 0.5,
  },
});
