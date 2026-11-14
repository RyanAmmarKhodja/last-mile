import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '@/shared/context/AuthContext';
import { AppRouter } from '@/shared/router';
import { queryClient } from '@/shared/query/queryClient';
import '@/shared/styles/globals.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <AppRouter />
      </AuthProvider>
    </QueryClientProvider>
  </StrictMode>,
);
