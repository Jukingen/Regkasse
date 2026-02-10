import React from 'react';

const Dashboard: React.FC = () => {
    return (
        <div>
            <h1>Admin Dashboard</h1>
            <p>Hoş geldiniz. Buradan genel istatistikleri ve özet verileri görebilirsiniz.</p>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '20px', marginTop: '20px' }}>
                <div style={{ padding: '20px', border: '1px solid #ccc', borderRadius: '8px' }}>
                    <h3>Toplam Satış</h3>
                    <p>€ 0.00</p>
                </div>
                <div style={{ padding: '20px', border: '1px solid #ccc', borderRadius: '8px' }}>
                    <h3>Aktif Masalar</h3>
                    <p>0</p>
                </div>
                <div style={{ padding: '20px', border: '1px solid #ccc', borderRadius: '8px' }}>
                    <h3>Bugünkü Fişler</h3>
                    <p>0</p>
                </div>
            </div>
        </div>
    );
};

export default Dashboard;
