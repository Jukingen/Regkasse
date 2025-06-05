import React, { useState } from 'react';
import { View, TextInput, TouchableOpacity, Text, StyleSheet, Alert, ActivityIndicator } from 'react-native';
import { useAuth } from '../../contexts/AuthContext';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';

export default function LoginScreen() {
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [isLoading, setIsLoading] = useState(false);
    const { login } = useAuth();
    const { t } = useTranslation();

    const handleLogin = async () => {
        console.log('Login attempt started...'); // Debug log

        if (!username || !password) {
            console.log('Empty fields detected'); // Debug log
            Alert.alert(
                t('login.error.title'),
                t('login.error.empty_fields')
            );
            return;
        }

        try {
            console.log('Setting loading state...'); // Debug log
            setIsLoading(true);
            
            console.log('Calling login function...'); // Debug log
            console.log('Username:', username); // Debug log (şifreyi loglamıyoruz)
            
            await login(username, password);
            console.log('Login successful!'); // Debug log
        } catch (error) {
            console.error('Login error details:', error); // Debug log
            Alert.alert(
                t('login.error.title'),
                error instanceof Error ? error.message : t('login.error.invalid_credentials')
            );
        } finally {
            console.log('Login attempt finished'); // Debug log
            setIsLoading(false);
        }
    };

    // Debug için buton basıldığında tetiklenecek
    const handleButtonPress = () => {
        console.log('Login button pressed'); // Debug log
        handleLogin();
    };

    return (
        <View style={styles.container}>
            <View style={styles.form}>
                <TextInput
                    style={styles.input}
                    placeholder={t('login.username')}
                    value={username}
                    onChangeText={(text) => {
                        console.log('Username changed:', text); // Debug log
                        setUsername(text);
                    }}
                    autoCapitalize="none"
                    editable={!isLoading}
                />
                <TextInput
                    style={styles.input}
                    placeholder={t('login.password')}
                    value={password}
                    onChangeText={(text) => {
                        console.log('Password changed (length):', text.length); // Debug log
                        setPassword(text);
                    }}
                    secureTextEntry
                    editable={!isLoading}
                />
                <TouchableOpacity
                    style={[styles.button, isLoading && styles.buttonDisabled]}
                    onPress={handleButtonPress} // handleLogin yerine handleButtonPress kullanıyoruz
                    disabled={isLoading}
                >
                    {isLoading ? (
                        <ActivityIndicator color="white" />
                    ) : (
                        <Text style={styles.buttonText}>{t('login.button')}</Text>
                    )}
                </TouchableOpacity>
            </View>
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
}); 