import React from 'react';
import { 
  Box, 
  Typography, 
  IconButton, 
  List, 
  ListItem, 
  ListItemText, 
  ListItemSecondaryAction, 
  Button,
  Divider,
  Paper,
  Alert
} from '@mui/material';
import { 
  Delete as DeleteIcon, 
  Add as AddIcon, 
  Remove as RemoveIcon,
  ShoppingCart as CartIcon
} from '@mui/icons-material';
import { useCart } from '../contexts/CartContext';

const Cart: React.FC = () => {
  const { items, removeFromCart, changeQuantity, clearCart, total } = useCart();

  const handleQuantityChange = (productId: string, currentQuantity: number, change: number) => {
    const newQuantity = currentQuantity + change;
    if (newQuantity > 0) {
      changeQuantity(productId, newQuantity);
    } else if (newQuantity === 0) {
      removeFromCart(productId);
    }
  };

  return (
    <Paper sx={{ p: 2, height: '100%', display: 'flex', flexDirection: 'column' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
        <CartIcon sx={{ mr: 1, color: 'primary.main' }} />
        <Typography variant="h6" fontWeight={700}>Sepet</Typography>
      </Box>

      {items.length === 0 ? (
        <Box sx={{ 
          flex: 1, 
          display: 'flex', 
          flexDirection: 'column', 
          alignItems: 'center', 
          justifyContent: 'center',
          textAlign: 'center'
        }}>
          <CartIcon sx={{ fontSize: 60, color: 'grey.400', mb: 2 }} />
          <Typography color="text.secondary" variant="body2">
            Sepetiniz boş
          </Typography>
          <Typography color="text.secondary" variant="caption">
            Ürün eklemek için ürün kartlarına tıklayın
          </Typography>
        </Box>
      ) : (
        <>
          <List sx={{ flex: 1, overflow: 'auto' }}>
            {items.map((item, index) => (
              <React.Fragment key={`${item.product.id}-${index}`}>
                <ListItem sx={{ px: 0, py: 1 }}>
                  <ListItemText
                    primary={
                      <Typography variant="subtitle2" fontWeight={600}>
                        {item.product.name}
                      </Typography>
                    }
                    secondary={
                      <Box>
                        <Typography variant="body2" color="text.secondary">
                          {item.product.price.toFixed(2)} € x {item.quantity}
                        </Typography>
                        <Typography variant="body2" fontWeight={600} color="primary">
                          {(item.product.price * item.quantity).toFixed(2)} €
                        </Typography>
                      </Box>
                    }
                  />
                  <ListItemSecondaryAction>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                      <IconButton 
                        size="small" 
                        onClick={() => handleQuantityChange(item.product.id, item.quantity, -1)}
                        sx={{ bgcolor: 'grey.100' }}
                      >
                        <RemoveIcon fontSize="small" />
                      </IconButton>
                      
                      <Typography variant="body2" sx={{ minWidth: 20, textAlign: 'center' }}>
                        {item.quantity}
                      </Typography>
                      
                      <IconButton 
                        size="small" 
                        onClick={() => handleQuantityChange(item.product.id, item.quantity, 1)}
                        sx={{ bgcolor: 'grey.100' }}
                      >
                        <AddIcon fontSize="small" />
                      </IconButton>
                      
                      <IconButton 
                        size="small" 
                        onClick={() => removeFromCart(item.product.id)}
                        sx={{ bgcolor: 'error.light', color: 'white', ml: 1 }}
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Box>
                  </ListItemSecondaryAction>
                </ListItem>
                {index < items.length - 1 && <Divider />}
              </React.Fragment>
            ))}
          </List>

          <Divider sx={{ my: 2 }} />
          
          <Box sx={{ mt: 'auto' }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
              <Typography variant="h6" fontWeight={700}>
                Toplam
              </Typography>
              <Typography variant="h5" fontWeight={700} color="primary">
                {total.toFixed(2)} €
              </Typography>
            </Box>
            
            <Button 
              variant="outlined" 
              color="error" 
              onClick={clearCart} 
              fullWidth
              sx={{ mb: 1 }}
            >
              Sepeti Temizle
            </Button>
          </Box>
        </>
      )}
    </Paper>
  );
};

export default Cart; 