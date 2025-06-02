import AsyncStorage from '@react-native-async-storage/async-storage';

// API temel URL'si
export const API_BASE_URL = 'http://localhost:5183/api';

// API istekleri için yardımcı fonksiyonlar
export const apiClient = {
    async get<T>(endpoint: string): Promise<T> {
        const token = await AsyncStorage.getItem('token');
        const response = await fetch(`${API_BASE_URL}${endpoint}`, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                ...(token ? { 'Authorization': `Bearer ${token}` } : {})
            }
        });

        if (!response.ok) {
            if (response.status === 401) {
                await AsyncStorage.removeItem('token');
                // Burada kullanıcıyı login sayfasına yönlendirebilirsiniz
            }
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        return response.json();
    },

    async post<T>(endpoint: string, data: any): Promise<T> {
        const token = await AsyncStorage.getItem('token');
        const response = await fetch(`${API_BASE_URL}${endpoint}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                ...(token ? { 'Authorization': `Bearer ${token}` } : {})
            },
            body: JSON.stringify(data)
        });

        if (!response.ok) {
            if (response.status === 401) {
                await AsyncStorage.removeItem('token');
            }
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        return response.json();
    },

    async put<T>(endpoint: string, data: any): Promise<T> {
        const token = await AsyncStorage.getItem('token');
        const response = await fetch(`${API_BASE_URL}${endpoint}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                ...(token ? { 'Authorization': `Bearer ${token}` } : {})
            },
            body: JSON.stringify(data)
        });

        if (!response.ok) {
            if (response.status === 401) {
                await AsyncStorage.removeItem('token');
            }
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        return response.json();
    },

    async delete(endpoint: string): Promise<void> {
        const token = await AsyncStorage.getItem('token');
        const response = await fetch(`${API_BASE_URL}${endpoint}`, {
            method: 'DELETE',
            headers: {
                'Content-Type': 'application/json',
                ...(token ? { 'Authorization': `Bearer ${token}` } : {})
            }
        });

        if (!response.ok) {
            if (response.status === 401) {
                await AsyncStorage.removeItem('token');
            }
            throw new Error(`HTTP error! status: ${response.status}`);
        }
    }
}; 