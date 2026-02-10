import { Platform } from 'react-native';

// Bilgisayarın IP adresini bulmak için yardımcı fonksiyonlar
export const getLocalIPInstructions = (): string => {
  if (Platform.OS === 'android') {
    return 'Android cihazınızda:\n\n' +
           '1. Ayarlar > Bağlantılar > Wi-Fi\n' +
           '2. Bağlı olduğunuz ağa dokunun\n' +
           '3. "Ağ bilgileri" veya "IP adresi" kısmına bakın\n\n' +
           'Veya bilgisayarınızın IP adresini kullanın:\n' +
           'Windows: ipconfig\n' +
           'Mac/Linux: ifconfig';
  } else if (Platform.OS === 'ios') {
    return 'iOS cihazınızda:\n\n' +
           '1. Ayarlar > Wi-Fi\n' +
           '2. Bağlı olduğunuz ağa dokunun\n' +
           '3. "IP Adresi" kısmına bakın\n\n' +
           'Veya bilgisayarınızın IP adresini kullanın:\n' +
           'Mac: ifconfig\n' +
           'Windows: ipconfig';
  }
  
  return 'Bilgisayarınızın IP adresini öğrenmek için:\n\n' +
         'Windows: ipconfig\n' +
         'Mac/Linux: ifconfig\n\n' +
         'IPv4 Address kısmındaki adresi kullanın.';
};

// Yaygın IP aralıklarını döndür
export const getCommonIPRanges = (): string[] => {
  return [
    '192.168.1.100',
    '192.168.1.101', 
    '192.168.1.102',
    '192.168.0.100',
    '192.168.0.101',
    '10.0.0.100',
    '10.0.0.101',
    '172.20.10.1',
    '172.20.10.2'
  ];
};

// IP adresinin geçerli olup olmadığını kontrol et
export const isValidIPAddress = (ip: string): boolean => {
  const ipRegex = /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$/;
  return ipRegex.test(ip);
};

// Test IP adreslerini döndür
export const getTestIPs = (): { label: string; ips: string[] }[] => {
  return [
    {
      label: 'Bilgisayarınızın IP Aralığı',
      ips: ['192.168.1.2', '192.168.1.3', '192.168.1.4', '192.168.1.5']
    },
    {
      label: 'Yaygın Router IP Aralığı',
      ips: ['192.168.1.100', '192.168.1.101', '192.168.1.102', '192.168.1.103', '192.168.1.104']
    },
    {
      label: 'Alternatif Router IP Aralığı',
      ips: ['192.168.0.100', '192.168.0.101', '192.168.0.102', '192.168.0.103', '192.168.0.104']
    },
    {
      label: 'Diğer Yaygın IP Aralığı',
      ips: ['10.0.0.100', '10.0.0.101', '10.0.0.102', '10.0.0.103', '10.0.0.104']
    },
    {
      label: 'iPhone Hotspot IP Aralığı',
      ips: ['172.20.10.1', '172.20.10.2', '172.20.10.3', '172.20.10.4', '172.20.10.5']
    }
  ];
};

// Network bağlantısını test et
export const testNetworkConnection = async (ipAddress: string): Promise<boolean> => {
  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 5000);
    
    const response = await fetch(`http://${ipAddress}:5183/api/health`, {
      method: 'GET',
      signal: controller.signal,
    });
    
    clearTimeout(timeoutId);
    return response.ok;
  } catch (error) {
    console.log(`Network test failed for ${ipAddress}:`, error);
    return false;
  }
};

// Otomatik IP adresi bulma (basit ping testi)
export const findWorkingIP = async (): Promise<string | null> => {
  const testIPs = getTestIPs().flatMap(group => group.ips);
  
  for (const ip of testIPs) {
    try {
      const isWorking = await testNetworkConnection(ip);
      if (isWorking) {
        console.log(`Working IP found: ${ip}`);
        return ip;
      }
    } catch (error) {
      console.log(`IP ${ip} test failed:`, error);
    }
  }
  
  return null;
}; 