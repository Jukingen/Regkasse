import { Stack } from 'expo-router';
import { useEffect } from 'react';
import { I18nextProvider } from 'react-i18next';
import i18n from '../i18n/config';
import { useColorScheme } from 'react-native';
import { AuthProvider } from '../contexts/AuthContext';
import { SystemProvider } from '../contexts/SystemContext';

export default function RootLayout() {
    const colorScheme = useColorScheme();

    useEffect(() => {
        i18n.init();
    }, []);

    return (
        <AuthProvider>
            <SystemProvider>
                <I18nextProvider i18n={i18n}>
                    <Stack
                        screenOptions={{
                            headerStyle: {
                                backgroundColor: colorScheme === 'dark' ? '#000' : '#fff',
                            },
                            headerTintColor: colorScheme === 'dark' ? '#fff' : '#000',
                        }}
                    >
                        <Stack.Screen
                            name="(auth)"
                            options={{
                                headerShown: false
                            }}
                        />
                        <Stack.Screen
                            name="(tabs)"
                            options={{
                                headerShown: false
                            }}
                        />
                    </Stack>
                </I18nextProvider>
            </SystemProvider>
        </AuthProvider>
    );
} 