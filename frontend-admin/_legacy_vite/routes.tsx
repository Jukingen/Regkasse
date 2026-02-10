import { Routes, Route, Link } from 'react-router-dom';
import Dashboard from './pages/Dashboard';
import Products from './pages/Products';

const AppRoutes = () => {
    return (
        <div style={{ display: 'flex', minHeight: '100vh' }}>
            {/* Sidebar Navigation */}
            <nav style={{ width: '250px', backgroundColor: '#2c3e50', color: 'white', padding: '20px' }}>
                <h2>Admin Panel</h2>
                <ul style={{ listStyle: 'none', padding: 0, marginTop: '30px' }}>
                    <li style={{ marginBottom: '15px' }}>
                        <Link to="/" style={{ color: 'white', textDecoration: 'none' }}>🏠 Dashboard</Link>
                    </li>
                    <li style={{ marginBottom: '15px' }}>
                        <Link to="/products" style={{ color: 'white', textDecoration: 'none' }}>📦 Ürünler</Link>
                    </li>
                </ul>
            </nav>

            {/* Main Content Area */}
            <main style={{ flex: 1, padding: '40px', backgroundColor: '#f9f9f9' }}>
                <Routes>
                    <Route path="/" element={<Dashboard />} />
                    <Route path="/products" element={<Products />} />
                </Routes>
            </main>
        </div>
    );
};

export default AppRoutes;
