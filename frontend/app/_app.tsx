import { Stack } from 'expo-router';
import { useEffect } from 'react';
import { I18nextProvider } from 'react-i18next';
import { AuthProvider } from '../contexts/AuthContext';
import { ThemeProvider } from '../contexts/ThemeContext';
import { ErrorBoundary } from '../components/ErrorBoundary';
import i18n from '../i18n';
import { useFonts } from 'expo-font';
import * as SplashScreen from 'expo-splash-screen';
import { StyleSheet } from 'react-native';

// Keep splash screen visible
SplashScreen.preventAutoHideAsync();

const styles = StyleSheet.create({
  receiptText: {
    fontFamily: 'OCRA-B',
    fontSize: 12,
  }
});

export default function RootLayout() {
    const [fontsLoaded] = useFonts({
        'OCRA-B': require('../assets/fonts/OCRA-B.ttf'),
    });

    useEffect(() => {
        if (fontsLoaded) {
            SplashScreen.hideAsync();
        }
    }, [fontsLoaded]);

    if (!fontsLoaded) {
        return null;
    }

    return (
        <ErrorBoundary>
            <ThemeProvider>
                <I18nextProvider i18n={i18n}>
                    <AuthProvider>
                        <Stack
                            screenOptions={{
                                headerStyle: {
                                    backgroundColor: '#fff',
                                },
                                headerTintColor: '#000',
                                headerTitleStyle: {
                                    fontWeight: 'bold',
                                },
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
                    </AuthProvider>
                </I18nextProvider>
            </ThemeProvider>
        </ErrorBoundary>
    );
} 