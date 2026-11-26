import { Download, LoaderCircle } from 'lucide-react';
import { Btn } from '../ui';

const BOTONES = [
  { formato: 'excel', texto: 'Exportar Excel' },
  { formato: 'pdf', texto: 'Exportar PDF' },
];

/**
 * "Exportar Excel" y "Exportar PDF" (tarea 33), secundarios, uno al lado del otro. Mientras uno descarga, los dos quedan
 * deshabilitados y el que descarga dice "Exportando…". Sin datos, los dos quedan deshabilitados (ext. 2.1a). Se usan con
 * `useExportar`.
 */
export default function BotonesExportar({ onExportar, exportando, disabled }) {
  return (
    <div className="flex flex-wrap gap-2">
      {BOTONES.map(({ formato, texto }) => (
        <Btn key={formato} variant="outline" size="md" onClick={() => onExportar(formato)} disabled={disabled || Boolean(exportando)}>
          {exportando === formato
            ? <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />
            : <Download size={16} aria-hidden="true" />}
          {exportando === formato ? 'Exportando…' : texto}
        </Btn>
      ))}
    </div>
  );
}
