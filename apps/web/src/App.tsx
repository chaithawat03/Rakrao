import { Link, Route, Routes } from 'react-router-dom';
import { productCopy } from './content';
import './styles.css';

function Home() {
  return (
    <main className="page-shell">
      <div className="brand-mark" aria-label={productCopy.brand}>
        <span className="brand-symbol" aria-hidden="true">✳</span>
        <span>{productCopy.brand}</span>
      </div>
      <section className="welcome-card" aria-labelledby="welcome-title">
        <p className="eyebrow">OUR ROOTS</p>
        <h1 id="welcome-title">{productCopy.name}</h1>
        <p className="tagline">{productCopy.tagline}</p>
        <p className="foundation-note">A place for family connections to grow.</p>
      </section>
    </main>
  );
}

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
      <Route path="/" element={<Home />} />
      <Route path="*" element={<NotFound />} />
    </Routes>
  );
}
