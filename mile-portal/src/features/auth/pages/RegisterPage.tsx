import { Link, useNavigate } from 'react-router-dom';
import { Card } from '@/shared/components';
import { RegisterForm } from '../components/RegisterForm';
import '../styles/auth.css';

export function RegisterPage() {
  const navigate = useNavigate();
  return (
    <Card className="narrow auth-card" title="Create an account" subtitle="Register as a client or a driver.">
      <div className="stack">
        <RegisterForm onRegistered={(user) => navigate('/login', { state: { registeredEmail: user.email } })} />
        <p className="auth-switch">
          Already registered? <Link to="/login">Sign in</Link>
        </p>
      </div>
    </Card>
  );
}
