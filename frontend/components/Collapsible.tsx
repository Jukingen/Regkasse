import { PropsWithChildren, useState } from 'react';
import { StyleSheet, TouchableOpacity, Text } from 'react-native';

import { ThemedText } from '@/components/ThemedText';
import { ThemedView } from '@/components/ThemedView';
import { Colors } from '@/constants/Colors';
import { useColorScheme } from '@/hooks/useColorScheme';

export function Collapsible({ children, title }: PropsWithChildren & { title: string }) {
  const [isOpen, setIsOpen] = useState(false);
  const theme = useColorScheme() ?? 'light';
  const chevronColor = theme === 'light' ? Colors.light.textSecondary : Colors.dark.textSecondary;

  return (
    <ThemedView>
      <TouchableOpacity
        style={styles.heading}
        onPress={() => {
          setIsOpen((value) => !value);
        }}
        activeOpacity={0.8}>
        <Text
          style={{
            color: chevronColor,
            fontSize: 16,
            transform: [{ rotate: isOpen ? '90deg' : '0deg' }],
          }}>
          ›
        </Text>

        <ThemedText type="defaultSemiBold">{title}</ThemedText>
      </TouchableOpacity>
      {isOpen && <ThemedView style={styles.content}>{children}</ThemedView>}
    </ThemedView>
  );
}

const styles = StyleSheet.create({
  heading: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  content: {
    marginTop: 6,
    marginLeft: 24,
  },
});
