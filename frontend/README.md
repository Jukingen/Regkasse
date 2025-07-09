# Cash Register App

## 📱 Cashier Application
React Native/Expo based mobile cash register application for cashiers. Built with TypeScript, offering a modern and user-friendly interface for daily cash register operations.

## 🔐 Login Credentials
**Email:** `admin@admin.com`  
**Password:** `Admin123!`

## 🛠️ Technical Details
- **Framework:** React Native/Expo
- **Language:** TypeScript
- **State Management:** React Context + Custom Hooks
- **UI Library:** React Native Paper
- **Offline Storage:** PouchDB
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
- **Offline Support:** Works without internet connection

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
- Uses PouchDB in offline mode
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
