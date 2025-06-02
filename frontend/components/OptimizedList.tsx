import React, { memo, useCallback } from 'react';
import {
  FlatList,
  FlatListProps,
  ListRenderItem,
  View,
  StyleSheet,
  ActivityIndicator,
} from 'react-native';
import { useTheme } from '../contexts/ThemeContext';

interface OptimizedListProps<T> extends Omit<FlatListProps<T>, 'renderItem'> {
  data: T[];
  renderItem: ListRenderItem<T>;
  loading?: boolean;
  onEndReached?: () => void;
  onRefresh?: () => void;
  ListEmptyComponent?: React.ComponentType<any> | React.ReactElement | null;
  keyExtractor?: (item: T, index: number) => string;
}

const MemoizedItem = memo(
  <T extends { id: string }>({ item, renderItem }: { item: T; renderItem: ListRenderItem<T> }) => {
    return renderItem({ item, index: 0, separators: { highlight: () => {}, unhighlight: () => {}, updateProps: () => {} } });
  },
  (prev, next) => prev.item.id === next.item.id
);

export function OptimizedList<T extends { id: string }>({
  data,
  renderItem,
  loading,
  onEndReached,
  onRefresh,
  ListEmptyComponent,
  keyExtractor = (item) => item.id,
  ...props
}: OptimizedListProps<T>) {
  const { theme } = useTheme();
  const styles = createStyles(theme);

  const renderItemCallback = useCallback<ListRenderItem<T>>(
    (info) => <MemoizedItem item={info.item} renderItem={renderItem} />,
    [renderItem]
  );

  const handleEndReached = useCallback(() => {
    if (!loading && onEndReached) {
      onEndReached();
    }
  }, [loading, onEndReached]);

  const ListFooterComponent = useCallback(() => {
    if (!loading) return null;
    return (
      <View style={styles.footer}>
        <ActivityIndicator color={theme.primary} />
      </View>
    );
  }, [loading, theme.primary]);

  return (
    <FlatList
      data={data}
      renderItem={renderItemCallback}
      keyExtractor={keyExtractor}
      onEndReached={handleEndReached}
      onEndReachedThreshold={0.5}
      onRefresh={onRefresh}
      refreshing={loading}
      ListEmptyComponent={ListEmptyComponent}
      ListFooterComponent={ListFooterComponent}
      removeClippedSubviews={true}
      maxToRenderPerBatch={10}
      windowSize={5}
      initialNumToRender={10}
      {...props}
    />
  );
}

const createStyles = (theme: any) => StyleSheet.create({
  footer: {
    padding: 16,
    alignItems: 'center',
  },
}); 