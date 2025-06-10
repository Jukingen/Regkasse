import React, { useState, useEffect } from 'react';
import {
  Box,
  Grid,
  Card,
  CardContent,
  Typography,
  CircularProgress,
  Alert,
  Chip,
  IconButton,
  Tooltip,
  LinearProgress,
} from '@mui/material';
import {
  TrendingUp,
  TrendingDown,
  ShoppingCart,
  Inventory,
  People,
  AttachMoney,
  Warning,
  Refresh,
  Download,
  Print,
} from '@mui/icons-material';
import { useTranslation } from 'react-i18next';
// Placeholder for recharts - in real implementation, install: npm install recharts
// import {
//   LineChart,
//   Line,
//   XAxis,
//   YAxis,
//   CartesianGrid,
//   Tooltip as RechartsTooltip,
//   ResponsiveContainer,
//   BarChart,
//   Bar,
//   PieChart,
//   Pie,
//   Cell,
// } from 'recharts';
import api from '../../services/api';

interface DashboardData {
  summary: {
    todaySales: number;
    thisMonthSales: number;
    lastMonthSales: number;
    salesGrowth: number;
    lowStockCount: number;
    pendingInvoices: number;
    totalCustomers: number;
    totalProducts: number;
  };
  salesChart: Array<{
    date: string;
    sales: number;
    invoices: number;
  }>;
  topProducts: Array<{
    name: string;
    revenue: number;
    quantity: number;
  }>;
  paymentMethods: Array<{
    method: string;
    amount: number;
    percentage: number;
  }>;
  recentActivity: Array<{
    id: string;
    type: string;
    description: string;
    timestamp: string;
    amount?: number;
  }>;
}

