import { clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

// Une clases de Tailwind y resuelve los choques: cn('px-2', 'px-4') deja solo 'px-4'.
export function cn(...entradas) {
  return twMerge(clsx(entradas));
}
