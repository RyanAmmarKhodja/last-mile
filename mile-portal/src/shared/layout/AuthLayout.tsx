import { Outlet } from 'react-router-dom';

/** Centered card layout for /login and /register. */
export function AuthLayout() {
  return (
    <div className="auth-layout">
      <div className="auth-brand">
        <span className="brand-mark">LM</span>
        <div>
          <div className="brand-name">Last Mile</div>
          <div className="brand-tagline">Fleet &amp; delivery tracking portal</div>
        </div>
      </div>
      <Outlet />
    </div>
  );
}
