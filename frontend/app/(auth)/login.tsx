import React, { useState, useCallback } from 'react';
import { 
  View, 
  Text, 
  StyleSheet, 
  Alert, 
  KeyboardAvoidingView, 
  Platform,
  ScrollView,
  TouchableWithoutFeedback,
  Keyboard,
  Dimensions
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../contexts/AuthContext';
import { StatusBar } from 'expo-status-bar';
import { FormInput } from '../../components/FormInput';
import { Button } from '../../components/Button';
import { SecurityIndicator } from '../../components/SecurityIndicator';
import { PasswordStrength } from '../../components/PasswordStrength';
import { ErrorMessage } from '../../components/ErrorMessage';
import { validateField } from '../../utils/validation';

const { width, height } = Dimensions.get('window');

export default function LoginScreen() {
  console.log('LoginScreen loaded');
  
  // State management
  const [formData, setFormData] = useState<Record<string, string>>({
    username: '',
    password: ''
  });
  
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [touched, setTouched] = useState<Record<string, boolean>>({});
  const [isLoading, setIsLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [loginAttempts, setLoginAttempts] = useState(0);
  const [isLocked, setIsLocked] = useState(false);
  const [generalError, setGeneralError] = useState<string>('');
  
  const { login } = useAuth();
  const { t } = useTranslation();

  // Form değerlerini güncelle
  const handleInputChange = useCallback((field: string, value: string) => {
    setFormData(prev => ({ ...prev, [field]: value }));
    
    // Real-time validasyon
    if (touched[field]) {
      const error = validateField(field, value);
      setErrors(prev => ({
        ...prev,
        [field]: error || ''
      }));
    }
  }, [touched]);

  // Input focus/blur işlemleri
  const handleInputBlur = useCallback((field: string) => {
    setTouched(prev => ({ ...prev, [field]: true }));
    
    // Validasyon çalıştır
    const error = validateField(field, formData[field]);
    setErrors(prev => ({
      ...prev,
      [field]: error || ''
    }));
  }, [formData]);

  // Şifre görünürlüğünü değiştir
  const togglePasswordVisibility = useCallback(() => {
    setShowPassword(prev => !prev);
  }, []);



  // Giriş işlemi
  const handleLogin = useCallback(async () => {
    // Hesap kilitli mi kontrol et
    if (isLocked) {
      Alert.alert('Hesap Kilitli', 'Çok fazla başarısız giriş denemesi. Lütfen 15 dakika bekleyin.');
      return;
    }
    
    // Tüm alanları touched olarak işaretle
    const allTouched = { username: true, password: true };
    setTouched(allTouched);
    
    // Form validasyonu
    const validationErrors: Record<string, string> = {};
    
    if (!formData.username.trim()) {
      validationErrors.username = 'Kullanıcı adı gereklidir';
    }
    
    if (!formData.password) {
      validationErrors.password = 'Şifre gereklidir';
    }
    
    // Diğer validasyonlar
    Object.keys(formData).forEach(field => {
      const error = validateField(field, formData[field]);
      if (error) {
        validationErrors[field] = error;
      }
    });
    
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      return;
    }
    
    try {
      setIsLoading(true);
      await login(formData.username, formData.password);
      console.log('Login successful!');
      
      // Başarılı giriş sonrası sayaçları sıfırla
      setLoginAttempts(0);
      setIsLocked(false);
      setGeneralError('');
    } catch (error) {
      console.error('Login error details:', error);
      
      // Başarısız giriş sayacını artır
      const newAttempts = loginAttempts + 1;
      setLoginAttempts(newAttempts);
      
      // 5 başarısız deneme sonrası hesabı kilitle
      if (newAttempts >= 5) {
        setIsLocked(true);
        Alert.alert(
          'Hesap Kilitlendi', 
          'Çok fazla başarısız giriş denemesi. Hesabınız 15 dakika kilitlendi.',
          [
            {
              text: 'Tamam',
              onPress: () => {
                // 15 dakika sonra kilidi aç
                setTimeout(() => {
                  setIsLocked(false);
                  setLoginAttempts(0);
                }, 15 * 60 * 1000);
              }
            }
          ]
        );
        return;
      }
      
      // Kullanıcı dostu hata mesajları
      let errorMessage = 'Giriş başarısız';
      
      if (error instanceof Error) {
        if (error.message.includes('network') || error.message.includes('Network')) {
          errorMessage = 'Ağ bağlantısı hatası. Lütfen internet bağlantınızı kontrol edin.';
        } else if (error.message.includes('unauthorized') || error.message.includes('401')) {
          errorMessage = `Kullanıcı adı veya şifre hatalı. Kalan deneme: ${5 - newAttempts}`;
        } else if (error.message.includes('server') || error.message.includes('500')) {
          errorMessage = 'Sunucu hatası. Lütfen daha sonra tekrar deneyin.';
        } else {
          errorMessage = error.message;
        }
      }
      
      // Genel hata mesajını göster
      setGeneralError(errorMessage);
    } finally {
      setIsLoading(false);
    }
  }, [formData, login, loginAttempts, isLocked]);



  // Form submit
  const handleSubmit = useCallback(() => {
    Keyboard.dismiss();
    handleLogin();
  }, [handleLogin]);

  return (
    <>
      <StatusBar style="light" hidden={true} />
      <SafeAreaView style={styles.container} edges={['top', 'left', 'right', 'bottom']}>
        <KeyboardAvoidingView 
          behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
          style={styles.keyboardAvoidingView}
        >
        <TouchableWithoutFeedback onPress={Keyboard.dismiss}>
          <ScrollView 
            contentContainerStyle={styles.scrollContent}
            keyboardShouldPersistTaps="handled"
            showsVerticalScrollIndicator={false}
          >
            {/* Logo ve Başlık */}
            <View style={styles.header}>
              <View style={styles.logoContainer}>
                <Ionicons name="cash-outline" size={64} color="#007AFF" />
              </View>
              <Text style={styles.title}>KasseAPP</Text>
              <Text style={styles.subtitle}>Kasiyer Giriş Sistemi</Text>
            </View>

            {/* Giriş Formu */}
            <View style={styles.formContainer}>
              <Text style={styles.formTitle}>Giriş Yap</Text>
              
              {/* Güvenlik Göstergesi */}
              <SecurityIndicator 
                isSecure={true} 
                message="HTTPS güvenli bağlantı aktif"
              />
              
              {/* Giriş Denemeleri Bilgisi */}
              {loginAttempts > 0 && (
                <View style={styles.attemptsInfo}>
                  <Ionicons name="information-circle-outline" size={16} color="#007AFF" />
                  <Text style={styles.attemptsText}>
                    Başarısız giriş denemesi: {loginAttempts}/5
                  </Text>
                </View>
              )}
              
              {/* Giriş Geçmişi Bilgisi */}
              <View style={styles.loginHistoryInfo}>
                <Ionicons name="time-outline" size={16} color="#6c757d" />
                <Text style={styles.loginHistoryText}>
                  Son giriş: {new Date().toLocaleDateString('tr-TR')}
                </Text>
              </View>
              
              {/* Genel Hata Mesajı */}
              {generalError && (
                <ErrorMessage 
                  message={generalError} 
                  type="error" 
                  showIcon={true}
                />
              )}
              
              <FormInput
                  label="Kullanıcı Adı veya Email"
                  placeholder="Kullanıcı adı, ID veya email adresinizi giriniz"
                  value={formData.username}
                  onChangeText={(value) => handleInputChange('username', value)}
                  onBlur={() => handleInputBlur('username')}
                  error={errors.username}
                  touched={touched.username}
                  leftIcon="person-outline"
                  autoCapitalize="none"
                  autoCorrect={false}
                  editable={!isLoading}
                  returnKeyType="next"
                />
                
                {/* Kullanıcı Adı Karakter Sayacı */}
                <View style={styles.characterCounter}>
                  <Text style={styles.counterText}>
                    {formData.username.length}/55 karakter
                  </Text>
                </View>

                              <FormInput
                  label="Şifre"
                  placeholder="Şifrenizi giriniz"
                  value={formData.password}
                  onChangeText={(value) => handleInputChange('password', value)}
                  onBlur={() => handleInputBlur('password')}
                  error={errors.password}
                  touched={touched.password}
                  leftIcon="lock-closed-outline"
                  rightIcon={showPassword ? "eye-off-outline" : "eye-outline"}
                  onRightIconPress={togglePasswordVisibility}
                  secureTextEntry={!showPassword}
                  editable={!isLoading}
                  returnKeyType="done"
                  onSubmitEditing={handleSubmit}
                />
                
                {/* Şifre Gücü Göstergesi */}
                <PasswordStrength password={formData.password} />

              {/* Giriş Butonu */}
              <Button
                title={isLocked ? "Hesap Kilitli" : "Giriş Yap"}
                onPress={handleSubmit}
                loading={isLoading}
                disabled={isLoading || isLocked}
                style={styles.loginButton}
              />
              
              {/* Güvenlik Uyarısı */}
              <View style={styles.securityWarning}>
                <Ionicons name="shield-outline" size={16} color="#6c757d" />
                <Text style={styles.securityWarningText}>
                  Giriş bilgileriniz güvenli şekilde şifrelenerek iletilir
                </Text>
              </View>
              
              {/* Ek Güvenlik Bilgileri */}
              <View style={styles.additionalSecurityInfo}>
                <View style={styles.securityFeature}>
                  <Ionicons name="lock-closed-outline" size={16} color="#34C759" />
                  <Text style={styles.securityFeatureText}>SSL/TLS Şifreleme</Text>
                </View>
                <View style={styles.securityFeature}>
                  <Ionicons name="shield-checkmark-outline" size={16} color="#34C759" />
                  <Text style={styles.securityFeatureText}>RKSV Uyumlu</Text>
                </View>
              </View>
            </View>
            
            {/* Alt Bilgi */}
            <View style={styles.footer}>
                <View style={styles.securityInfo}>
                  <Ionicons name="shield-checkmark-outline" size={16} color="#34C759" />
                  <Text style={styles.securityText}>
                    Otomatik çıkış: 30 dakika inaktivite
                  </Text>
                </View>
                <Text style={styles.footerText}>
                  Güvenli kasiyer giriş sistemi
                </Text>
                <Text style={styles.versionText}>v2.1.0</Text>
              </View>
          </ScrollView>
        </TouchableWithoutFeedback>
      </KeyboardAvoidingView>
    </SafeAreaView>
    </>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f8f9fa',
    paddingTop: 0,
    marginTop: 0,
    top: 0,
    position: 'absolute',
    left: 0,
    right: 0,
    bottom: 0,
  },
  keyboardAvoidingView: {
    flex: 1,
  },
  scrollContent: {
    flexGrow: 1,
    justifyContent: 'center',
    paddingHorizontal: 24,
    paddingTop: 40,
    paddingBottom: 20,
    minHeight: height - 60, // Safe area için
  },
  
  // Header Styles
  header: {
    alignItems: 'center',
    marginBottom: 40,
    marginTop: 20,
  },
  logoContainer: {
    width: 120,
    height: 120,
    borderRadius: 60,
    backgroundColor: 'white',
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 20,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.1,
    shadowRadius: 8,
    elevation: 8,
  },
  title: {
    fontSize: 32,
    fontWeight: 'bold',
    color: '#1a1a1a',
    marginBottom: 8,
  },
  subtitle: {
    fontSize: 16,
    color: '#666',
    textAlign: 'center',
  },
  
  // Form Styles
  formContainer: {
    backgroundColor: 'white',
    borderRadius: 20,
    padding: 24,
    marginBottom: 30,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.1,
    shadowRadius: 16,
    elevation: 12,
  },
  formTitle: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#1a1a1a',
    marginBottom: 24,
    textAlign: 'center',
  },
  loginButton: {
    marginTop: 8,
    marginBottom: 20,
  },
  
  // Attempts Info
  attemptsInfo: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
    paddingHorizontal: 12,
    backgroundColor: '#e3f2fd',
    borderRadius: 8,
    marginBottom: 16,
  },
  attemptsText: {
    fontSize: 14,
    color: '#1976d2',
    marginLeft: 8,
    fontWeight: '500',
  },
  
  // Character Counter
  characterCounter: {
    alignItems: 'flex-end',
    marginTop: -8,
    marginBottom: 8,
  },
  counterText: {
    fontSize: 12,
    color: '#666',
  },
  
  // Login History Info
  loginHistoryInfo: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
    paddingHorizontal: 12,
    backgroundColor: '#f8f9fa',
    borderRadius: 8,
    marginBottom: 16,
  },
  loginHistoryText: {
    fontSize: 14,
    color: '#6c757d',
    marginLeft: 8,
    fontWeight: '500',
  },
  
  // Security Warning
  securityWarning: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
    paddingHorizontal: 12,
    backgroundColor: '#f8f9fa',
    borderRadius: 8,
    marginBottom: 20,
  },
  securityWarningText: {
    fontSize: 12,
    color: '#6c757d',
    marginLeft: 8,
    textAlign: 'center',
    flex: 1,
  },
  
  // Additional Security Info
  additionalSecurityInfo: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    paddingVertical: 16,
    marginTop: 8,
  },
  securityFeature: {
    alignItems: 'center',
    flex: 1,
  },
  securityFeatureText: {
    fontSize: 12,
    color: '#34C759',
    marginTop: 4,
    fontWeight: '600',
    textAlign: 'center',
  },
  
  // Footer
  footer: {
    alignItems: 'center',
  },
  securityInfo: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 12,
    paddingHorizontal: 16,
    paddingVertical: 8,
    backgroundColor: '#f0f9ff',
    borderRadius: 8,
  },
  securityText: {
    fontSize: 14,
    color: '#0369a1',
    marginLeft: 8,
    fontWeight: '500',
  },
  footerText: {
    fontSize: 14,
    color: '#666',
    textAlign: 'center',
    marginBottom: 8,
  },
  versionText: {
    fontSize: 12,
    color: '#999',
  },
}); 