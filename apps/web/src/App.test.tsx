import { renderToString } from 'react-dom/server';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import App from './App';

describe('application shell', () => {
  it('shows the Thai name and Romanized brand on the home route', () => {
    const html = renderToString(
      <MemoryRouter initialEntries={['/']}>
        <App />
      </MemoryRouter>,
    );

    expect(html).toContain('รากเรา');
    expect(html).toContain('RAKRAO');
  });
});
