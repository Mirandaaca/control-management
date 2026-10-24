import { useEffect, useMemo, useState } from 'react';
import { ScrollText } from 'lucide-react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import BotonExportar from '../components/reportes/BotonExportar';
import { Table, Td, Tr } from '../components/Table';
import { Alert, BarraDeFiltros, EstadoVacio, Input, PageHeader, Paginacion, Selector } from '../components/ui';
import { useExportar, useReporte } from '../hooks/useReporte';
import { formatearFecha } from '../utils/formato';

const TAMANO_PAGINA = 25;
const SIN_FILTROS = { usuario: '', modulo: '', desde: '', hasta: '' };

/**
 * La bitácora (8.5, 7.3.35): quién hizo cada operación, en qué módulo, cuándo y desde qué IP, del registro más reciente
 * al más antiguo. Solo se consulta y se exporta: los registros no se modifican ni se eliminan.
 */
export default function Bitacora() {
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [opciones, setOpciones] = useState({ usuarios: [], modulos: [] });
  const [errorOpciones, setErrorOpciones] = useState('');

  useEffect(() => {
    api.get('/bitacora/opciones')
      .then(({ data }) => setOpciones(data))
      .catch((err) => setErrorOpciones(mensajeDeError(err, 'No se pudieron cargar los filtros.')));
  }, []);

  const filtrosDeLaApi = useMemo(() => ({
    usuarioId: filtros.usuario || undefined,
    modulo: filtros.modulo || undefined,
    desde: filtros.desde || undefined,
    hasta: filtros.hasta || undefined,
  }), [filtros]);
  const parametros = useMemo(() => ({ ...filtrosDeLaApi, pagina: numeroPagina, tamano: TAMANO_PAGINA }), [filtrosDeLaApi, numeroPagina]);
  const { datos: pagina, cargando, erroresFiltro, estado: estadoDeCarga } =
    useReporte('/bitacora', parametros, 'No se pudo consultar la bitácora.');
  const { exportar, exportando, errorExportar } = useExportar('/bitacora/excel', filtrosDeLaApi, 'bitacora');

  const filtrar = (cambios) => { setFiltros((f) => ({ ...f, ...cambios })); setNumeroPagina(1); };
  const hayFiltros = Object.values(filtros).some((v) => v !== '');
  const sinRegistros = pagina?.total === 0;

  let estado = estadoDeCarga;
  if (!estado && sinRegistros && !hayFiltros) estado = (
    <EstadoVacio titulo="Todavía no hay registros" icono={ScrollText}>
      Cada operación que registra, modifica o anula datos queda aquí.
    </EstadoVacio>
  );

  return (
    <Layout>
      <PageHeader
        title="Bitácora"
        subtitle="Quién hizo cada operación y cuándo. Los registros no se modifican ni se eliminan."
        action={<BotonExportar onExportar={exportar} exportando={exportando} disabled={!pagina || sinRegistros} />}
      />

      {(errorExportar || errorOpciones) && <Alert type="error" className="mb-4">{errorExportar || errorOpciones}</Alert>}
      {sinRegistros && hayFiltros && (
        <Alert type="info" className="mb-4">Ningún registro coincide con los filtros: no hay nada que exportar.</Alert>
      )}

      <Table
        headers={['Fecha y hora', 'Usuario', 'Módulo', 'Acción', 'IP']}
        empty="Ningún registro coincide con los filtros."
        estado={estado}
        barra={!cargando && (hayFiltros || pagina?.total > 0) && (
          <BarraDeFiltros resultado={pagina ? `${pagina.total} ${pagina.total === 1 ? 'registro' : 'registros'}` : ''}
            hayFiltros={hayFiltros} onLimpiar={() => { setFiltros(SIN_FILTROS); setNumeroPagina(1); }}>
            <div className="w-full sm:w-52">
              <Selector label="Usuario" todos="Todos" value={filtros.usuario}
                opciones={opciones.usuarios.map((u) => ({ valor: u.id, texto: u.nombreCompleto }))}
                onChange={(usuario) => filtrar({ usuario })} />
            </div>
            <div className="w-full sm:w-60">
              <Selector label="Módulo" todos="Todos" value={filtros.modulo}
                opciones={opciones.modulos.map((m) => ({ valor: m, texto: m }))}
                onChange={(modulo) => filtrar({ modulo })} />
            </div>
            <div className="w-full sm:w-40">
              <Input label="Desde" type="date" autoComplete="off" value={filtros.desde} error={erroresFiltro.desde}
                onChange={(e) => filtrar({ desde: e.target.value })} />
            </div>
            <div className="w-full sm:w-40">
              <Input label="Hasta" type="date" autoComplete="off" value={filtros.hasta} error={erroresFiltro.hasta}
                onChange={(e) => filtrar({ hasta: e.target.value })} />
            </div>
          </BarraDeFiltros>
        )}
        pie={pagina && <Paginacion pagina={pagina.numeroPagina} totalPaginas={pagina.totalPaginas} onCambiar={setNumeroPagina} />}
      >
        {(pagina?.items ?? []).map((r) => (
          <Tr key={r.id}>
            <Td className="whitespace-nowrap tabular-nums">{formatearFecha(r.fecha, { hora: true })}</Td>
            <Td>{r.usuario?.nombreCompleto ?? <span className="text-slate-600">—</span>}</Td>
            <Td>{r.modulo}</Td>
            <Td className="min-w-64"><span className="wrap-break-word">{r.accion}</span></Td>
            <Td mono>{r.direccionIp ?? '—'}</Td>
          </Tr>
        ))}
      </Table>
    </Layout>
  );
}
