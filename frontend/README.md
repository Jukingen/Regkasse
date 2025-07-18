# Cash Register App

## 📱 Cashier Application
React Native/Expo based mobile cash register application for cashiers. Built with TypeScript, offering a modern and user-friendly interface for daily cash register operations.

## 🔐 Login Credentials
**Email:** `admin@admin.com`  
**Password:** `Admin123!`
**Kullanıcı	Şifre	Rol	Erişebileceği Özellikler	Erişemeyeceği Özellikler**
**demo.cashier1	Demo123!	Cashier	Satış, Sepet, Ödeme, Fatura	Kullanıcı yönetimi, Raporlar, Ayarlar**
**demo.cashier2	Demo123!	Cashier	Satış, Sepet, Ödeme, Fatura	Kullanıcı yönetimi, Raporlar, Ayarlar**
**demo.admin1	Admin123!	Admin	Tüm özellikler	Yok**
**demo.admin2	Admin123!	Admin	Tüm özellikler	Yok**
 

## 🛠️ Technical Details
- **Framework:** React Native/Expo
- **Language:** TypeScript
- **State Management:** React Context + Custom Hooks
- **UI Library:** React Native Paper
- **Multi-language:** i18next
- **Form Management:** React Hook Form
- **Validation:** Zod

## 📦 Installation

### Requirements
- Node.js 18+
- npm or yarn
- Expo CLI
- Android Studio (for Android development)
- Xcode (for iOS development, macOS only)

### Installation Steps
1. Install dependencies:
```bash
npm install
```

2. Start development server:
```bash
npm start
```

3. Test with Expo Go app:
```bash
npm run android  # For Android
npm run ios     # For iOS (macOS only)
```

## 🏗️ Project Structure
```
frontend/
├── app/              # Main application pages
│   ├── (auth)/       # Authentication pages
│   └── (tabs)/       # Main tabs (Cash Register, Settings)
├── components/       # Reusable components
│   ├── ChangeCalculator.tsx
│   ├── OrderManager.tsx
│   ├── LanguageSelector.tsx
│   └── PrinterSettings.tsx
├── hooks/           # Custom React hooks
├── services/        # API services
│   └── api/         # Backend API integration
├── contexts/        # React contexts
├── i18n/            # Multi-language files
├── constants/       # Constants and styles
└── assets/          # Images, fonts, etc.
```

## 🔧 Development

### Important Commands
```bash
npm start           # Start development server
npm run android    # Start Android app
npm run ios        # Start iOS app
npm run test       # Run tests
npm run lint       # Lint check
npm run build      # Production build
```

### Features
- **Product Management:** Add products to cart, manage quantities
- **Payment Processing:** Cash, card, and voucher payments
- **Change Calculation:** Automatic change calculation with breakdown
- **Order Management:** Create and manage orders for restaurants
- **Settings:** Language, printer, and TSE device settings

### RKSV Compliance
- TSE device integration required
- Receipt signing with TSE
- Daily reporting (Tagesabschluss)
- Tax calculation (20%, 10%, 13%)

### Printer Integration
- EPSON TM-T88VI support
- Star TSP 700 support
- OCRA-B font requirement

## 📚 Detailed Documentation
For more detailed information, see [DEVELOPMENT.md](DEVELOPMENT.md).

## ⚠️ Important Notes
- TSE device connection is required
- All receipts must have TSE signature
- Tax number format: ATU12345678
- Customer ID must be 8 digits

## 🎯 Cashier Workflow
1. **Login** with cashier credentials
2. **Add Products** to cart from product list
3. **Process Payment** with selected method
4. **Calculate Change** if needed
5. **Print Receipt** automatically
6. **Manage Orders** for restaurant operations

## 🔒 Security
- JWT-based authentication
- Role-based access control
- Secure API communication
- Data encryption for sensitive information

---

## 1. Backend - Demo Kullanıcı Oluşturma

