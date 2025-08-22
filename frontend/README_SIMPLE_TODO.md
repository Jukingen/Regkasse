# 📝 Simple React Todo - Kullanım Kılavuzu

Projenize entegre edilmiş **basit todo list componentinin** detaylı kullanım kılavuzu.

## 🎯 Ne İçin Kullanılır?

### **Task-Master vs Simple Todo**

| Özellik | Task-Master (Gelişmiş) | Simple Todo (Basit) |
|---------|------------------------|---------------------|
| **AI Desteği** | ✅ Çoklu AI engine | ❌ Yok |
| **Görselleştirme** | ✅ Mind maps, analytics | ❌ Yok |
| **RKSV Compliance** | ✅ Full compliance | ✅ Kategori desteği |
| **Kullanım** | 🔧 Karmaşık | 📝 Çok basit |
| **Hız** | ⚡ Orta | ⚡⚡ Çok hızlı |
| **Senario** | Büyük projeler | Günlük notlar |

## 🚀 Hemen Kullanmaya Başlayın

### **1. Uygulamada Nerede?**
- 📱 **"Todo" sekmesi** → Ana todo ekranı
- 📋 Basit, hızlı todo listesi

### **2. Temel Kullanım**
```typescript
import SimpleTodo from '../components/SimpleTodo';

// Basit kullanım
<SimpleTodo />

// Özelleştirilmiş kullanım
<SimpleTodo
  storageKey="my_todos"
  maxItems={50}
  enableCategories={true}
  enablePriority={true}
/>
```

## 📋 Özellikler

### **✅ Temel Özellikler**
- ➕ **Todo ekleme** - Text input ile hızlı ekleme
- ☑️ **Tamamlama** - Checkbox ile işaretle
- 🗑️ **Silme** - Çöp kutusu ikonu ile sil
- 💾 **Otomatik kaydetme** - AsyncStorage ile local storage

### **🏷️ Kategori Sistemi**
- **RKSV** 🔴 - Yasal gereksinimler ve compliance
- **TSE** 🟠 - Teknik güvenlik cihazı işlemleri  
- **ALLGEMEIN** 🔵 - Genel görevler ve notlar

### **⭐ Öncelik Sistemi**
- **H (High)** 🔴 - Kritik/Acil görevler
- **M (Medium)** 🟡 - Normal öncelik
- **L (Low)** 🟢 - İsteğe bağlı

## 🎯 Kullanım Senaryoları

### **📋 RKSV Günlük Kontrol Listesi**
```
☐ TSE cihaz bağlantısını kontrol et [RKSV][H]
☐ Dün kalan işlemleri kontrol et [RKSV][M]
☐ System backup durumunu kontrol et [TSE][H]
☐ Compliance log'ları incele [RKSV][M]
```

### **⚡ Hızlı Notlar**
```
☐ Müşteri X ile görüşme [ALLGEMEIN][M]
☐ Yazılım güncelleme yap [TSE][L]
☐ Backup kontrolü [TSE][H]
☐ Finans raporu hazırla [RKSV][H]
```

### **🔧 Teknik Görevler**
```
☐ Payment gateway test [TSE][H]
☐ Database backup [TSE][M]
☐ Security patch update [TSE][H]
☐ Log rotation [ALLGEMEIN][L]
```

## ⚙️ Konfigürasyon Seçenekleri

### **Props Listesi**
```typescript
interface SimpleTodoProps {
  storageKey?: string;        // AsyncStorage key (default: 'simple_todo_items')
  maxItems?: number;          // Max todo sayısı (default: 50)
  enableCategories?: boolean; // Kategori sistemi (default: true)
  enablePriority?: boolean;   // Öncelik sistemi (default: true)
}
```

### **Farklı Konfigürasyonlar**

#### **Minimal Setup**
```typescript
<SimpleTodo
  storageKey="quick_notes"
  maxItems={20}
  enableCategories={false}
  enablePriority={false}
/>
```

#### **RKSV Optimized**
```typescript
<SimpleTodo
  storageKey="rksv_compliance_todos"
  maxItems={100}
  enableCategories={true}
  enablePriority={true}
/>
```

#### **Team Usage**
```typescript
<SimpleTodo
  storageKey="team_daily_tasks"
  maxItems={75}
  enableCategories={true}
  enablePriority={true}
/>
```

## 💾 Data Storage

### **AsyncStorage Keys**
- **Default**: `simple_todo_items`
- **Custom**: Istediğiniz key ile (`storageKey` prop)
- **Format**: JSON array

### **Data Structure**
```json
[
  {
    "id": "todo_1704902400000_abc123def",
    "text": "TSE cihaz durumu kontrol et",
    "completed": false,
    "createdAt": "2025-01-10T08:00:00.000Z",
    "priority": "high",
    "category": "tse"
  }
]
```

### **Storage Management**
```typescript
// Manual storage operations
import AsyncStorage from '@react-native-async-storage/async-storage';

// Tüm todo'ları al
const todos = await AsyncStorage.getItem('simple_todo_items');

// Todo'ları temizle
await AsyncStorage.removeItem('simple_todo_items');

// Backup al
const backup = await AsyncStorage.getItem('simple_todo_items');
await AsyncStorage.setItem('simple_todo_backup', backup);
```

## 🎨 UI Customization

### **Renk Şeması**
```typescript
// Kategori renkleri
const categoryColors = {
  rksv: '#FF5722',     // Kırmızı - Yasal gereksinimler
  tse: '#FF9800',      // Turuncu - Teknik güvenlik
  allgemein: '#2196F3' // Mavi - Genel görevler
};

// Öncelik renkleri
const priorityColors = {
  high: '#F44336',     // Kırmızı - Kritik
  medium: '#666666',   // Gri - Normal
  low: '#4CAF50'       // Yeşil - Düşük
};
```

