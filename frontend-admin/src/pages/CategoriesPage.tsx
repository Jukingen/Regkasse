import React, { useState, useEffect } from 'react';
import {
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
  IconButton,
  Tooltip,
  Alert,
  Snackbar,
  Grid,
  Paper,
  Switch,
  FormControlLabel,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
} from '@mui/material';
import {
  Add as AddIcon,
  Edit as EditIcon,
  Delete as DeleteIcon,
  Visibility as ViewIcon,
  Category as CategoryIcon,
  ColorLens as ColorIcon,
  Image as ImageIcon,
} from '@mui/icons-material';
import { categoryService, Category, CreateCategoryRequest, UpdateCategoryRequest } from '../services/api/categoryService';

interface CategoryStats {
  total: number;
  active: number;
  inactive: number;
  withProducts: number;
  empty: number;
}

export default function CategoriesPage() {
  const [categories, setCategories] = useState<Category[]>([]);
  const [filteredCategories, setFilteredCategories] = useState<Category[]>([]);
  const [loading, setLoading] = useState(false);
  const [stats, setStats] = useState<CategoryStats | null>(null);
  
  // Dialog states
  const [dialogOpen, setDialogOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [viewDialogOpen, setViewDialogOpen] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<Category | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  
  // Form states
  const [formData, setFormData] = useState<CreateCategoryRequest>({
    name: '',
    description: '',
    color: '#1976d2',
    icon: 'category',
    sortOrder: 0,
  });
  
  // Filter states
  const [statusFilter, setStatusFilter] = useState<string>('All');
  
  // Notification states
  const [snackbar, setSnackbar] = useState<{
    open: boolean;
    message: string;
    severity: 'success' | 'error' | 'info';
  }>({
    open: false,
    message: '',
    severity: 'info',
  });

  useEffect(() => {
    loadCategories();
  }, []);

  useEffect(() => {
    filterCategories();
  }, [categories, statusFilter]);

  const loadCategories = async () => {
    try {
      setLoading(true);
      const data = await categoryService.getCategories();
      setCategories(data);
      
      // Load stats
      const statsData = await categoryService.getCategoryStats();
      setStats(statsData);
    } catch (error) {
      showNotification('Failed to load categories', 'error');
    } finally {
      setLoading(false);
    }
  };

  const filterCategories = () => {
    let filtered = categories;

    // Status filter
    if (statusFilter !== 'All') {
      const isActive = statusFilter === 'Active';
      filtered = filtered.filter(c => c.isActive === isActive);
    }

    setFilteredCategories(filtered);
  };

  const handleAddCategory = () => {
    setIsEditing(false);
    setFormData({
      name: '',
      description: '',
      color: '#1976d2',
      icon: 'category',
      sortOrder: categories.length,
    });
    setDialogOpen(true);
  };

  const handleEditCategory = (category: Category) => {
    setIsEditing(true);
    setSelectedCategory(category);
    setFormData({
      name: category.name,
      description: category.description || '',
      color: category.color || '#1976d2',
      icon: category.icon || 'category',
      sortOrder: category.sortOrder,
    });
    setDialogOpen(true);
  };

  const handleViewCategory = (category: Category) => {
    setSelectedCategory(category);
    setViewDialogOpen(true);
  };

  const handleDeleteCategory = (category: Category) => {
    setSelectedCategory(category);
    setDeleteDialogOpen(true);
  };

  const handleSaveCategory = async () => {
    try {
      if (isEditing && selectedCategory) {
        await categoryService.updateCategory(selectedCategory.id, formData);
        showNotification('Category updated successfully', 'success');
      } else {
        await categoryService.createCategory(formData);
        showNotification('Category created successfully', 'success');
      }
      
      setDialogOpen(false);
      loadCategories();
    } catch (error: any) {
      showNotification(error.response?.data?.error || 'Failed to save category', 'error');
    }
  };

  const handleConfirmDelete = async () => {
    if (!selectedCategory) return;

    try {
      await categoryService.deleteCategory(selectedCategory.id);
      showNotification('Category deleted successfully', 'success');
      setDeleteDialogOpen(false);
      loadCategories();
    } catch (error: any) {
      showNotification(error.response?.data?.error || 'Failed to delete category', 'error');
    }
  };

  const handleStatusToggle = async (category: Category) => {
    try {
      await categoryService.updateCategoryStatus(category.id, !category.isActive);
      showNotification('Category status updated successfully', 'success');
      loadCategories();
    } catch (error) {
      showNotification('Failed to update category status', 'error');
    }
  };

  const showNotification = (message: string, severity: 'success' | 'error' | 'info') => {
    setSnackbar({
      open: true,
      message,
      severity,
    });
  };

  const iconOptions = [
    'category', 'shopping_cart', 'local_grocery_store', 'restaurant', 'coffee', 
    'local_bar', 'local_pizza', 'cake', 'local_dining', 'fastfood',
    'liquor', 'smoke_free', 'local_pharmacy', 'local_hospital', 'spa',
    'fitness_center', 'sports_soccer', 'sports_basketball', 'sports_tennis',
    'sports_esports', 'movie', 'music_note', 'book', 'school', 'work',
    'home', 'apartment', 'hotel', 'car_rental', 'flight', 'train',
    'directions_bus', 'directions_car', 'motorcycle', 'bike_scooter'
  ];

  const colorOptions = [
    '#1976d2', '#dc004e', '#2e7d32', '#ed6c02', '#9c27b0',
    '#d32f2f', '#388e3c', '#f57c00', '#7b1fa2', '#c2185b',
    '#689f38', '#ff8f00', '#512da8', '#ad1457', '#558b2f',
    '#ef6c00', '#4527a0', '#880e4f', '#33691e', '#e65100'
  ];

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h4" gutterBottom>
        Category Management
      </Typography>

      {/* Stats Cards */}
      {stats && (
        <Grid container spacing={3} sx={{ mb: 3 }}>
          <Grid item xs={12} sm={6} md={2.4}>
            <Card>
              <CardContent>
                <Typography color="textSecondary" gutterBottom>
                  Total Categories
                </Typography>
                <Typography variant="h4">{stats.total}</Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} sm={6} md={2.4}>
            <Card>
              <CardContent>
                <Typography color="textSecondary" gutterBottom>
                  Active Categories
                </Typography>
                <Typography variant="h4" color="success.main">{stats.active}</Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} sm={6} md={2.4}>
            <Card>
              <CardContent>
                <Typography color="textSecondary" gutterBottom>
                  With Products
                </Typography>
                <Typography variant="h4" color="primary.main">{stats.withProducts}</Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} sm={6} md={2.4}>
            <Card>
              <CardContent>
                <Typography color="textSecondary" gutterBottom>
                  Empty Categories
                </Typography>
                <Typography variant="h4" color="warning.main">{stats.empty}</Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} sm={6} md={2.4}>
            <Card>
              <CardContent>
                <Typography color="textSecondary" gutterBottom>
                  Inactive
                </Typography>
                <Typography variant="h4" color="error.main">{stats.inactive}</Typography>
              </CardContent>
            </Card>
          </Grid>
        </Grid>
      )}

      {/* Filters and Actions */}
      <Paper sx={{ p: 2, mb: 3 }}>
        <Grid container spacing={2} alignItems="center">
          <Grid item xs={12} sm={4}>
            <FormControlLabel
              control={
                <Switch
                  checked={statusFilter === 'Active'}
                  onChange={(e) => setStatusFilter(e.target.checked ? 'Active' : 'All')}
                />
              }
              label="Show Active Only"
            />
          </Grid>
          <Grid item xs={12} sm={8}>
            <Button
              variant="contained"
              startIcon={<AddIcon />}
              onClick={handleAddCategory}
              sx={{ float: 'right' }}
            >
              Add Category
            </Button>
          </Grid>
        </Grid>
      </Paper>

      {/* Categories Table */}
      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Category</TableCell>
              <TableCell>Description</TableCell>
              <TableCell>Products</TableCell>
              <TableCell>Sort Order</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {filteredCategories.map((category) => (
              <TableRow key={category.id}>
                <TableCell>
                  <Box sx={{ display: 'flex', alignItems: 'center' }}>
                    <Box
                      sx={{
                        width: 40,
                        height: 40,
                        borderRadius: '50%',
                        backgroundColor: category.color || '#1976d2',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        mr: 2,
                      }}
                    >
                      <CategoryIcon sx={{ color: 'white' }} />
                    </Box>
                    <Box>
                      <Typography variant="subtitle2">{category.name}</Typography>
                      <Typography variant="body2" color="textSecondary">
                        Icon: {category.icon}
                      </Typography>
                    </Box>
                  </Box>
                </TableCell>
                <TableCell>
                  <Typography variant="body2">
                    {category.description || 'No description'}
                  </Typography>
                </TableCell>
                <TableCell>
                  <Chip
                    label={`${category.productCount || 0} products`}
                    color={category.productCount && category.productCount > 0 ? 'success' : 'default'}
                    size="small"
                  />
                </TableCell>
                <TableCell>
                  <Typography variant="body2">{category.sortOrder}</Typography>
                </TableCell>
                <TableCell>
                  <Chip
                    label={category.isActive ? 'Active' : 'Inactive'}
                    color={category.isActive ? 'success' : 'default'}
                    size="small"
                    onClick={() => handleStatusToggle(category)}
                    sx={{ cursor: 'pointer' }}
                  />
                </TableCell>
                <TableCell>
                  <Tooltip title="View Details">
                    <IconButton onClick={() => handleViewCategory(category)}>
                      <ViewIcon />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Edit Category">
                    <IconButton onClick={() => handleEditCategory(category)}>
                      <EditIcon />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Delete Category">
                    <IconButton 
                      onClick={() => handleDeleteCategory(category)}
                      disabled={(category.productCount || 0) > 0}
                    >
                      <DeleteIcon />
                    </IconButton>
                  </Tooltip>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Add/Edit Category Dialog */}
      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>
          {isEditing ? 'Edit Category' : 'Add New Category'}
        </DialogTitle>
        <DialogContent>
          <Grid container spacing={2} sx={{ mt: 1 }}>
            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                label="Category Name"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                required
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                label="Sort Order"
                type="number"
                value={formData.sortOrder}
                onChange={(e) => setFormData({ ...formData, sortOrder: Number(e.target.value) })}
                inputProps={{ min: 0 }}
                required
              />
            </Grid>
            <Grid item xs={12}>
              <TextField
                fullWidth
                label="Description"
                multiline
                rows={3}
                value={formData.description}
                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <FormControl fullWidth>
                <InputLabel>Icon</InputLabel>
                <Select
                  value={formData.icon}
                  onChange={(e) => setFormData({ ...formData, icon: e.target.value })}
                  label="Icon"
                >
                  {iconOptions.map((icon) => (
                    <MenuItem key={icon} value={icon}>
                      <Box sx={{ display: 'flex', alignItems: 'center' }}>
                        <CategoryIcon sx={{ mr: 1 }} />
                        {icon}
                      </Box>
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Grid>
            <Grid item xs={12} sm={6}>
              <FormControl fullWidth>
                <InputLabel>Color</InputLabel>
                <Select
                  value={formData.color}
                  onChange={(e) => setFormData({ ...formData, color: e.target.value })}
                  label="Color"
                >
                  {colorOptions.map((color) => (
                    <MenuItem key={color} value={color}>
                      <Box sx={{ display: 'flex', alignItems: 'center' }}>
                        <Box
                          sx={{
                            width: 20,
                            height: 20,
                            borderRadius: '50%',
                            backgroundColor: color,
                            mr: 1,
                            border: '1px solid #ccc',
                          }}
                        />
                        {color}
                      </Box>
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Grid>
          </Grid>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>Cancel</Button>
          <Button onClick={handleSaveCategory} variant="contained">
            {isEditing ? 'Update' : 'Create'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* View Category Dialog */}
      <Dialog open={viewDialogOpen} onClose={() => setViewDialogOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>Category Details</DialogTitle>
        <DialogContent>
          {selectedCategory && (
            <Grid container spacing={2} sx={{ mt: 1 }}>
              <Grid item xs={12} sm={6}>
                <Typography variant="subtitle2" color="textSecondary">Name</Typography>
                <Typography variant="body1">{selectedCategory.name}</Typography>
              </Grid>
              <Grid item xs={12} sm={6}>
                <Typography variant="subtitle2" color="textSecondary">Sort Order</Typography>
                <Typography variant="body1">{selectedCategory.sortOrder}</Typography>
              </Grid>
              <Grid item xs={12}>
                <Typography variant="subtitle2" color="textSecondary">Description</Typography>
                <Typography variant="body1">
                  {selectedCategory.description || 'No description'}
                </Typography>
              </Grid>
              <Grid item xs={12} sm={6}>
                <Typography variant="subtitle2" color="textSecondary">Icon</Typography>
                <Box sx={{ display: 'flex', alignItems: 'center' }}>
                  <CategoryIcon sx={{ mr: 1 }} />
                  <Typography variant="body1">{selectedCategory.icon}</Typography>
                </Box>
              </Grid>
              <Grid item xs={12} sm={6}>
                <Typography variant="subtitle2" color="textSecondary">Color</Typography>
                <Box sx={{ display: 'flex', alignItems: 'center' }}>
                  <Box
                    sx={{
                      width: 20,
                      height: 20,
                      borderRadius: '50%',
                      backgroundColor: selectedCategory.color || '#1976d2',
                      mr: 1,
                      border: '1px solid #ccc',
                    }}
                  />
                  <Typography variant="body1">{selectedCategory.color}</Typography>
                </Box>
              </Grid>
              <Grid item xs={12} sm={6}>
                <Typography variant="subtitle2" color="textSecondary">Status</Typography>
                <Chip
                  label={selectedCategory.isActive ? 'Active' : 'Inactive'}
                  color={selectedCategory.isActive ? 'success' : 'default'}
                />
              </Grid>
              <Grid item xs={12} sm={6}>
                <Typography variant="subtitle2" color="textSecondary">Products</Typography>
                <Chip
                  label={`${selectedCategory.productCount || 0} products`}
                  color={selectedCategory.productCount && selectedCategory.productCount > 0 ? 'success' : 'default'}
                />
              </Grid>
              <Grid item xs={12} sm={6}>
                <Typography variant="subtitle2" color="textSecondary">Created</Typography>
                <Typography variant="body1">
                  {new Date(selectedCategory.createdAt).toLocaleDateString()}
                </Typography>
              </Grid>
              <Grid item xs={12} sm={6}>
                <Typography variant="subtitle2" color="textSecondary">Last Updated</Typography>
                <Typography variant="body1">
                  {new Date(selectedCategory.updatedAt).toLocaleDateString()}
                </Typography>
              </Grid>
            </Grid>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setViewDialogOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>

      {/* Delete Confirmation Dialog */}
      <Dialog open={deleteDialogOpen} onClose={() => setDeleteDialogOpen(false)}>
        <DialogTitle>Delete Category</DialogTitle>
        <DialogContent>
          <Typography>
            Are you sure you want to delete "{selectedCategory?.name}"? 
            {selectedCategory && (selectedCategory.productCount || 0) > 0 && (
              <Alert severity="warning" sx={{ mt: 2 }}>
                This category has {selectedCategory.productCount} products. 
                You cannot delete a category that contains products.
              </Alert>
            )}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteDialogOpen(false)}>Cancel</Button>
          <Button 
            onClick={handleConfirmDelete} 
            color="error" 
            variant="contained"
            disabled={selectedCategory && (selectedCategory.productCount || 0) > 0}
          >
            Delete
          </Button>
        </DialogActions>
      </Dialog>

      {/* Snackbar for notifications */}
      <Snackbar
        open={snackbar.open}
        autoHideDuration={6000}
        onClose={() => setSnackbar({ ...snackbar, open: false })}
      >
        <Alert
          onClose={() => setSnackbar({ ...snackbar, open: false })}
          severity={snackbar.severity}
          sx={{ width: '100%' }}
        >
          {snackbar.message}
        </Alert>
      </Snackbar>
    </Box>
  );
} 