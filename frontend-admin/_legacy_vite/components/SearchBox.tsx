import React from 'react';
import { TextField } from '@mui/material';
import { useProduct } from '../contexts/ProductContext';

const SearchBox: React.FC = () => {
  const { search, setSearch } = useProduct();
  return (
    <TextField
      fullWidth
      variant="outlined"
      placeholder="Ürün ara..."
      value={search}
      onChange={e => setSearch(e.target.value)}
      sx={{ my: 2 }}
    />
  );
};

export default SearchBox; 