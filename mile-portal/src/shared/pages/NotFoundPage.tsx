import { Link } from 'react-router-dom';

export function NotFoundPage() {
  return (
    <div className="center-page">
      <div className="card narrow">
        <h1>404</h1>
        <p className="muted">This page does not exist.</p>
        <Link to="/home">Back to home</Link>
      </div>
    </div>
  );
}
