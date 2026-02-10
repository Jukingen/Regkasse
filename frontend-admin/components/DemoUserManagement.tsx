import React, { useState, useEffect } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Alert, FlatList } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import MaterialIcons from 'react-native-vector-icons/MaterialIcons';

interface DemoUser {
  username: string;
  email: string;
  fullName: string;
  employeeNumber: string;
  role: string;
  isActive: boolean;
}

export default function DemoUserManagement() {
  const [demoUsers, setDemoUsers] = useState<DemoUser[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    fetchDemoUsers();
  }, []);

  const fetchDemoUsers = async () => {
    setLoading(true);
    try {
      const token = await AsyncStorage.getItem('userToken');
      const response = await fetch('http://localhost:5000/api/demo/users', {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const users = await response.json();
      setDemoUsers(users);
    } catch (error) {
      Alert.alert('Hata', 'Demo kullanıcılar alınamadı');
    } finally {
      setLoading(false);
    }
  };

  const createDemoUsers = async () => {
    try {
      const token = await AsyncStorage.getItem('userToken');
      const response = await fetch('http://localhost:5000/api/demo/create-users', {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${token}` }
      });
      
      if (response.ok) {
        Alert.alert('Başarılı', 'Demo kullanıcılar oluşturuldu');
        fetchDemoUsers();
      } else {
        throw new Error('Demo kullanıcılar oluşturulamadı');
      }
    } catch (error) {
      Alert.alert('Hata', 'Demo kullanıcılar oluşturulamadı');
    }
  };

  const deleteDemoUsers = async () => {
    Alert.alert(
      'Onay',
      'Tüm demo kullanıcıları silmek istediğinizden emin misiniz?',
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Sil',
          style: 'destructive',
          onPress: async () => {
            try {
              const token = await AsyncStorage.getItem('userToken');
              const response = await fetch('http://localhost:5000/api/demo/users', {
                method: 'DELETE',
                headers: { 'Authorization': `Bearer ${token}` }
              });
              
              if (response.ok) {
                Alert.alert('Başarılı', 'Demo kullanıcılar silindi');
                fetchDemoUsers();
              } else {
                throw new Error('Demo kullanıcılar silinemedi');
              }
            } catch (error) {
              Alert.alert('Hata', 'Demo kullanıcılar silinemedi');
            }
          }
        }
      ]
    );
  };

  const renderUserCard = ({ item }: { item: DemoUser }) => (
    <View style={styles.userCard}>
      <View style={styles.userHeader}>
        <MaterialIcons 
          name={item.role === 'Admin' ? 'admin-panel-settings' : 'person'} 
          size={24} 
          color={item.role === 'Admin' ? '#1976d2' : '#7b1fa2'} 
        />
        <View style={styles.userInfo}>
          <Text style={styles.userName}>{item.username}</Text>
          <Text style={styles.userFullName}>{item.fullName}</Text>
          <Text style={styles.userRole}>Rol: {item.role}</Text>
        </View>
        <View style={[styles.statusBadge, { backgroundColor: item.isActive ? '#4caf50' : '#f44336' }]}>
          <Text style={styles.statusText}>{item.isActive ? 'Aktif' : 'Pasif'}</Text>
        </View>
      </View>
      
      <View style={styles.userDetails}>
        <Text style={styles.userDetail}>E-posta: {item.email}</Text>
        <Text style={styles.userDetail}>Çalışan No: {item.employeeNumber}</Text>
      </View>
    </View>
  );

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>Demo Kullanıcı Yönetimi</Text>
        <View style={styles.buttonContainer}>
          <TouchableOpacity style={styles.createButton} onPress={createDemoUsers}>
            <MaterialIcons name="add" size={20} color="#fff" />
            <Text style={styles.createButtonText}>Oluştur</Text>
          </TouchableOpacity>
          <TouchableOpacity style={styles.deleteButton} onPress={deleteDemoUsers}>
            <MaterialIcons name="delete" size={20} color="#fff" />
            <Text style={styles.deleteButtonText}>Sil</Text>
          </TouchableOpacity>
        </View>
      </View>

      <FlatList
        data={demoUsers}
        renderItem={renderUserCard}
        keyExtractor={item => item.username}
        refreshing={loading}
        onRefresh={fetchDemoUsers}
        ListEmptyComponent={
          <View style={styles.emptyState}>
            <MaterialIcons name="people" size={48} color="#ccc" />
            <Text style={styles.emptyText}>Henüz demo kullanıcı yok</Text>
            <Text style={styles.emptySubtext}>Demo kullanıcıları oluşturmak için "Oluştur" butonuna tıklayın</Text>
          </View>
        }
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 16,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
  },
  title: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#333',
  },
  buttonContainer: {
    flexDirection: 'row',
    gap: 8,
  },
  createButton: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#4caf50',
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 6,
  },
  createButtonText: {
    color: '#fff',
    fontWeight: 'bold',
    marginLeft: 4,
  },
  deleteButton: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#f44336',
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 6,
  },
  deleteButtonText: {
    color: '#fff',
    fontWeight: 'bold',
    marginLeft: 4,
  },
  userCard: {
    backgroundColor: '#fff',
    margin: 8,
    padding: 16,
    borderRadius: 8,
    elevation: 2,
  },
  userHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 12,
  },
  userInfo: {
    marginLeft: 12,
    flex: 1,
  },
  userName: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
  },
  userFullName: {
    fontSize: 14,
    color: '#666',
  },
  userRole: {
    fontSize: 12,
    color: '#999',
  },
  statusBadge: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 12,
  },
  statusText: {
    fontSize: 10,
    color: '#fff',
    fontWeight: 'bold',
  },
  userDetails: {
    borderTopWidth: 1,
    borderTopColor: '#eee',
    paddingTop: 12,
  },
  userDetail: {
    fontSize: 12,
    color: '#666',
    marginBottom: 4,
  },
  emptyState: {
    alignItems: 'center',
    padding: 40,
  },
  emptyText: {
    fontSize: 16,
    color: '#999',
    marginTop: 16,
  },
  emptySubtext: {
    fontSize: 14,
    color: '#ccc',
    marginTop: 8,
    textAlign: 'center',
  },
}); 