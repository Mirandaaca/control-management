import { Download, LoaderCircle } from 'lucide-react';
import { Btn } from '../ui';

/** "Exportar Excel", secundario, con su indicador mientras descarga. Sin datos queda deshabilitado (ext. 2.1a). */
export default function BotonExportar({ onExportar, exportando, disabled }) {
  return (
    <Btn variant="outline" size="md" onClick={onExportar} disabled={disabled || exportando}>
      {exportando ? <LoaderCircle size={16} className="animate-spin" aria-hidden="true" /> : <Download size={16} aria-hidden="true" />}
      {exportando ? 'Exportando…' : 'Exportar Excel'}
    </Btn>
  );
}
