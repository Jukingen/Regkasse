import React from 'react';
import { View, StyleSheet } from 'react-native';
import ProductList from '../components/ProductList';
import { SafeAreaView } from 'react-native-safe-area-context';

export default function ProductsScreen() {
    return (
        <SafeAreaView style={styles.container}>
            <ProductList />
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: '#fff',
    },
}); 