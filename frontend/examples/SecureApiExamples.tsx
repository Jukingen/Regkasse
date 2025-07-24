import React, { useState } from 'react';
import { View, Text, TouchableOpacity, Alert, ScrollView } from 'react-native';
import { useSecureApi } from '../hooks/useSecureApi';
import { RoleGuard } from '../components/RoleGuard';

/**
 * JWT ve Role-Based Access Control kullanım örnekleri
 * Türkçe açıklama: Bu dosya güvenli API kullanımının nasıl yapılacağını gösterir
 */
export const SecureApiExamples: React.FC = () => {
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState<any>(null);
  
  const {
    secureGet,
    securePost,
    securePut,
    secureDelete,
    criticalOperation,
    checkDemoRestriction,
    checkTokenStatus,
    isAdmin,
    isCashier,
    isManager,
    hasPermission,
    hasRole
  } = useSecureApi();

  // Örnek 1: Basit güvenli GET isteği
  const handleSecureGet = async () => {
    setLoading(true);
    try {
      const result = await secureGet('/api/products');
      setData(result);
      Alert.alert('Başarılı', 'Ürünler başarıyla getirildi');
    } catch (error) {
      Alert.alert('Hata', 'Ürünler getirilemedi');
    } finally {
      setLoading(false);
    }
  };

  // Örnek 2: Yetki gerektiren GET isteği
  const handleAdminOnlyGet = async () => {
    setLoading(true);
    try {
      const result = await secureGet(
        '/api/users',
        { resource: 'users', action: 'read' }, // Gerekli yetki
        'Admin' // Gerekli rol
      );
      setData(result);
      Alert.alert('Başarılı', 'Kullanıcılar başarıyla getirildi');
    } catch (error) {
      Alert.alert('Hata', 'Kullanıcılar getirilemedi');
    } finally {
      setLoading(false);
    }
  };

  // Örnek 3: Güvenli POST isteği
  const handleSecurePost = async () => {
    setLoading(true);
    try {
      const newProduct = {
        name: 'Test Ürün',
        price: 10.99,
        category: 'Test'
      };

      const result = await securePost(
        '/api/products',
        newProduct,
        { resource: 'products', action: 'create' }
      );
      setData(result);
      Alert.alert('Başarılı', 'Ürün başarıyla oluşturuldu');
    } catch (error) {
      Alert.alert('Hata', 'Ürün oluşturulamadı');
    } finally {
      setLoading(false);
    }
  };

  // Örnek 4: Kritik işlem
  const handleCriticalOperation = async () => {
    setLoading(true);
    try {
      const result = await criticalOperation(
        async () => {
          // Kritik işlem: Sistem ayarlarını değiştir
          return await securePost('/api/system/settings', {
            maintenanceMode: true
          });
        },
        'Sistem Ayarları Değiştirme'
      );
      setData(result);
      Alert.alert('Başarılı', 'Kritik işlem başarıyla tamamlandı');
    } catch (error) {
      Alert.alert('Hata', 'Kritik işlem başarısız');
    } finally {
      setLoading(false);
    }
  };

  // Örnek 5: Demo kullanıcı kontrolü
  const handleDemoRestrictionCheck = () => {
    if (checkDemoRestriction('Gerçek Satış İşlemi')) {
      Alert.alert('Demo Kısıtlaması', 'Demo kullanıcılar bu işlemi yapamaz');
    }
  };

  // Örnek 6: Token durumu kontrolü
  const handleTokenStatusCheck = async () => {
    const isValid = await checkTokenStatus();
    Alert.alert(
      'Token Durumu',
      isValid ? 'Token geçerli' : 'Token geçersiz veya süresi dolmuş'
    );
  };

  return (
    <ScrollView style={{ flex: 1, padding: 16 }}>
      <Text style={{ fontSize: 24, fontWeight: 'bold', marginBottom: 20 }}>
        Güvenli API Kullanım Örnekleri
      </Text>

      {/* Kullanıcı Bilgileri */}
      <View style={{ marginBottom: 20, padding: 16, backgroundColor: '#f0f0f0' }}>
        <Text style={{ fontSize: 18, fontWeight: 'bold', marginBottom: 10 }}>
          Kullanıcı Durumu:
        </Text>
        <Text>Admin: {isAdmin ? 'Evet' : 'Hayır'}</Text>
        <Text>Cashier: {isCashier ? 'Evet' : 'Hayır'}</Text>
        <Text>Manager: {isManager ? 'Evet' : 'Hayır'}</Text>
        <Text>Ürün Okuma Yetkisi: {hasPermission('products', 'read') ? 'Var' : 'Yok'}</Text>
        <Text>Kullanıcı Yönetimi Yetkisi: {hasPermission('users', 'manage') ? 'Var' : 'Yok'}</Text>
      </View>

      {/* Örnek 1: Basit Güvenli GET */}
      <TouchableOpacity
        style={{ padding: 16, backgroundColor: '#007AFF', marginBottom: 10, borderRadius: 8 }}
        onPress={handleSecureGet}
        disabled={loading}
      >
        <Text style={{ color: 'white', textAlign: 'center', fontWeight: 'bold' }}>
          {loading ? 'Yükleniyor...' : '1. Güvenli GET - Ürünleri Getir'}
        </Text>
      </TouchableOpacity>

      {/* Örnek 2: Admin Only GET */}
      <RoleGuard role="Admin" fallback={
        <View style={{ padding: 16, backgroundColor: '#FFE5E5', marginBottom: 10, borderRadius: 8 }}>
          <Text style={{ color: '#D32F2F', textAlign: 'center' }}>
            Bu işlem için Admin rolü gereklidir
          </Text>
        </View>
      }>
        <TouchableOpacity
          style={{ padding: 16, backgroundColor: '#FF6B35', marginBottom: 10, borderRadius: 8 }}
          onPress={handleAdminOnlyGet}
          disabled={loading}
        >
          <Text style={{ color: 'white', textAlign: 'center', fontWeight: 'bold' }}>
            {loading ? 'Yükleniyor...' : '2. Admin Only GET - Kullanıcıları Getir'}
          </Text>
        </TouchableOpacity>
      </RoleGuard>

      {/* Örnek 3: Güvenli POST */}
      <TouchableOpacity
        style={{ padding: 16, backgroundColor: '#34C759', marginBottom: 10, borderRadius: 8 }}
        onPress={handleSecurePost}
        disabled={loading}
      >
        <Text style={{ color: 'white', textAlign: 'center', fontWeight: 'bold' }}>
          {loading ? 'Yükleniyor...' : '3. Güvenli POST - Yeni Ürün Oluştur'}
        </Text>
      </TouchableOpacity>

      {/* Örnek 4: Kritik İşlem */}
      <RoleGuard role="Admin" fallback={
        <View style={{ padding: 16, backgroundColor: '#FFF3E0', marginBottom: 10, borderRadius: 8 }}>
          <Text style={{ color: '#F57C00', textAlign: 'center' }}>
            Kritik işlemler için Admin rolü gereklidir
          </Text>
        </View>
      }>
        <TouchableOpacity
          style={{ padding: 16, backgroundColor: '#FF3B30', marginBottom: 10, borderRadius: 8 }}
          onPress={handleCriticalOperation}
          disabled={loading}
        >
          <Text style={{ color: 'white', textAlign: 'center', fontWeight: 'bold' }}>
            {loading ? 'Yükleniyor...' : '4. Kritik İşlem - Sistem Ayarları'}
          </Text>
        </TouchableOpacity>
      </RoleGuard>

      {/* Örnek 5: Demo Kısıtlaması */}
      <TouchableOpacity
        style={{ padding: 16, backgroundColor: '#AF52DE', marginBottom: 10, borderRadius: 8 }}
        onPress={handleDemoRestrictionCheck}
      >
        <Text style={{ color: 'white', textAlign: 'center', fontWeight: 'bold' }}>
          5. Demo Kısıtlaması Kontrolü
        </Text>
      </TouchableOpacity>

      {/* Örnek 6: Token Durumu */}
      <TouchableOpacity
        style={{ padding: 16, backgroundColor: '#5856D6', marginBottom: 10, borderRadius: 8 }}
        onPress={handleTokenStatusCheck}
      >
        <Text style={{ color: 'white', textAlign: 'center', fontWeight: 'bold' }}>
          6. Token Durumu Kontrolü
        </Text>
      </TouchableOpacity>

      {/* Sonuç Verisi */}
      {data && (
        <View style={{ marginTop: 20, padding: 16, backgroundColor: '#E8F5E8', borderRadius: 8 }}>
          <Text style={{ fontSize: 16, fontWeight: 'bold', marginBottom: 10 }}>
            Son İşlem Sonucu:
          </Text>
          <Text>{JSON.stringify(data, null, 2)}</Text>
        </View>
      )}
    </ScrollView>
  );
}; 