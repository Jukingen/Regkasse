import React, { useState, useCallback, useEffect, useRef } from 'react';
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
  Dimensions,
} from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { StatusBar } from 'expo-status-bar';
import { storage } from '../../utils/storage';
import { useAuth } from '../../contexts/AuthContext';
import { getLoginFailure } from '@/utils/loginErrorHandler';
import { validateUsername, validatePassword } from '../../utils/validation';
import { useTranslation } from 'react-i18next';

import { WaveLoader } from '../../src/components/common/WaveLoader';

const { width, height } = Dimensions.get('window');

interface FormErrors {
  loginIdentifier?: string;
  password?: string;
}

const LAST_USERNAME_KEY = 'lastUsername';
const SAVED_LOGIN_IDENTIFIER_KEY = 'savedLoginIdentifier';
const LEGACY_SAVED_USERNAME_KEY = 'savedUsername';

export default function LoginScreen() {
  const { t } = useTranslation('auth');
  const [loginIdentifier, setLoginIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<FormErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const passwordInputRef = useRef<TextInput>(null);

  const { login } = useAuth();

  useEffect(() => {
    loadSavedLoginIdentifier();
  }, []);

  const loadSavedLoginIdentifier = async () => {
    try {
      const saved =
        (await storage.getItem(LAST_USERNAME_KEY)) ??
        (await storage.getItem(SAVED_LOGIN_IDENTIFIER_KEY)) ??
        (await storage.getItem(LEGACY_SAVED_USERNAME_KEY));
      if (saved) {
        setLoginIdentifier(saved);
        requestAnimationFrame(() => passwordInputRef.current?.focus());
      }

      // One-shot cleanup: remove legacy plaintext password from storage
      await storage.removeItem('savedPassword');
    } catch (error) {
      // no-op
    }
  };

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
  }, [loginIdentifier, password, login, validateForm, t]);

  // Handle dismissing keyboard only on mobile (not web)
  const handleDismissKeyboard = useCallback(() => {
    if (Platform.OS !== 'web') {
      Keyboard.dismiss();
    }
  }, []);

  return (
    <>
      <StatusBar style="light" />
      <TouchableWithoutFeedback onPress={handleDismissKeyboard}>
        <View style={styles.container}>
          {/* Gradient Header */}
          <LinearGradient
            colors={['#FF8C42', '#F54EA2', '#9B59B6']}
            start={{ x: 0, y: 0 }}
            end={{ x: 1, y: 1 }}
            style={styles.gradientHeader}
          >
            <View style={styles.headerContent}>
              <Text style={styles.headerTitle}>Registrierkasse</Text>
            </View>
          </LinearGradient>

          {/* White Bottom Section */}
          <KeyboardAvoidingView
            behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
            style={styles.formSection}
          >
            <View style={styles.formContainer}>
              <Text style={styles.loginTitle}>{t('loginTitle')}</Text>

              {error && (
                <View style={styles.errorContainer}>
                  <Text style={styles.errorBannerText}>❌ {error}</Text>
                </View>
              )}

              {/* Email or username */}
              <View style={styles.inputContainer}>
                <TextInput
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
                />
                <View style={styles.inputLine} />
                {errors.loginIdentifier && (
                  <Text style={styles.errorText}>{errors.loginIdentifier}</Text>
                )}
                <Text style={styles.caseHint}>{t('loginIdentifierCaseHint')}</Text>
              </View>

              {/* Password Input */}
              <View style={styles.inputContainer}>
                <TextInput
                  ref={passwordInputRef}
                  style={styles.input}
                  placeholder={t('passwordPlaceholder')}
                  placeholderTextColor="#999"
                  value={password}
                  onChangeText={(text) => {
                    setPassword(text);
                    setError(null);
                    setErrors((prev) => ({ ...prev, password: undefined }));
                  }}
                  secureTextEntry
                  editable={!isLoading}
                  onSubmitEditing={handleLogin}
                />
                <View style={styles.inputLine} />
                {errors.password && (
                  <Text style={styles.errorText}>{errors.password}</Text>
                )}
              </View>

              {/* Login Button */}
              <TouchableOpacity
                style={[styles.loginButton, isLoading && styles.loginButtonDisabled]}
                onPress={handleLogin}
                disabled={isLoading}
                activeOpacity={0.8}
              >
                {isLoading ? (
                  <WaveLoader size={20} color="#333" />
                ) : (
                  <Text style={styles.loginButtonText}>{t('loginButton')}</Text>
                )}
              </TouchableOpacity>

              {/* Footer Text */}
              <Text style={styles.footerText}>{t('signinHint')}</Text>
            </View>
          </KeyboardAvoidingView>
        </View>
      </TouchableWithoutFeedback>
    </>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#FFFFFF',
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
  },
  headerTitle: {
    fontSize: 32,
    fontWeight: '700',
    color: '#FFFFFF',
    letterSpacing: 4,
    textAlign: 'center',
  },
  formSection: {
    flex: 1,
    backgroundColor: '#FFFFFF',
  },
  formContainer: {
    flex: 1,
    paddingHorizontal: 40,
    paddingTop: 40,
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
  inputLine: {
    height: 1,
    backgroundColor: '#DDD',
  },
  errorContainer: {
    width: '100%',
    backgroundColor: '#FDF2F2',
    borderWidth: 1,
    borderColor: '#E74C3C',
    borderRadius: 8,
    paddingVertical: 12,
    paddingHorizontal: 16,
    marginBottom: 20,
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
  caseHint: {
    fontSize: 11,
    color: '#999',
    marginTop: 8,
    lineHeight: 16,
  },
  loginButton: {
    width: 180,
    height: 50,
    borderRadius: 25,
    borderWidth: 1.5,
    borderColor: '#333',
    justifyContent: 'center',
    alignItems: 'center',
    marginTop: 20,
    backgroundColor: 'transparent',
  },
  loginButtonDisabled: {
    borderColor: '#CCC',
    opacity: 0.7,
  },
  loginButtonText: {
    fontSize: 16,
    fontWeight: '500',
    color: '#333',
    letterSpacing: 1,
  },
  footerText: {
    fontSize: 13,
    color: '#999',
    marginTop: 16,
    letterSpacing: 0.5,
  },
});