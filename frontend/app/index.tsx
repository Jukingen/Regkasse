import { Redirect } from 'expo-router';
import { useAuth } from '../contexts/AuthContext';
import { View, ActivityIndicator, Text } from 'react-native';
import { useEffect, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';

export default function Index() {
    const { isAuthenticated, isLoading, user } = useAuth();
    const [isChecking, setIsChecking] = useState(true);

    useEffect(() => {
        const checkAuth = async () => {
            try {
                const token = await AsyncStorage.getItem('token');
                const storedUser = await AsyncStorage.getItem('user');
                
                console.log('Index: Auth check - Token:', !!token, 'User:', !!storedUser, 'State:', { isAuthenticated, isLoading });
                
                // Token yoksa kesinlikle login sayfasına yönlendir
                if (!token) {
                    console.log('Index: No token found, redirecting to login');
                }
            } catch (error) {
                console.error('Index: Auth check error:', error);
            } finally {
                setIsChecking(false);
            }
        };

        checkAuth();
    }, [isAuthenticated, isLoading]);

    if (isLoading || isChecking) {
        return (
            <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
                <ActivityIndicator size="large" color="#007AFF" />
                <Text style={{ marginTop: 10, color: '#666' }}>Loading...</Text>
            </View>
        );
    }

    // Kullanıcı giriş yapmamışsa veya user bilgisi yoksa login sayfasına yönlendir
    if (!isAuthenticated || !user) {
        console.log('Index: Not authenticated or no user, redirecting to login');
        return <Redirect href="/(auth)/login" />;
    }

    console.log('Index: Authenticated, redirecting to cash register');
    return <Redirect href="/(tabs)/cash-register" />;
} 