import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AppLayout, AuthLayout } from '@/shared/layout';
import { NotFoundPage } from '@/shared/pages/NotFoundPage';
import { LoginPage, RegisterPage, HomePage, AccountSettingsPage, ForbiddenPage } from '@/features/auth';
import { UsersPage } from '@/features/admin-portal';
import { ProtectedRoute, PublicOnlyRoute } from './ProtectedRoute';

export function AppRouter() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Navigate to="/home" replace />} />

        <Route element={<PublicOnlyRoute />}>
          <Route element={<AuthLayout />}>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
          </Route>
        </Route>

        <Route element={<ProtectedRoute />}>
          <Route element={<AppLayout />}>
            <Route path="/home" element={<HomePage />} />
            <Route path="/account" element={<AccountSettingsPage />} />
            <Route path="/forbidden" element={<ForbiddenPage />} />

            <Route element={<ProtectedRoute roles={['ADMIN']} />}>
              <Route path="/admin/users" element={<UsersPage />} />
            </Route>
          </Route>
        </Route>

        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </BrowserRouter>
  );
}
