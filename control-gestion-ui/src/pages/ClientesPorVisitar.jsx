import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { CalendarCheck, Eye, Plus } from 'lucide-react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import MapaUbicacion from '../components/MapaUbicacion';
import { Table, Td, Tr } from '../components/Table';
import {
  Alert, BarraDeFiltros, Btn, Buscador, Cargando, ErrorDeCarga, EstadoVacio, Modal, PageHeader, Paginacion, Selector,
} from '../components/ui';
import { textoDelAtraso } from '../components/clientes/visitas';
import { useAuth } from '../context/auth';
import { ELIGEN_PROMOTOR, PERMISOS } from '../context/permisos';
import { useDemorado } from '../hooks/useDemorado';
import { diaEnBolivia, formatearDia, formatearFecha } from '../utils/formato';

const TAMANO_PAGINA = 25;
const SIN_FILTROS = { texto: '', zonaId: '', promotorId: '' };

function Dato({ titulo, children }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs font-semibold text-slate-600">{titulo}</dt>
      <dd className="mt-0.5 text-sm text-slate-900">{children}</dd>
    </div>
  );
}

/**
 * Clientes por visitar (5.3, 7.3.19): los del ciclo de visitas cuya próxima visita ya llegó, del más atrasado al más
 * reciente; primero los que nunca se visitaron. Al tocar uno, su ubicación y sus visitas. Desde aquí se planifica una
 * ruta con ellos (ext. 3a).
 */
