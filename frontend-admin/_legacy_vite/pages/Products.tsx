import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { getProducts } from '../api/products';

const Products: React.FC = () => {
    const { data: products, isLoading, error } = useQuery({
        queryKey: ['products'],
        queryFn: getProducts,
    });

    return (
        <div>
            <h1>Ürün Yönetimi</h1>
            <p>Ürünleri ekleyin, düzenleyin veya stok durumlarını kontrol edin.</p>

            {isLoading && <p>Yükleniyor...</p>}
            {error && <p>Hata oluştu: {(error as Error).message}</p>}

            <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: '20px' }}>
                <thead>
                    <tr style={{ textAlign: 'left', borderBottom: '2px solid #eee' }}>
                        <th style={{ padding: '10px' }}>ID</th>
                        <th style={{ padding: '10px' }}>Ad</th>
                        <th style={{ padding: '10px' }}>Fiyat</th>
                    </tr>
                </thead>
                <tbody>
                    {products?.map((p: any) => (
                        <tr key={p.id} style={{ borderBottom: '1px solid #eee' }}>
                            <td style={{ padding: '10px' }}>{p.id}</td>
                            <td style={{ padding: '10px' }}>{p.name}</td>
                            <td style={{ padding: '10px' }}>€ {p.price?.toFixed(2)}</td>
                        </tr>
                    ))}
                    {(!products || products.length === 0) && !isLoading && (
                        <tr>
                            <td colSpan={3} style={{ padding: '20px', textAlign: 'center' }}>Ürün bulunamadı.</td>
                        </tr>
                    )}
                </tbody>
            </table>
        </div>
    );
};

export default Products;
