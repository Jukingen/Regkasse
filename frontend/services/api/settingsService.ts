import { useFetch } from './useFetch';

// Kullanıcı ayarlarını getir
export const getUserSettings = () => {
  return useFetch<any>('/settings/user');
};

// Kullanıcı ayarlarını güncelle
export const updateUserSettings = (settings: any) => {
  return useFetch<any>('/settings/user', { method: 'PUT' });
};

// Kullanıcı ayarlarını sıfırla
export const resetUserSettings = () => {
  return useFetch<any>('/settings/user/reset', { method: 'POST' });
}; 