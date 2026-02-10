import React, { useState, useCallback, useEffect } from 'react';
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
  ActivityIndicator,
  Dimensions,
} from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { StatusBar } from 'expo-status-bar';
import { storage } from '../../utils/storage';
import { useAuth } from '../../contexts/AuthContext';

const { width, height } = Dimensions.get('window');

interface FormErrors {
  username?: string;
  password?: string;
}

export default function LoginScreen() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<FormErrors>({});
  const [isLoading, setIsLoading] = useState(false);

  const { login } = useAuth();

  // Load saved credentials on mount
  useEffect(() => {
    loadSavedCredentials();
  }, []);

  const loadSavedCredentials = async () => {
    try {
      const savedUsername = await storage.getItem('savedUsername');
      const savedPassword = await storage.getItem('savedPassword');

      if (savedUsername) setUsername(savedUsername);
      if (savedPassword) setPassword(savedPassword);

      console.log('✅ Loaded saved credentials from AsyncStorage');
    } catch (error) {
      console.log('ℹ️ No saved credentials found');
    }
  };

  // Validate form fields
  const validateForm = useCallback((): boolean => {
    const newErrors: FormErrors = {};

    if (!username.trim()) {
      newErrors.username = 'Username is required';
    }

    if (!password) {
      newErrors.password = 'Password is required';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  }, [username, password]);

  // Handle login
  const handleLogin = useCallback(async () => {
    Keyboard.dismiss();

    if (!validateForm()) {
      return;
    }

    try {
      setIsLoading(true);
      setErrors({});
      await login(username, password);

      // Save credentials after successful login
      await storage.setItem('savedUsername', username);
      await storage.setItem('savedPassword', password);
      console.log('✅ Saved credentials to AsyncStorage');
    } catch (error) {
      console.error('Login failed:', error);
      setErrors({
        username: 'Invalid username or password',
      });
    } finally {
      setIsLoading(false);
    }
  }, [username, password, login, validateForm]);

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
              <Text style={styles.loginTitle}>LOGIN</Text>

              {/* Username Input */}
              <View style={styles.inputContainer}>
                <TextInput
                  style={styles.input}
                  placeholder="USERNAME"
                  placeholderTextColor="#999"
                  value={username}
                  onChangeText={setUsername}
                  autoCapitalize="none"
                  autoCorrect={false}
                  editable={!isLoading}
                  onFocus={() => console.log('📍 Username input focused')}
                  onBlur={() => console.log('📍 Username input blurred')}
                />
                <View style={styles.inputLine} />
                {errors.username && (
                  <Text style={styles.errorText}>{errors.username}</Text>
                )}
              </View>

              {/* Password Input */}
              <View style={styles.inputContainer}>
                <TextInput
                  style={styles.input}
                  placeholder="PASSWORD"
                  placeholderTextColor="#999"
                  value={password}
                  onChangeText={setPassword}
                  secureTextEntry
                  editable={!isLoading}
                  onSubmitEditing={handleLogin}
                  onFocus={() => console.log('📍 Password input focused')}
                  onBlur={() => console.log('📍 Password input blurred')}
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
                  <ActivityIndicator color="#333" />
                ) : (
                  <Text style={styles.loginButtonText}>Log In</Text>
                )}
              </TouchableOpacity>

              {/* Footer Text */}
              <Text style={styles.footerText}>Sign in to continue.</Text>
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
  errorText: {
    fontSize: 12,
    color: '#E74C3C',
    marginTop: 6,
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