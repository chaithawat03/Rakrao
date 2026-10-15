import { productCopy } from '../content';

interface BrandMarkProps {
  variant: 'public' | 'compact';
}

export function BrandMark({ variant }: BrandMarkProps) {
  if (variant === 'public') {
    return (
      <div className="brand-mark brand-mark--public">
        <p className="eyebrow">{productCopy.brand}</p>
        <h1>{productCopy.name}</h1>
      </div>
    );
  }

  return (
    <div
      className="brand-mark brand-mark--compact"
      aria-label={productCopy.brand}
    >
      <span className="brand-symbol" aria-hidden="true">
        ✳
      </span>
      <span>{productCopy.name}</span>
      <span>{productCopy.brand}</span>
    </div>
  );
}