const Dashboard: React.FC = () => {
  const { t } = useTranslation();
  const [data, setData] = useState<DashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date>(new Date());

  const fetchDashboardData = async () => {
    try {
      setLoading(true);
      setError(null);

      const [summaryResponse, salesResponse, productsResponse, paymentsResponse, activityResponse] = await Promise.all([
        api.get('/api/reports/dashboard-summary'),
        api.get('/api/reports/sales-analytics?startDate=2024-01-01&endDate=2024-12-31'),
        api.get('/api/reports/top-products'),
        api.get('/api/reports/payment-methods'),
        api.get('/api/audit/logs?page=1&pageSize=10')
      ]);

      const dashboardData: DashboardData = {
        summary: summaryResponse.data,
        salesChart: salesResponse.data.dailySales || [],
        topProducts: productsResponse.data.topProducts || [],
        paymentMethods: paymentsResponse.data.paymentMethodBreakdown || [],
        recentActivity: activityResponse.data || []
      };

      setData(dashboardData);
      setLastUpdated(new Date());
    } catch (err) {
      console.error('Dashboard data fetch failed:', err);
      setError(t('dashboard.error.load_failed'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchDashboardData();
  }, []);

  const handleRefresh = () => {
    fetchDashboardData();
  };

  const handleExport = () => {
    // TODO: Implement dashboard export
    console.log('Export dashboard data');
  };

  const handlePrint = () => {
    // TODO: Implement dashboard print
    console.log('Print dashboard');
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('de-DE', {
      style: 'currency',
      currency: 'EUR'
    }).format(amount);
  };

  const formatPercentage = (value: number) => {
    return `${value >= 0 ? '+' : ''}${value.toFixed(1)}%`;
  };

  const getGrowthColor = (value: number) => {
    return value >= 0 ? 'success' : 'error';
  };

  const getGrowthIcon = (value: number) => {
    return value >= 0 ? <TrendingUp /> : <TrendingDown />;
  };

  const COLORS = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042', '#8884D8'];

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="400px">
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    return (
      <Alert severity="error" sx={{ mb: 2 }}>
        {error}
      </Alert>
    );
  }

  if (!data) {
    return (
      <Alert severity="warning">
        {t('dashboard.no_data')}
      </Alert>
    );
  }

  return (
    <Box p={3}>
      {/* Header */}
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={3}>
        <Typography variant="h4" component="h1">
          {t('dashboard.title')}
        </Typography>
        <Box>
          <Tooltip title={t('dashboard.refresh')}>
            <IconButton onClick={handleRefresh} color="primary">
              <Refresh />
            </IconButton>
          </Tooltip>
          <Tooltip title={t('dashboard.export')}>
            <IconButton onClick={handleExport} color="primary">
              <Download />
            </IconButton>
          </Tooltip>
          <Tooltip title={t('dashboard.print')}>
            <IconButton onClick={handlePrint} color="primary">
              <Print />
            </IconButton>
          </Tooltip>
        </Box>
      </Box>

      {/* Last Updated */}
      <Typography variant="caption" color="text.secondary" mb={2}>
        {t('dashboard.last_updated')}: {lastUpdated.toLocaleString()}
      </Typography>

      {/* Summary Cards */}
      <Grid container spacing={3} mb={3}>
        <Grid item xs={12} sm={6} md={3}>
          <Card>
            <CardContent>
              <Box display="flex" alignItems="center" justifyContent="space-between">
                <Box>
                  <Typography color="text.secondary" gutterBottom>
                    {t('dashboard.today_sales')}
                  </Typography>
                  <Typography variant="h4" component="div">
                    {formatCurrency(data.summary.todaySales)}
                  </Typography>
                </Box>
                <AttachMoney color="primary" sx={{ fontSize: 40 }} />
              </Box>
            </CardContent>
          </Card>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Card>
            <CardContent>
              <Box display="flex" alignItems="center" justifyContent="space-between">
                <Box>
                  <Typography color="text.secondary" gutterBottom>
                    {t('dashboard.monthly_sales')}
                  </Typography>
                  <Typography variant="h4" component="div">
                    {formatCurrency(data.summary.thisMonthSales)}
                  </Typography>
                  <Box display="flex" alignItems="center" mt={1}>
                    <Chip
                      icon={getGrowthIcon(data.summary.salesGrowth)}
                      label={formatPercentage(data.summary.salesGrowth)}
                      color={getGrowthColor(data.summary.salesGrowth) as any}
                      size="small"
                    />
                  </Box>
                </Box>
                <TrendingUp color="primary" sx={{ fontSize: 40 }} />
              </Box>
            </CardContent>
          </Card>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Card>
            <CardContent>
              <Box display="flex" alignItems="center" justifyContent="space-between">
                <Box>
                  <Typography color="text.secondary" gutterBottom>
                    {t('dashboard.low_stock')}
                  </Typography>
                  <Typography variant="h4" component="div">
                    {data.summary.lowStockCount}
                  </Typography>
                </Box>
                <Warning color="warning" sx={{ fontSize: 40 }} />
              </Box>
            </CardContent>
          </Card>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Card>
            <CardContent>
              <Box display="flex" alignItems="center" justifyContent="space-between">
                <Box>
                  <Typography color="text.secondary" gutterBottom>
                    {t('dashboard.pending_invoices')}
                  </Typography>
                  <Typography variant="h4" component="div">
                    {data.summary.pendingInvoices}
                  </Typography>
                </Box>
                <ShoppingCart color="info" sx={{ fontSize: 40 }} />
              </Box>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Charts Row */}
      <Grid container spacing={3} mb={3}>
        {/* Sales Chart */}
        <Grid item xs={12} md={8}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('dashboard.sales_trend')}
              </Typography>
              <Box height={300} display="flex" alignItems="center" justifyContent="center">
                <Typography color="text.secondary">
                  Chart component will be implemented with recharts library
                </Typography>
              </Box>
            </CardContent>
          </Card>
        </Grid>

        {/* Payment Methods */}
        <Grid item xs={12} md={4}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('dashboard.payment_methods')}
              </Typography>
              <Box height={300} display="flex" alignItems="center" justifyContent="center">
                <Typography color="text.secondary">
                  Pie chart will be implemented with recharts library
                </Typography>
              </Box>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Bottom Row */}
      <Grid container spacing={3}>
        {/* Top Products */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('dashboard.top_products')}
              </Typography>
              <Box>
                {data.topProducts.slice(0, 5).map((product, index) => (
                  <Box key={index} display="flex" justifyContent="space-between" alignItems="center" mb={2}>
                    <Box>
                      <Typography variant="body2" fontWeight="bold">
                        {product.name}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {product.quantity} {t('dashboard.units')}
                      </Typography>
                    </Box>
                    <Typography variant="body2" fontWeight="bold">
                      {formatCurrency(product.revenue)}
                    </Typography>
                  </Box>
                ))}
              </Box>
            </CardContent>
          </Card>
        </Grid>

        {/* Recent Activity */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('dashboard.recent_activity')}
              </Typography>
              <Box>
                {data.recentActivity.slice(0, 5).map((activity, index) => (
                  <Box key={index} mb={2}>
                    <Box display="flex" justifyContent="space-between" alignItems="flex-start">
                      <Box flex={1}>
                        <Typography variant="body2" fontWeight="bold">
                          {activity.type}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                          {activity.description}
                        </Typography>
                        <Typography variant="caption" display="block" color="text.secondary">
                          {new Date(activity.timestamp).toLocaleString()}
                        </Typography>
                      </Box>
                      {activity.amount && (
                        <Typography variant="body2" fontWeight="bold">
                          {formatCurrency(activity.amount)}
                        </Typography>
                      )}
                    </Box>
                  </Box>
                ))}
              </Box>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
};

export default Dashboard; 