import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import indexHtml from '../index.html?raw';
import { App } from './App';
import { strings } from './strings';

describe('App', () => {
  it('renders the app name from the central strings', () => {
    render(<App />);
    expect(screen.getByRole('heading', { level: 1 }).textContent).toBe(strings.appName);
  });

  it('ships an Arabic, right-to-left document', () => {
    expect(indexHtml).toMatch(/<html lang="ar" dir="rtl">/);
  });
});
