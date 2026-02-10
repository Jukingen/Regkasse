import React from 'react';
import { Box, Grid, Paper, Typography, AppBar, Toolbar, IconButton } from '@mui/material';
import { 
  AccountCircle as UserIcon, 
  Language as LanguageIcon,
  ExitToApp as LogoutIcon,
  Brightness4 as DarkIcon,
  Brightness7 as LightIcon
} from '@mui/icons-material';
import { ProductProvider } from '../contexts/ProductContext';
import { CartProvider } from '../contexts/CartContext';
import { ThemeProvider, useTheme } from '../contexts/ThemeContext';
// import { UserProvider } from '../contexts/UserContext';
// Bileşenler: CategoryBar, ProductGrid, Cart, QuickActions, SearchBox
import CategoryBar from '../components/CategoryBar';
import SearchBox from '../components/SearchBox';
import ProductGrid from '../components/ProductGrid';
import Cart from '../components/Cart';
import QuickActions from '../components/QuickActions';

const CashRegisterContent: React.FC = () => {
  const { isDarkMode, toggleTheme } = useTheme();

  return (
    <Box sx={{ height: '100vh', display: 'flex', flexDirection: 'column', bgcolor: 'background.default' }}>
      {/* Üst Bar */}
      <AppBar position="static" elevation={1}>
        <Toolbar>
          <Typography variant="h6" component="div" sx={{ flexGrow: 1, fontWeight: 700 }}>
            🛒 Kasa Sistemi
          </Typography>
          <IconButton color="inherit" size="large">
            <LanguageIcon />
          </IconButton>
          <IconButton color="inherit" size="large" onClick={toggleTheme}>
            {isDarkMode ? <LightIcon /> : <DarkIcon />}
          </IconButton>
          <IconButton color="inherit" size="large">
            <UserIcon />
          </IconButton>
          <IconButton color="inherit" size="large">
            <LogoutIcon />
          </IconButton>
        </Toolbar>
      </AppBar>

      {/* Ana İçerik */}
      <Box sx={{ flex: 1, p: { xs: 1, sm: 2 }, overflow: 'hidden' }}>
        {/* Kategori Barı */}
        <CategoryBar />

        {/* Arama Kutusu */}
        <SearchBox />

        {/* Ürünler ve Sepet Grid */}
        <Grid container spacing={2} sx={{ height: 'calc(100vh - 200px)', overflow: 'hidden' }}>
          {/* Ürün Grid'i - Mobilde tam genişlik, desktop'ta 8/12 */}
          <Grid item xs={12} lg={8} sx={{ height: '100%' }}>
            <Paper 
              sx={{ 
                height: '100%', 
                bgcolor: 'background.paper',
                overflow: 'auto',
                '&::-webkit-scrollbar': {
                  width: 6,
                },
                '&::-webkit-scrollbar-track': {
                  bgcolor: 'grey.100',
                },
                '&::-webkit-scrollbar-thumb': {
                  bgcolor: 'grey.400',
                  borderRadius: 3,
                }
              }}
            >
              <Box sx={{ p: 2 }}>
                <ProductGrid />
              </Box>
            </Paper>
          </Grid>

          {/* Sepet ve Hızlı İşlemler - Mobilde tam genişlik, desktop'ta 4/12 */}
          <Grid item xs={12} lg={4} sx={{ height: '100%' }}>
            <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column', gap: 2 }}>
              {/* Sepet */}
              <Box sx={{ flex: 1 }}>
                <Cart />
              </Box>
              
              {/* Hızlı İşlemler */}
              <Paper sx={{ p: 2 }}>
                <QuickActions />
              </Paper>
            </Box>
          </Grid>
        </Grid>
      </Box>
    </Box>
  );
};

const CashRegisterScreen: React.FC = () => {
  return (
    <ThemeProvider>
      <ProductProvider>
        <CartProvider>
          <CashRegisterContent />
        </CartProvider>
      </ProductProvider>
    </ThemeProvider>
  );
};

export default CashRegisterScreen; 