import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { View, TextInput, TouchableOpacity, Text, StyleSheet, Alert, ActivityIndicator } from 'react-native';
import { useAuth } from '../../contexts/AuthContext';
import { ApiSettingsModal } from '../../components/ApiSettingsModal';

export default function LoginScreen() {
    console.log('LoginScreen loaded');
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [isLoading, setIsLoading] = useState(false);
    const [showApiSettings, setShowApiSettings] = useState(false);
    const { login } = useAuth();
    const { t } = useTranslation();

    const handleDebugInfo = () => {
        Alert.alert(
            t('login.debugTitle', 'Expo Go Debug Bilgileri'),
            t('login.debugInfo',
              'Expo Go\'da network erişimi için:\n\n' +
              '1. Bilgisayarınızın IP adresini öğrenin:\n' +
              '   Windows: ipconfig\n' +
              '   Mac/Linux: ifconfig\n\n' +
              '2. API Ayarları\'na gidin ve IP adresinizi girin\n\n' +
              '3. Aynı Wi-Fi ağında olduğunuzdan emin olun\n\n' +
              '4. Backend\'in çalıştığından emin olun\n\n' +
              '5. Firewall ayarlarını kontrol edin')
        );
    };

    const handleLogin = async () => {
        console.log('Login attempt started...');
        if (!username || !password) {
            console.log('Empty fields detected');
            Alert.alert(
                t('auth.loginError'),
                t('validation.required')
            );
            return;
        }
        try {
            setIsLoading(true);
            await login(username, password);
            console.log('Login successful!');
        } catch (error) {
            console.error('Login error details:', error);
            Alert.alert(
                t('auth.loginError'),
                error instanceof Error ? error.message : t('auth.loginError')
            );
        } finally {
            setIsLoading(false);
        }
    };

    const handleButtonPress = () => {
        console.log('Login button pressed');
        handleLogin();
    };

    return (
        <View style={styles.container}>
            <View style={styles.form}>
                <TextInput
                    style={styles.input}
                    placeholder={t('auth.username')}
                    value={username}
                    onChangeText={setUsername}
                    autoCapitalize="none"
                    editable={!isLoading}
                />
                <TextInput
                    style={styles.input}
                    placeholder={t('auth.password')}
                    value={password}
                    onChangeText={setPassword}
                    secureTextEntry
                    editable={!isLoading}
                />
                <TouchableOpacity
                    style={[styles.button, isLoading && styles.buttonDisabled]}
                    onPress={handleButtonPress}
                    disabled={isLoading}
                >
                    {isLoading ? (
                        <ActivityIndicator color="white" />
                    ) : (
                        <Text style={styles.buttonText}>{t('auth.button')}</Text>
                    )}
                </TouchableOpacity>
                <TouchableOpacity
                    style={styles.settingsButton}
                    onPress={() => setShowApiSettings(true)}
                    disabled={isLoading}
                >
                    <Text style={styles.settingsButtonText}>{t('login.apiSettings', 'API Ayarları')}</Text>
                </TouchableOpacity>
                <TouchableOpacity
                    style={styles.debugButton}
                    onPress={handleDebugInfo}
                    disabled={isLoading}
                >
                    <Text style={styles.debugButtonText}>{t('login.debugButton', 'Debug Bilgileri')}</Text>
                </TouchableOpacity>
            </View>
            <ApiSettingsModal
                visible={showApiSettings}
                onClose={() => setShowApiSettings(false)}
            />
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        justifyContent: 'center',
        padding: 20,
        backgroundColor: '#f5f5f5',
    },
    form: {
        backgroundColor: 'white',
        padding: 20,
        borderRadius: 10,
        shadowColor: '#000',
        shadowOffset: {
            width: 0,
            height: 2,
        },
        shadowOpacity: 0.25,
        shadowRadius: 3.84,
        elevation: 5,
    },
    input: {
        height: 50,
        borderWidth: 1,
        borderColor: '#ddd',
        borderRadius: 5,
        marginBottom: 15,
        paddingHorizontal: 10,
        backgroundColor: 'white',
    },
    button: {
        backgroundColor: '#007AFF',
        height: 50,
        borderRadius: 5,
        justifyContent: 'center',
        alignItems: 'center',
    },
    buttonText: {
        color: 'white',
        fontSize: 16,
        fontWeight: 'bold',
    },
    buttonDisabled: {
        backgroundColor: '#ccc',
    },
    settingsButton: {
        backgroundColor: '#6c757d',
        height: 40,
        borderRadius: 5,
        justifyContent: 'center',
        alignItems: 'center',
        marginTop: 10,
    },
    settingsButtonText: {
        color: 'white',
        fontSize: 14,
        fontWeight: '600',
    },
    debugButton: {
        backgroundColor: '#17a2b8',
        height: 40,
        borderRadius: 5,
        justifyContent: 'center',
        alignItems: 'center',
        marginTop: 10,
    },
    debugButtonText: {
        color: 'white',
        fontSize: 14,
        fontWeight: '600',
    },
}); 