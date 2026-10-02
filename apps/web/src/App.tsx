import { Link, Route, Routes } from 'react-router-dom';
import { productCopy } from './content';
import AuthHome from './AuthHome';
import './styles.css';

function NotFound() {
  return (
    <main className="page-shell">
      <section className="welcome-card">
        <h1>Page not found</h1>
        <Link to="/">Return to {productCopy.brand}</Link>
      </section>
    </main>
  );
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<AuthHome />} />
      <Route path="*" element={<NotFound />} />
    </Routes>
  );
}
