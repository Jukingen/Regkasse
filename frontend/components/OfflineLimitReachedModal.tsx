import React from 'react';
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';

export type OfflineLimitReachedModalProps = {
  visible: boolean;
  title: string;
  body: string;
  retryLabel: string;
  cancelLabel: string;
  onRetryNow?: () => void;
  onCancel?: () => void;
};

export function OfflineLimitReachedModal({
  visible,
  title,
  body,
  retryLabel,
  cancelLabel,
  onRetryNow,
  onCancel,
}: OfflineLimitReachedModalProps) {
  if (!visible) {
    return null;
  }

  return (
    <Modal visible transparent animationType="fade" onRequestClose={onCancel}>
      <View style={styles.backdrop} testID="offline-limit-reached-modal">
        <View style={styles.card} accessibilityRole="alert" accessibilityLabel={title}>
          <Text style={styles.title}>{title}</Text>
          <Text style={styles.body}>{body}</Text>
          <View style={styles.actions}>
            {onCancel ? (
              <Pressable
                style={styles.cancelBtn}
                onPress={onCancel}
                accessibilityRole="button"
                accessibilityLabel={cancelLabel}
                testID="offline-limit-cancel"
              >
                <Text style={styles.cancelText}>{cancelLabel}</Text>
              </Pressable>
            ) : null}
            {onRetryNow ? (
              <Pressable
                style={styles.retryBtn}
                onPress={onRetryNow}
                accessibilityRole="button"
                accessibilityLabel={retryLabel}
                testID="offline-limit-retry"
              >
                <Text style={styles.retryText}>{retryLabel}</Text>
              </Pressable>
            ) : null}
          </View>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(17, 24, 39, 0.45)',
    justifyContent: 'center',
    padding: 24,
  },
  card: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 20,
  },
  title: {
    fontSize: 18,
    fontWeight: '700',
    color: '#111',
    marginBottom: 8,
  },
  body: {
    fontSize: 15,
    color: '#374151',
    lineHeight: 22,
    marginBottom: 16,
  },
  actions: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    gap: 8,
  },
  cancelBtn: {
    paddingHorizontal: 14,
    paddingVertical: 8,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#d1d5db',
  },
  cancelText: {
    color: '#374151',
    fontWeight: '600',
  },
  retryBtn: {
    backgroundColor: '#2563eb',
    paddingHorizontal: 14,
    paddingVertical: 8,
    borderRadius: 8,
  },
  retryText: {
    color: '#fff',
    fontWeight: '600',
  },
});
