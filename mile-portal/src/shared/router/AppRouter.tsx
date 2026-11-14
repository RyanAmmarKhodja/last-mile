import { lazy, Suspense } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { FullPageSpinner } from '@/shared/components';
import { AppLayout, AuthLayout } from '@/shared/layout';
import { NotFoundPage } from '@/shared/pages/NotFoundPage';
import { LoginPage, RegisterPage, SessionPage, AccountSettingsPage, AccountProfilePage, ForbiddenPage } from '@/features/auth';
import { UsersPage } from '@/features/admin-portal';
import { ShipmentsPage, CreateShipmentPage } from '@/features/shipments';
import { DriversPage, VehiclesPage } from '@/features/fleet';
import { PublicLayout, TrackLookupPage } from '@/features/public-tracking';
import { ProtectedRoute, PublicOnlyRoute } from './ProtectedRoute';

// Pages with a map are split out, so MapLibre (~800 kB) only loads when a map is shown.
const HomePage = lazy(() => import('@/features/dashboard').then((m) => ({ default: m.HomePage })));
const FleetMapPage = lazy(() => import('@/features/tracking/pages/FleetMapPage').then((m) => ({ default: m.FleetMapPage })));
const ShipmentDetailPage = lazy(() => import('@/features/shipments/pages/ShipmentDetailPage').then((m) => ({ default: m.ShipmentDetailPage })));
const PublicTrackingPage = lazy(() => import('@/features/public-tracking/pages/PublicTrackingPage').then((m) => ({ default: m.PublicTrackingPage })));

export function AppRouter() {
  return (
    <BrowserRouter>
      <Suspense fallback={<FullPageSpinner />}>
        <Routes>
          <Route path="/" element={<Navigate to="/home" replace />} />

          {/* FR-3: no account needed, signed in or not. */}
          <Route element={<PublicLayout />}>
            <Route path="/track" element={<TrackLookupPage />} />
            <Route path="/track/:trackingNumber" element={<PublicTrackingPage />} />
          </Route>

          <Route element={<PublicOnlyRoute />}>
            <Route element={<AuthLayout />}>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/register" element={<RegisterPage />} />
            </Route>
          </Route>

          <Route element={<ProtectedRoute />}>
            <Route element={<AppLayout />}>
              <Route path="/home" element={<HomePage />} />
              <Route path="/shipments" element={<ShipmentsPage />} />
              <Route path="/shipments/:id" element={<ShipmentDetailPage />} />
              <Route path="/account" element={<AccountProfilePage />} />
              <Route path="/account/security" element={<AccountSettingsPage />} />
              <Route path="/account/session" element={<SessionPage />} />
              <Route path="/forbidden" element={<ForbiddenPage />} />

              <Route element={<ProtectedRoute roles={['CLIENT', 'ADMIN']} />}>
                <Route path="/shipments/new" element={<CreateShipmentPage />} />
              </Route>

              <Route element={<ProtectedRoute roles={['ADMIN']} />}>
                <Route path="/map" element={<FleetMapPage />} />
                <Route path="/fleet" element={<Navigate to="/fleet/drivers" replace />} />
                <Route path="/fleet/drivers" element={<DriversPage />} />
                <Route path="/fleet/vehicles" element={<VehiclesPage />} />
                <Route path="/admin/users" element={<UsersPage />} />
              </Route>
            </Route>
          </Route>

          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </Suspense>
    </BrowserRouter>
  );
}
