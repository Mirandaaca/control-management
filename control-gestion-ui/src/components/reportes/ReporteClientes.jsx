import { useMemo, useState } from 'react';
import { UserPlus, UserRoundCheck } from 'lucide-react';
import { useExportar, useReporte } from '../../hooks/useReporte';
import { diaEnBolivia, formatearFecha } from '../../utils/formato';
import { Table, Td, Tr } from '../Table';
import { Alert, BarraDeFiltros, Indicador, Input, Paginacion } from '../ui';
import BotonExportar from './BotonExportar';

const TAMANO_PAGINA = 25;

// El período pide las dos fechas (ext. 2a): empieza con el mes en curso.
const delMesEnCurso = () => {
  const hoy = diaEnBolivia();
  return { desde: `${hoy.slice(0, 8)}01`, hasta: hoy };
};

const clientes = (n) => (n === 1 ? '1 cliente' : `${n} clientes`);

function Cliente({ cliente }) {
  return (
    <Td>
      <span className="block font-medium">{cliente.nombreCompleto}</span>
      <span className="block font-mono text-xs text-slate-600" translate="no">{cliente.codigo}</span>
    </Td>
  );
}

/**
 * Clientes nuevos y recurrentes (6.3, 7.3.24): los registrados en el período, y los registrados antes que compraron en
 * él, con cuántas HP confirmadas. Cada lista con su página.
 */
export default function ReporteClientes({ barra, panel }) {
  const [periodo, setPeriodo] = useState(delMesEnCurso);
  const [paginaNuevos, setPaginaNuevos] = useState(1);
  const [paginaRecurrentes, setPaginaRecurrentes] = useState(1);
  const filtrosDeLaApi = useMemo(() => ({ desde: periodo.desde || undefined, hasta: periodo.hasta || undefined }), [periodo]);
  const parametros = useMemo(() => ({ ...filtrosDeLaApi, paginaNuevos, paginaRecurrentes, tamano: TAMANO_PAGINA }),
    [filtrosDeLaApi, paginaNuevos, paginaRecurrentes]);
  const { datos: reporte, cargando, erroresFiltro, estado } =
    useReporte('/reportes/clientes', parametros, 'No se pudo generar el reporte de clientes.');
  const { exportar, exportando, errorExportar } = useExportar('/reportes/clientes/excel', filtrosDeLaApi, 'clientes');

  const cambiar = (cambios) => {
    setPeriodo((p) => ({ ...p, ...cambios }));
    setPaginaNuevos(1);
    setPaginaRecurrentes(1);
  };
  const sinClientes = reporte && reporte.cantidadNuevos === 0 && reporte.cantidadRecurrentes === 0;

  return (
    <>
      {barra(<BotonExportar onExportar={exportar} exportando={exportando} disabled={!reporte || sinClientes} />)}
      <div {...panel}>
        {errorExportar && <Alert type="error" className="mb-4">{errorExportar}</Alert>}
        {sinClientes && (
          <Alert type="info" className="mb-4">No hay clientes nuevos ni recurrentes en el período: no hay nada que exportar.</Alert>
        )}

        {reporte && (
          <section aria-label="Totales del período" className="mb-5 grid gap-4 sm:grid-cols-2">
            <Indicador titulo="Clientes nuevos" icono={UserPlus} valor={reporte.cantidadNuevos} detalle="Registrados en el período" />
            <Indicador titulo="Clientes recurrentes" icono={UserRoundCheck} valor={reporte.cantidadRecurrentes}
              detalle="Registrados antes, con compras en el período" />
          </section>
        )}

        <Table
          titulo="Clientes nuevos"
          headers={['Cliente nuevo', 'Zona', 'Promotor', 'Registrado']}
          empty="Ningún cliente se registró en el período."
          estado={estado}
          barra={!cargando && (
            <BarraDeFiltros resultado={reporte ? clientes(reporte.cantidadNuevos + reporte.cantidadRecurrentes) : ''}
              hayFiltros={false}>
              <div className="w-full sm:w-40">
                <Input label="Desde" type="date" autoComplete="off" required value={periodo.desde} error={erroresFiltro.desde}
                  onChange={(e) => cambiar({ desde: e.target.value })} />
              </div>
              <div className="w-full sm:w-40">
                <Input label="Hasta" type="date" autoComplete="off" required value={periodo.hasta} error={erroresFiltro.hasta}
                  onChange={(e) => cambiar({ hasta: e.target.value })} />
              </div>
            </BarraDeFiltros>
          )}
          pie={reporte && <Paginacion pagina={reporte.nuevos.numeroPagina} totalPaginas={reporte.nuevos.totalPaginas} onCambiar={setPaginaNuevos} />}
        >
          {(reporte?.nuevos.items ?? []).map((c) => (
            <Tr key={c.id}>
              <Cliente cliente={c} />
              <Td>{c.zona.nombre}</Td>
              <Td>{c.promotor?.nombreCompleto ?? '—'}</Td>
              <Td className="whitespace-nowrap tabular-nums">{formatearFecha(c.fechaRegistro)}</Td>
            </Tr>
          ))}
        </Table>

        {reporte && (
          <div className="mt-6">
            <Table
              titulo="Clientes recurrentes"
              headers={['Cliente recurrente', 'Zona', 'Promotor', { texto: 'Compras', derecha: true }]}
              empty="Ningún cliente de antes compró en el período."
              pie={<Paginacion pagina={reporte.recurrentes.numeroPagina} totalPaginas={reporte.recurrentes.totalPaginas} onCambiar={setPaginaRecurrentes} />}
            >
              {reporte.recurrentes.items.map((c) => (
                <Tr key={c.id}>
                  <Cliente cliente={c} />
                  <Td>{c.zona.nombre}</Td>
                  <Td>{c.promotor?.nombreCompleto ?? '—'}</Td>
                  <Td className="text-right tabular-nums">{c.compras}</Td>
                </Tr>
              ))}
            </Table>
          </div>
        )}
      </div>
    </>
  );
}