```csharp
// Services/DemoUserService.cs
using Microsoft.AspNetCore.Identity;
using Registrierkasse_API.Models;

namespace Registrierkasse_API.Services
{
    public class DemoUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;
        private readonly ILogger<DemoUserService> _logger;

        public DemoUserService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext context,
            ILogger<DemoUserService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        public async Task CreateDemoUsers()
        {
            try
            {
                // Rolleri oluştur
                await CreateRoles();

                // Demo kasiyerler
                await CreateDemoCashiers();

                // Demo adminler
                await CreateDemoAdmins();

                _logger.LogInformation("Demo kullanıcılar başarıyla oluşturuldu");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Demo kullanıcı oluşturma hatası");
                throw;
            }
        }

        private async Task CreateRoles()
        {
            var roles = new[] { "Cashier", "Admin", "Manager" };
            
            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }

        private async Task CreateDemoCashiers()
        {
            var cashiers = new[]
            {
                new { UserName = "demo.cashier1", Email = "cashier1@demo.com", FirstName = "Ahmet", LastName = "Kasiyer", EmployeeNumber = "CASH001" },
                new { UserName = "demo.cashier2", Email = "cashier2@demo.com", FirstName = "Ayşe", LastName = "Kasiyer", EmployeeNumber = "CASH002" }
            };

            foreach (var cashier in cashiers)
            {
                var user = new ApplicationUser
                {
                    UserName = cashier.UserName,
                    Email = cashier.Email,
                    FirstName = cashier.FirstName,
                    LastName = cashier.LastName,
                    EmployeeNumber = cashier.EmployeeNumber,
                    EmailConfirmed = true,
                    Role = "Cashier"
                };

                if (await _userManager.FindByNameAsync(cashier.UserName) == null)
                {
                    var result = await _userManager.CreateAsync(user, "Demo123!");
                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, "Cashier");
                        _logger.LogInformation($"Demo kasiyer oluşturuldu: {cashier.UserName}");
                    }
                }
            }
        }

        private async Task CreateDemoAdmins()
        {
            var admins = new[]
            {
                new { UserName = "demo.admin1", Email = "admin1@demo.com", FirstName = "Mehmet", LastName = "Admin", EmployeeNumber = "ADMIN001" },
                new { UserName = "demo.admin2", Email = "admin2@demo.com", FirstName = "Fatma", LastName = "Admin", EmployeeNumber = "ADMIN002" }
            };

            foreach (var admin in admins)
            {
                var user = new ApplicationUser
                {
                    UserName = admin.UserName,
                    Email = admin.Email,
                    FirstName = admin.FirstName,
                    LastName = admin.LastName,
                    EmployeeNumber = admin.EmployeeNumber,
                    EmailConfirmed = true,
                    Role = "Admin"
                };

                if (await _userManager.FindByNameAsync(admin.UserName) == null)
                {
                    var result = await _userManager.CreateAsync(user, "Admin123!");
                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, "Admin");
                        _logger.LogInformation($"Demo admin oluşturuldu: {admin.UserName}");
                    }
                }
            }
        }

        public async Task<List<DemoUserInfo>> GetDemoUsers()
        {
            var demoUsers = await _userManager.Users
                .Where(u => u.UserName.StartsWith("demo."))
                .Select(u => new DemoUserInfo
                {
                    UserName = u.UserName,
                    Email = u.Email,
                    FullName = $"{u.FirstName} {u.LastName}",
                    EmployeeNumber = u.EmployeeNumber,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return demoUsers;
        }
    }

    public class DemoUserInfo
    {
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public string EmployeeNumber { get; set; }
        public string Role { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
```

---

## 2. Backend - Demo Controller

```csharp
// Controllers/DemoController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse_API.Services;

namespace Registrierkasse_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DemoController : ControllerBase
    {
        private readonly DemoUserService _demoUserService;
        private readonly ILogger<DemoController> _logger;

        public DemoController(DemoUserService demoUserService, ILogger<DemoController> logger)
        {
            _demoUserService = demoUserService;
            _logger = logger;
        }

        [HttpPost("create-users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateDemoUsers()
        {
            try
            {
                await _demoUserService.CreateDemoUsers();
                return Ok(new { message = "Demo kullanıcılar başarıyla oluşturuldu" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Demo kullanıcı oluşturma hatası");
                return StatusCode(500, new { error = "Demo kullanıcılar oluşturulamadı" });
            }
        }

        [HttpGet("users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetDemoUsers()
        {
            try
            {
                var users = await _demoUserService.GetDemoUsers();
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Demo kullanıcı listesi alınamadı");
                return StatusCode(500, new { error = "Kullanıcı listesi alınamadı" });
            }
        }

        [HttpGet("login-info")]
        public IActionResult GetDemoLoginInfo()
        {
            var loginInfo = new
            {
                Cashiers = new[]
                {
                    new { Username = "demo.cashier1", Password = "Demo123!", Role = "Cashier", Description = "Demo Kasiyer 1" },
                    new { Username = "demo.cashier2", Password = "Demo123!", Role = "Cashier", Description = "Demo Kasiyer 2" }
                },
                Admins = new[]
                {
                    new { Username = "demo.admin1", Password = "Admin123!", Role = "Admin", Description = "Demo Admin 1" },
                    new { Username = "demo.admin2", Password = "Admin123!", Role = "Admin", Description = "Demo Admin 2" }
                }
            };

            return Ok(loginInfo);
        }
    }
}
```