export default function ClientesPorVisitar() {
  const { usuario, tiene } = useAuth();
  const veTodos = tiene(PERMISOS.seguridad.verCarteraCompleta);
  const eligePromotor = veTodos && tiene(...ELIGEN_PROMOTOR);
  const puedePlanificar = tiene(PERMISOS.territorial.planificarRuta);

  const [pagina, setPagina] = useState(null);
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const busqueda = useDemorado(filtros.texto.trim(), 300);
  const [zonas, setZonas] = useState([]);
  const [promotores, setPromotores] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [errorFiltros, setErrorFiltros] = useState('');
  const [elegido, setElegido] = useState(null);

  useEffect(() => {
    Promise.all([api.get('/zonas'), eligePromotor ? api.get('/usuarios/promotores') : Promise.resolve({ data: [] })])
      .then(([z, p]) => { setZonas(z.data); setPromotores(p.data); })
      .catch((err) => setErrorFiltros(mensajeDeError(err, 'No se pudieron cargar las zonas y los promotores.')));
  }, [eligePromotor]);

  const cargar = useCallback(() => api.get('/clientes/por-visitar', {
    params: {
      pagina: numeroPagina,
      tamano: TAMANO_PAGINA,
      buscar: busqueda || undefined,
      zonaId: filtros.zonaId || undefined,
      promotorId: filtros.promotorId || undefined,
    },
  })
    .then(({ data }) => { setPagina(data); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la lista de clientes por visitar.')))
    .finally(() => { setCargando(false); setReintentando(false); }),
  [numeroPagina, busqueda, filtros.zonaId, filtros.promotorId]);

  useEffect(() => { cargar(); }, [cargar]);

  const filtrar = (cambios) => { setFiltros((f) => ({ ...f, ...cambios })); setNumeroPagina(1); };

  // Sin cartera completa (el promotor), solo sus zonas, como la lista.
  const zonasElegibles = veTodos ? zonas : zonas.filter((z) => z.promotor?.id === usuario?.id);
  const hayFiltros = filtros.texto !== '' || filtros.zonaId !== '' || filtros.promotorId !== '';
  const clientes = pagina?.items ?? [];
  const hoy = formatearDia(diaEnBolivia());

  let estado = null;
  if (cargando) estado = <Cargando texto="Cargando clientes por visitar…" />;
  else if (errorCarga && !pagina) estado = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
  );
  else if (pagina?.total === 0 && !hayFiltros) estado = (
    <EstadoVacio titulo="Ningún cliente debe ser visitado todavía." icono={CalendarCheck}>
      Aquí aparecen los clientes cuando llega el día de su próxima visita.
    </EstadoVacio>
  );

  const planificar = puedePlanificar && (
    <Link to={filtros.zonaId ? `/rutas?planificar=1&zona=${filtros.zonaId}` : '/rutas?planificar=1'}
      className="inline-flex h-10 items-center justify-center gap-1.5 rounded-lg bg-marca-600 px-4 text-sm font-medium text-white transition-colors hover:bg-marca-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 focus-visible:ring-offset-2">
      <Plus size={16} aria-hidden="true" />
      Planificar ruta
    </Link>
  );

  return (
    <Layout>
      <PageHeader
        title="Clientes por visitar"
        subtitle={`Hoy, ${hoy}: los clientes cuya próxima visita ya llegó, del más atrasado al más reciente.`}
        action={planificar}
      />

      {errorFiltros && <Alert type="error" className="mb-4">{errorFiltros}</Alert>}
      {errorCarga && pagina && <Alert type="error" className="mb-4">{errorCarga}</Alert>}

      <Table
        headers={['Cliente', 'Zona', ...(veTodos ? ['Promotor'] : []), 'Última visita', 'Próxima visita', 'Atraso', { texto: 'Acciones', oculto: true }]}
        empty="Ningún cliente por visitar coincide con la búsqueda."
        estado={estado}
        barra={!cargando && pagina && (pagina.total > 0 || hayFiltros) && (
          <BarraDeFiltros
            resultado={`${pagina.total} ${pagina.total === 1 ? 'cliente' : 'clientes'}`}
            hayFiltros={hayFiltros}
            onLimpiar={() => { setFiltros(SIN_FILTROS); setNumeroPagina(1); }}
          >
            <Buscador label="Buscar" placeholder="Nombre, código o celular…" value={filtros.texto}
              onChange={(texto) => filtrar({ texto })} />
            <div className="w-full sm:w-48">
              <Selector label="Zona" todos="Todas las zonas" value={filtros.zonaId}
                opciones={zonasElegibles.map((z) => ({ valor: String(z.id), texto: z.nombre }))}
                onChange={(zonaId) => filtrar({ zonaId })} />
            </div>
            {eligePromotor && (
              <div className="w-full sm:w-56">
                <Selector label="Promotor" todos="Todos los promotores" value={filtros.promotorId}
                  opciones={promotores.map((p) => ({ valor: p.id, texto: p.nombreCompleto }))}
                  onChange={(promotorId) => filtrar({ promotorId })} />
              </div>
            )}
          </BarraDeFiltros>
        )}
        pie={pagina && (
          <Paginacion pagina={pagina.numeroPagina} totalPaginas={pagina.totalPaginas} onCambiar={setNumeroPagina} />
        )}
      >
        {clientes.map((c) => (
          <Tr key={c.id}>
            <Td>
              <p className="font-medium">{c.nombreCompleto}</p>
              <p className="text-xs text-slate-600" translate="no">{c.codigo}</p>
            </Td>
            <Td>{c.zona.nombre}</Td>
            {veTodos && <Td>{c.promotor?.nombreCompleto ?? <span className="text-slate-600">Sin promotor</span>}</Td>}
            <Td className="whitespace-nowrap tabular-nums">{c.ultimaVisita ? formatearFecha(c.ultimaVisita) : '—'}</Td>
            <Td className="whitespace-nowrap tabular-nums">{c.proximaVisita ? formatearFecha(c.proximaVisita) : '—'}</Td>
            <Td className="whitespace-nowrap tabular-nums">
              <span className={c.diasDeAtraso > 0 ? 'font-medium text-amber-800' : ''}>{textoDelAtraso(c.diasDeAtraso)}</span>
            </Td>
            <Td>
              <div className="flex justify-end">
                <Btn variant="ghost" onClick={() => setElegido(c)} aria-label={`Ver a ${c.nombreCompleto} en el mapa`}>
                  <Eye size={14} aria-hidden="true" />
                  Ver
                </Btn>
              </div>
            </Td>
          </Tr>
        ))}
      </Table>

      <Modal abierto={elegido !== null} onCerrar={() => setElegido(null)} titulo={elegido?.nombreCompleto ?? ''}
        descripcion={elegido ? `${elegido.codigo} · ${elegido.zona.nombre} · ${elegido.direccionTexto}` : undefined} ancho="lg">
        {elegido && (
          <div className="space-y-4 pb-1">
            <dl className="grid gap-3 sm:grid-cols-3">
              <Dato titulo="Última visita">{elegido.ultimaVisita ? formatearFecha(elegido.ultimaVisita, { hora: true }) : 'Nunca'}</Dato>
              <Dato titulo="Próxima visita">{elegido.proximaVisita ? formatearFecha(elegido.proximaVisita) : '—'}</Dato>
              <Dato titulo="Atraso">{textoDelAtraso(elegido.diasDeAtraso)}</Dato>
            </dl>
            <MapaUbicacion valor={elegido} alto={280} etiqueta={`Mapa con la ubicación de ${elegido.nombreCompleto}`} />
          </div>
        )}
      </Modal>
    </Layout>
  );
}