### **Icon Mapping**
```typescript
// Öncelik ikonları
const priorityIcons = {
  high: 'chevron-up',     // ↑
  medium: 'remove',       // —
  low: 'chevron-down'     // ↓
};

// State ikonları
const stateIcons = {
  completed: 'checkbox',        // ☑️
  pending: 'square-outline'     // ☐
};
```

## 📱 Mobile UX Features

### **Gesture Support**
- ✅ **Tap to toggle** - Todo completion
- 🗑️ **Tap delete icon** - Remove todo
- ⌨️ **Return key** - Submit new todo
- 📝 **Multiline input** - Long descriptions

### **Responsive Design**
- 📱 **Mobile optimized** - Touch-friendly interface
- 🎨 **Material Design** - Modern UI patterns
- 🌍 **i18n ready** - Multi-language support
- ♿ **Accessibility** - Screen reader compatible

### **Performance Features**
- ⚡ **FlatList** - Efficient list rendering
- 💾 **AsyncStorage** - Local persistence
- 🔄 **Real-time updates** - Instant state changes
- 📊 **Statistics** - Live todo counts

## 🔍 Debugging & Troubleshooting

### **Common Issues**

#### **1. Todo'lar kayboluyor**
```typescript
// Storage key kontrolü
console.log('Storage key:', storageKey);

// Storage içeriği kontrolü
AsyncStorage.getItem(storageKey).then(data => {
  console.log('Stored todos:', data);
});
```

#### **2. Kategoriler görünmüyor**
```typescript
// Kategori prop kontrolü
<SimpleTodo enableCategories={true} />
```

#### **3. Performans sorunları**
```typescript
// Max items sınırla
<SimpleTodo maxItems={30} />

// Gereksiz özellikleri kapat
<SimpleTodo 
  enableCategories={false}
  enablePriority={false}
/>
```

### **Debug Commands**
```typescript
// Console debug
console.log('SimpleTodo Debug:', {
  totalTodos: todos.length,
  completedTodos: todos.filter(t => t.completed).length,
  categories: [...new Set(todos.map(t => t.category))],
  priorities: [...new Set(todos.map(t => t.priority))]
});
```

## 📊 Analytics & Metrics

### **Built-in Statistics**
- 📈 **Total todos** - Toplam todo sayısı
- ✅ **Completed** - Tamamlanan todo sayısı  
- ⏳ **Pending** - Bekleyen todo sayısı
- 📊 **Completion rate** - Tamamlanma oranı

### **Category Breakdown**
```typescript
// Kategori bazlı istatistikler
const stats = {
  rksv: todos.filter(t => t.category === 'rksv').length,
  tse: todos.filter(t => t.category === 'tse').length,
  allgemein: todos.filter(t => t.category === 'allgemein').length
};
```

### **Priority Analysis**
```typescript
// Öncelik bazlı analiz
const priorityStats = {
  high: todos.filter(t => t.priority === 'high').length,
  medium: todos.filter(t => t.priority === 'medium').length,
  low: todos.filter(t => t.priority === 'low').length
};
```

## 🔗 Integration with Task-Master

### **Parallel Usage**
```typescript
// Aynı anda her ikisini de kullanabilirsiniz
import useTaskMaster from '../hooks/useTaskMaster';
import SimpleTodo from '../components/SimpleTodo';

const MyScreen = () => {
  const taskMaster = useTaskMaster(); // Gelişmiş features
  
  return (
    <View>
      {/* Büyük projeler için */}
      <TaskMasterDashboard />
      
      {/* Hızlı notlar için */}
      <SimpleTodo storageKey="quick_notes" />
    </View>
  );
};
```

### **Data Migration**
```typescript
// SimpleTodo'dan TaskMaster'a migrate
const migrateToTaskMaster = async () => {
  const simpleTodos = await AsyncStorage.getItem('simple_todo_items');
  const todos = JSON.parse(simpleTodos || '[]');
  
  for (const todo of todos) {
    await createTask({
      title: todo.text,
      category: mapCategory(todo.category),
      priority: mapPriority(todo.priority),
      status: todo.completed ? TaskStatus.COMPLETED : TaskStatus.PENDING
    });
  }
};
```

## 🎯 Best Practices

### **📋 Todo Writing**
- ✅ **Kısa ve net** - Maksimum 50 karakter
- 🎯 **Actionable** - Eylem odaklı tanımlar
- 📅 **Time-bound** - Zaman sınırı belirtin
- 🏷️ **Kategorize** - Doğru kategori seçin

### **🔧 Technical**
- 💾 **Unique storage keys** - Her component için farklı key
- 📊 **Reasonable limits** - maxItems ile sınır koyun
- 🧹 **Regular cleanup** - Tamamlanan todo'ları temizleyin
- 🔄 **Consistent categories** - Kategori kullanımında tutarlı olun

### **📱 UX/UI**
- 🎨 **Visual consistency** - Renk ve ikon tutarlılığı
- ⚡ **Quick actions** - Hızlı erişim sağlayın
- 📝 **Clear feedback** - Alert ve mesajlar kullanın
- ♿ **Accessibility** - Erişilebilirlik önceliği

## 🚀 Sonuç

**SimpleTodo** ile:

✅ **Hızlı ve basit** todo yönetimi  
✅ **RKSV kategorileri** ile organize çalışma  
✅ **Local storage** ile offline kullanım  
✅ **Mobile-first** tasarım  
✅ **Zero-config** kolay başlangıç  

**Perfect for**: Günlük notlar, hızlı görevler, RKSV kontrol listeleri, team coordination!

---

**Başarılı kullanımlar!** 🎉
