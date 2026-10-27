import { DayPicker } from 'react-day-picker';
import { es } from 'react-day-picker/locale';
import { ChevronLeft, ChevronRight } from 'lucide-react';

// El calendario de react-day-picker 9, en español (también las etiquetas del lector de pantalla) y con los colores de
// docs/diseno.md. Lo usan CampoFecha y CampoFechaHora de ui/index.jsx: las pantallas no lo importan directo.

const botonDeNavegacion = 'inline-flex size-9 items-center justify-center rounded-lg text-slate-700 hover:bg-slate-100 cursor-pointer '
  + 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 disabled:cursor-not-allowed disabled:opacity-40';

function Flecha({ orientation, className }) {
  const Icono = orientation === 'left' ? ChevronLeft : ChevronRight;
  return <Icono size={16} className={className} aria-hidden="true" />;
}

export function Calendario(props) {
  return (
    <DayPicker
      locale={es}
      showOutsideDays
      classNames={{
        root: 'text-sm',
        months: 'relative flex flex-col gap-4',
        month: 'flex flex-col gap-2',
        month_caption: 'flex h-9 items-center justify-center px-10',
        caption_label: 'font-semibold text-slate-900 first-letter:uppercase',
        nav: 'absolute inset-x-0 top-0 flex items-center justify-between',
        button_previous: botonDeNavegacion,
        button_next: botonDeNavegacion,
        month_grid: 'border-collapse',
        weekdays: 'flex',
        weekday: 'w-9 py-1 text-xs font-medium text-slate-600 first-letter:uppercase',
        week: 'flex',
        day: 'size-9 p-0 text-center',
        day_button: 'size-9 rounded-lg text-slate-900 tabular-nums cursor-pointer hover:bg-marca-50 '
          + 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500',
        today: '[&>button]:font-semibold [&>button]:text-marca-700 [&>button]:underline [&>button]:underline-offset-4',
        selected: '[&>button]:bg-marca-600 [&>button]:text-white [&>button]:no-underline [&>button]:hover:bg-marca-700',
        outside: '[&>button]:text-slate-500',
        disabled: '[&>button]:text-slate-300 [&>button]:cursor-not-allowed [&>button]:hover:bg-transparent',
        hidden: 'invisible',
      }}
      components={{ Chevron: Flecha }}
      {...props}
    />
  );
}