---

## 3. Frontend (Expo) - Demo Login Component

```tsx
// frontend/components/DemoLogin.tsx
import React, { useState, useEffect } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Alert, ScrollView } from 'react-native';
import { MaterialIcons } from '@expo/vector-icons';
import { PermissionHelper } from '../shared/utils/PermissionHelper';
import { UserRole } from '../shared/types/Roles';

interface DemoUser {
  username: string;
  password: string;
  role: string;
  description: string;
}

export default function DemoLogin({ onLogin }) {
  const [demoUsers, setDemoUsers] = useState<{ Cashiers: DemoUser[], Admins: DemoUser[] }>({ Cashiers: [], Admins: [] });
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    fetchDemoUsers();
  }, []);

  const fetchDemoUsers = async () => {
    try {
      const response = await fetch('/api/demo/login-info');
      const data = await response.json();
      setDemoUsers(data);
    } catch (error) {
      console.error('Demo kullanıcı bilgileri alınamadı:', error);
    }
  };

  const handleDemoLogin = async (user: DemoUser) => {
    setLoading(true);
    
    try {
      // Demo giriş simülasyonu
      const loginResponse = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          username: user.username,
          password: user.password
        })
      });

      if (loginResponse.ok) {
        const userData = await loginResponse.json();
        
        // Rol bilgisini ayarla
        PermissionHelper.setUserRole(user.role as UserRole);
        
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
      Alert.alert('Hata', 'Demo giriş yapılamadı');
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
        {demoUsers.Cashiers?.map((user, index) => renderUserCard(user, index))}
      </View>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Admin Hesapları</Text>
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
    marginBottom: 12,
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
```

---

## 4. Admin Panel - Demo Kullanıcı Yönetimi

```tsx
// frontend-admin/components/DemoUserManagement.tsx
import React, { useState, useEffect } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Alert, FlatList } from 'react-native';
import { MaterialIcons } from '@expo/vector-icons';

interface DemoUser {
  username: string;
  email: string;
  fullName: string;
  employeeNumber: string;
  role: string;
  createdAt: string;
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
      const response = await fetch('/api/demo/users', {
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
      const response = await fetch('/api/demo/create-users', {
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
      </View>
      
      <View style={styles.userDetails}>
        <Text style={styles.userDetail}>E-posta: {item.email}</Text>
        <Text style={styles.userDetail}>Çalışan No: {item.employeeNumber}</Text>
        <Text style={styles.userDetail}>Oluşturulma: {new Date(item.createdAt).toLocaleDateString('tr-TR')}</Text>
      </View>
    </View>
  );

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>Demo Kullanıcı Yönetimi</Text>
        <TouchableOpacity style={styles.createButton} onPress={createDemoUsers}>
          <MaterialIcons name="add" size={24} color="#fff" />
          <Text style={styles.createButtonText}>Demo Kullanıcı Oluştur</Text>
        </TouchableOpacity>
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
  createButton: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#4caf50',
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 8,
  },
  createButtonText: {
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
});
```

---

## 5. Jest Test Senaryoları

```tsx
<code_block_to_apply_changes_from>
```

---

## 6. Demo Kullanıcı Bilgileri Tablosu

| Kullanıcı | Şifre | Rol | Erişebileceği Özellikler | Erişemeyeceği Özellikler |
|-----------|-------|-----|--------------------------|---------------------------|
| demo.cashier1 | Demo123! | Cashier | Satış, Sepet, Ödeme, Fatura | Kullanıcı yönetimi, Raporlar, Ayarlar |
| demo.cashier2 | Demo123! | Cashier | Satış, Sepet, Ödeme, Fatura | Kullanıcı yönetimi, Raporlar, Ayarlar |
| demo.admin1 | Admin123! | Admin | Tüm özellikler | Yok |
| demo.admin2 | Admin123! | Admin | Tüm özellikler | Yok |

Bu sistem, demo kullanıcıların güvenli bir şekilde test edilmesini sağlar ve rol bazlı erişim kontrolünü tam olarak test eder.
