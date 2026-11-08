import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '@/shared/context/AuthContext';
import { Alert, Card } from '@/shared/components';
import { LoginForm } from '../components/LoginForm';
import '../styles/auth.css';

interface LocationState {
  registeredEmail?: string;
}

export function LoginPage() {
  const { notice, clearNotice } = useAuth();
  const state = (useLocation().state ?? {}) as LocationState;

  return (
    <Card className="narrow auth-card" title="Sign in" subtitle="Use your Last Mile account.">
      <div className="stack">
        {notice && (
          <Alert variant="info" onClose={clearNotice}>
            {notice}
          </Alert>
        )}
        {state.registeredEmail && (
          <Alert variant="success">
            Account created for <strong>{state.registeredEmail}</strong>. You can sign in now.
          </Alert>
        )}
        <LoginForm initialEmail={state.registeredEmail} />
        <p className="auth-switch">
          No account yet? <Link to="/register">Create one</Link>
        </p>
      </div>
    </Card>
  );
}
