import { MaterialIcons } from '@expo/vector-icons';
import AsyncStorage from '@react-native-async-storage/async-storage';
import React, { useState, useEffect } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Alert, ScrollView } from 'react-native';

interface DemoUser {
  username: string;
  password: string;
  role: string;
  description: string;
  permissions: string[];
}

export default function DemoLogin({ onLogin }: { onLogin: (userData: any) => void }) {
  const [demoUsers, setDemoUsers] = useState<{ Cashiers: DemoUser[], Admins: DemoUser[] }>({ Cashiers: [], Admins: [] });
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    fetchDemoUsers();
  }, []);

  const fetchDemoUsers = async () => {
    try {
      const response = await fetch('http://localhost:5000/api/demo/login-info');
      const data = await response.json();
      setDemoUsers(data);
    } catch (error) {
      console.error('Demo kullanıcı bilgileri alınamadı:', error);
      // Fallback demo kullanıcıları
      setDemoUsers({
        Cashiers: [
          { username: "demo.cashier1", password: "Demo123!", role: "Cashier", description: "Demo Kasiyer 1", permissions: ["Satış", "Sepet", "Ödeme", "Fatura"] },
          { username: "demo.cashier2", password: "Demo123!", role: "Cashier", description: "Demo Kasiyer 2", permissions: ["Satış", "Sepet", "Ödeme", "Fatura"] }
        ],
        Admins: [
          { username: "demo.admin1", password: "Admin123!", role: "Admin", description: "Demo Admin 1", permissions: ["Tüm özellikler"] },
          { username: "demo.admin2", password: "Admin123!", role: "Admin", description: "Demo Admin 2", permissions: ["Tüm özellikler"] }
        ]
      });
    }
  };

  const handleDemoLogin = async (user: DemoUser) => {
    setLoading(true);
    
    try {
      // Demo giriş simülasyonu
      const loginResponse = await fetch('http://localhost:5000/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          username: user.username,
          password: user.password
        })
      });

      if (loginResponse.ok) {
        const userData = await loginResponse.json();
        
        // Kullanıcı bilgilerini kaydet
        await AsyncStorage.setItem('userToken', userData.token);
        await AsyncStorage.setItem('userRole', user.role);
        await AsyncStorage.setItem('userData', JSON.stringify(userData));
        
        Alert.alert(
          'Demo Giriş Başarılı',
          `${user.description} olarak giriş yapıldı.\nRol: ${user.role}`,
          [{ text: 'Tamam', onPress: () => onLogin(userData) }]
        );
      } else {
        throw new Error('Giriş başarısız');
      }
    } catch (error) {
      Alert.alert('Hata', 'Demo giriş yapılamadı. Lütfen backend servisinin çalıştığından emin olun.');
    } finally {
      setLoading(false);
    }
  };

  const renderUserCard = (user: DemoUser, index: number) => (
    <TouchableOpacity
      key={index}
      style={[
        styles.userCard,
        { backgroundColor: user.role === 'Admin' ? '#e3f2fd' : '#f3e5f5' }
      ]}
      onPress={() => handleDemoLogin(user)}
      disabled={loading}
    >
      <View style={styles.userInfo}>
        <MaterialIcons 
          name={user.role === 'Admin' ? 'admin-panel-settings' : 'person'} 
          size={32} 
          color={user.role === 'Admin' ? '#1976d2' : '#7b1fa2'} 
        />
        <View style={styles.userDetails}>
          <Text style={styles.userName}>{user.username}</Text>
          <Text style={styles.userDescription}>{user.description}</Text>
          <Text style={styles.userRole}>Rol: {user.role}</Text>
        </View>
      </View>
      
      <View style={styles.loginInfo}>
        <Text style={styles.passwordText}>Şifre: {user.password}</Text>
        <MaterialIcons name="login" size={24} color="#666" />
      </View>
    </TouchableOpacity>
  );

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>Demo Kullanıcılar</Text>
        <Text style={styles.subtitle}>Test için demo hesaplarla giriş yapın</Text>
      </View>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Kasiyer Hesapları</Text>
        <Text style={styles.sectionDescription}>
          Sadece satış, ödeme alma ve ürün görüntüleme ekranlarına erişebilir
        </Text>
        {demoUsers.Cashiers?.map((user, index) => renderUserCard(user, index))}
      </View>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Admin Hesapları</Text>
        <Text style={styles.sectionDescription}>
          Tüm yönetici yetkilerine sahip, kullanıcı yönetimi, raporlar ve ayarlar
        </Text>
        {demoUsers.Admins?.map((user, index) => renderUserCard(user, index))}
      </View>

      <View style={styles.infoBox}>
        <MaterialIcons name="info" size={20} color="#1976d2" />
        <Text style={styles.infoText}>
          Demo hesaplar sadece test amaçlıdır. Gerçek kullanımda kendi hesaplarınızı oluşturun.
        </Text>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  header: {
    padding: 20,
    backgroundColor: '#fff',
    alignItems: 'center',
  },
  title: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#333',
  },
  subtitle: {
    fontSize: 16,
    color: '#666',
    marginTop: 8,
  },
  section: {
    margin: 16,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 8,
  },
  sectionDescription: {
    fontSize: 14,
    color: '#666',
    marginBottom: 12,
    fontStyle: 'italic',
  },
  userCard: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
    elevation: 2,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
  },
  userInfo: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 12,
  },
  userDetails: {
    marginLeft: 12,
    flex: 1,
  },
  userName: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
  },
  userDescription: {
    fontSize: 14,
    color: '#666',
    marginTop: 2,
  },
  userRole: {
    fontSize: 12,
    color: '#999',
    marginTop: 2,
  },
  loginInfo: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: '#eee',
  },
  passwordText: {
    fontSize: 12,
    color: '#666',
    fontFamily: 'monospace',
  },
  infoBox: {
    flexDirection: 'row',
    backgroundColor: '#e3f2fd',
    padding: 16,
    margin: 16,
    borderRadius: 8,
    alignItems: 'center',
  },
  infoText: {
    fontSize: 14,
    color: '#1976d2',
    marginLeft: 8,
    flex: 1,
  },
}); 