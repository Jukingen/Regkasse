import React from 'react';
import { View, Text, StyleSheet } from 'react-native';

interface PasswordStrengthProps {
  password: string;
}

export const PasswordStrength: React.FC<PasswordStrengthProps> = ({ password }) => {
  if (!password) return null;

  // Şifre gücünü hesapla
  const calculateStrength = (pass: string): { score: number; label: string; color: string } => {
    let score = 0;
    
    // Uzunluk kontrolü
    if (pass.length >= 8) score += 1;
    if (pass.length >= 12) score += 1;
    
    // Karakter çeşitliliği
    if (/[a-z]/.test(pass)) score += 1;
    if (/[A-Z]/.test(pass)) score += 1;
    if (/[0-9]/.test(pass)) score += 1;
    if (/[^A-Za-z0-9]/.test(pass)) score += 1;
    
    // Güç seviyesi belirleme
    if (score <= 2) {
      return { score: 1, label: 'Zayıf', color: '#FF3B30' };
    } else if (score <= 4) {
      return { score: 2, label: 'Orta', color: '#FF9500' };
    } else if (score <= 5) {
      return { score: 3, label: 'İyi', color: '#34C759' };
    } else {
      return { score: 4, label: 'Güçlü', color: '#007AFF' };
    }
  };

  const strength = calculateStrength(password);
  const strengthBars = [1, 2, 3, 4];

  return (
    <View style={styles.container}>
      <View style={styles.strengthBars}>
        {strengthBars.map((bar) => (
          <View
            key={bar}
            style={[
              styles.bar,
              bar <= strength.score && { backgroundColor: strength.color }
            ]}
          />
        ))}
      </View>
      <Text style={[styles.label, { color: strength.color }]}>
        {strength.label}
      </Text>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 4,
  },
  strengthBars: {
    flexDirection: 'row',
    gap: 4,
    marginRight: 8,
  },
  bar: {
    width: 20,
    height: 4,
    borderRadius: 2,
    backgroundColor: '#E5E5EA',
  },
  label: {
    fontSize: 12,
    fontWeight: '600',
  },
});
