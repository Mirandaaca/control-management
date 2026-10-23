import { useCallback, useEffect, useState } from 'react';
import { CircleCheck, Eye, Pencil, TriangleAlert } from 'lucide-react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import DetalleEvento from '../components/eventos/DetalleEvento';
import EstadoEvento from '../components/eventos/EstadoEvento';
import FormularioEvento from '../components/eventos/FormularioEvento';
import GaleriaDeFotos from '../components/eventos/GaleriaDeFotos';
import ResolverEvento from '../components/eventos/ResolverEvento';
import { accionesDelEvento, ESTADOS } from '../components/eventos/estados';
import {
  Alert, BarraDeFiltros, Btn, Buscador, Cargando, ErrorDeCarga, EstadoVacio, Input, PageHeader, Paginacion, Selector,
} from '../components/ui';
import { useAuth } from '../context/auth';
import { ELIGEN_PROMOTOR, PANTALLAS, PERMISOS } from '../context/permisos';
import { useDemorado } from '../hooks/useDemorado';
import { formatearFecha } from '../utils/formato';

const { eventos: permisos, seguridad } = PERMISOS;
const TAMANO_PAGINA = 25;
const SIN_FILTROS = { texto: '', tipo: '', estado: '', zona: '', promotor: '', desde: '', hasta: '' };

/**
 * Eventos e incidencias en campo (4.1 y 4.2): la lista, el detalle con las fotos, editar y resolver. El evento se
 * registra en la app, con el GPS del celular (7.3.13, paso 2; tarea 15). El promotor ve los que registró, los de su
 * cartera y los de sus pedidos asignados; la secretaria, todos.
 */
export default function Eventos() {
  const { usuario: yo, tiene } = useAuth();
  const veCarteraCompleta = tiene(seguridad.verCarteraCompleta);
  const eligePromotor = tiene(...ELIGEN_PROMOTOR);
  const puedeVerPedidos = tiene(...PANTALLAS.pedidos);
  const acciones = (evento) => accionesDelEvento(evento, { tiene, permisos, veCarteraCompleta, yo });

  const [pagina, setPagina] = useState(null);
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const busqueda = useDemorado(filtros.texto.trim(), 300);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [aviso, setAviso] = useState('');
  const [errorAccion, setErrorAccion] = useState('');
  const [errorFiltros, setErrorFiltros] = useState('');

  const [tipos, setTipos] = useState([]);
  const [zonas, setZonas] = useState([]);
  const [promotores, setPromotores] = useState([]);
  const [detalle, setDetalle] = useState(null);
  // null: cerrado. { evento }: editar ese evento.
  const [formulario, setFormulario] = useState(null);
  const [resolviendo, setResolviendo] = useState(null);

  // Los catálogos de los filtros. Si fallan, la pantalla igual funciona: se avisa arriba, y "Reintentar" los vuelve a pedir.
  const cargarFiltros = useCallback(() => Promise.all([
    api.get('/eventos/tipos'),
    api.get('/zonas'),
    eligePromotor ? api.get('/usuarios/promotores') : Promise.resolve({ data: [] }),
  ])
    .then(([t, z, p]) => { setTipos(t.data); setZonas(z.data); setPromotores(p.data); setErrorFiltros(''); })
    .catch((err) => setErrorFiltros(`No se pudieron cargar los filtros: ${mensajeDeError(err, 'error desconocido.')}`)),
  [eligePromotor]);

  useEffect(() => { cargarFiltros(); }, [cargarFiltros]);

  const cargar = useCallback(() => api.get('/eventos', {
    params: {
      pagina: numeroPagina,
      tamano: TAMANO_PAGINA,
      buscar: busqueda || undefined,
      tipoEventoId: filtros.tipo || undefined,
      estado: filtros.estado || undefined,
      zonaId: filtros.zona || undefined,
      promotorId: filtros.promotor || undefined,
      desde: filtros.desde || undefined,
      hasta: filtros.hasta || undefined,
    },
  })
    .then(({ data }) => { setPagina(data); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la lista de eventos.')))
    .finally(() => { setCargando(false); setReintentando(false); }),
  [numeroPagina, busqueda, filtros.tipo, filtros.estado, filtros.zona, filtros.promotor, filtros.desde, filtros.hasta]);

  useEffect(() => { cargar(); }, [cargar]);

  const filtrar = (cambios) => { setFiltros((f) => ({ ...f, ...cambios })); setNumeroPagina(1); };

  // El detalle trae la descripción, la ubicación y las fotos: la lista solo trae el resumen. Resolver también lo necesita.
  const abrirEvento = (id, alAbrir = setDetalle) => {
    setErrorAccion('');
    api.get(`/eventos/${id}`)
      .then(({ data }) => alAbrir(data))
      .catch((err) => setErrorAccion(mensajeDeError(err, 'No se pudo abrir el evento.')));
  };

  const abrirFormulario = (evento) => {
    setAviso('');
    setDetalle(null);
    setFormulario({ evento });
  };

  const abrirResolver = (evento) => {
    setAviso('');
    setDetalle(null);
    setResolviendo(evento);
  };

  const alGuardar = (evento) => {
    setFormulario(null);
    setAviso(`${evento.numero} quedó modificado.`);
    cargar();
  };

  const alResolver = (evento) => {
    setResolviendo(null);
    setAviso(`${evento.numero} quedó resuelto.`);
    cargar();
  };

  // Agregar o quitar una foto cambia el detalle y la cantidad de fotos de la lista.
  const alCambiarFotos = (evento, mensaje) => {
    setDetalle(evento);
    setAviso(mensaje);
    cargar();
  };

  const hayFiltros = Object.values(filtros).some((v) => v !== '');
  const eventos = pagina?.items ?? [];

  let estado = null;
  if (cargando) estado = <Cargando texto="Cargando eventos…" />;
  else if (errorCarga && !pagina) estado = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); cargarFiltros(); }} reintentando={reintentando} />
  );
  else if (pagina?.total === 0 && !hayFiltros) estado = (
    <EstadoVacio titulo="Todavía no hay eventos" icono={TriangleAlert}>
      {veCarteraCompleta
        ? 'Las incidencias que registren los promotores aparecen aquí.'
        : 'Las incidencias de tu cartera y las que registres aparecen aquí.'}
    </EstadoVacio>
  );

  const encabezados = ['Número', 'Fecha', 'Tipo', 'Cliente', ...(veCarteraCompleta ? ['Registró'] : []),
    { texto: 'Fotos', derecha: true }, 'Estado', ''];
  const delDetalle = acciones(detalle);

  return (
    <Layout>
      <PageHeader
        title="Eventos"
        subtitle={veCarteraCompleta
          ? 'Las incidencias en campo de todos los clientes, sus fotos y su solución.'
          : 'Las incidencias de tu cartera, sus fotos y su solución.'}
      />

      {aviso && <Alert type="success" className="mb-4">{aviso}</Alert>}
      {errorAccion && <Alert type="error" className="mb-4">{errorAccion}</Alert>}
      {errorFiltros && <Alert type="error" className="mb-4">{errorFiltros}</Alert>}
      {errorCarga && pagina && <Alert type="error" className="mb-4">{errorCarga}</Alert>}

      <Table
        headers={encabezados}
        empty="Ningún evento coincide con los filtros."
        estado={estado}
        barra={!cargando && pagina && (pagina.total > 0 || hayFiltros) && (
          <BarraDeFiltros
            resultado={`${pagina.total} ${pagina.total === 1 ? 'evento' : 'eventos'}`}
            hayFiltros={hayFiltros}
            onLimpiar={() => { setFiltros(SIN_FILTROS); setNumeroPagina(1); }}
          >
            <Buscador label="Buscar" placeholder="Número, cliente o descripción…" value={filtros.texto} onChange={(texto) => filtrar({ texto })} />
            <div className="w-full sm:w-48">
              <Selector label="Tipo" todos="Todos" value={filtros.tipo}
                opciones={tipos.map((t) => ({ valor: String(t.id), texto: t.nombre }))}
                onChange={(tipo) => filtrar({ tipo })} />
            </div>
            <div className="w-full sm:w-36">
              <Selector label="Estado" todos="Todos" value={filtros.estado}
                opciones={Object.entries(ESTADOS).map(([valor, texto]) => ({ valor, texto }))}
                onChange={(valor) => filtrar({ estado: valor })} />
            </div>
            <div className="w-full sm:w-44">
              <Selector label="Zona" todos="Todas" value={filtros.zona}
                opciones={zonas.map((z) => ({ valor: String(z.id), texto: z.nombre }))}
                onChange={(zona) => filtrar({ zona })} />
            </div>
            {veCarteraCompleta && eligePromotor && (
              <div className="w-full sm:w-48">
                <Selector label="Registró" todos="Todos" value={filtros.promotor}
                  opciones={promotores.map((p) => ({ valor: p.id, texto: p.nombreCompleto }))}
                  onChange={(promotor) => filtrar({ promotor })} />
              </div>
            )}
            <div className="w-full sm:w-40">
              <Input label="Desde" type="date" autoComplete="off" value={filtros.desde} onChange={(e) => filtrar({ desde: e.target.value })} />
            </div>
            <div className="w-full sm:w-40">
              <Input label="Hasta" type="date" autoComplete="off" value={filtros.hasta} onChange={(e) => filtrar({ hasta: e.target.value })} />
            </div>
          </BarraDeFiltros>
        )}
        pie={pagina && <Paginacion pagina={pagina.numeroPagina} totalPaginas={pagina.totalPaginas} onCambiar={setNumeroPagina} />}
      >
        {eventos.map((e) => (
          <Tr key={e.id}>
            <Td mono>{e.numero}</Td>
            <Td className="whitespace-nowrap tabular-nums">{formatearFecha(e.fechaEvento, { hora: true })}</Td>
            <Td>{e.tipo.nombre}</Td>
            <Td>
              <p className="font-medium">{e.cliente?.nombreCompleto ?? '—'}</p>
              {e.pedido && <p className="text-xs text-slate-600 font-mono" translate="no">{e.pedido.numero}</p>}
            </Td>
            {veCarteraCompleta && <Td>{e.promotor.nombreCompleto}</Td>}
            <Td className="text-right tabular-nums">{e.cantidadFotos || '—'}</Td>
            <Td><EstadoEvento estado={e.estado} /></Td>
            <Td>
              <div className="flex items-center justify-end gap-1">
                <Btn variant="ghost" onClick={() => abrirEvento(e.id)} aria-label={`Ver ${e.numero}`}>
                  <Eye size={14} aria-hidden="true" />
                  Ver
                </Btn>
                {acciones(e).resolver && (
                  <Btn variant="ghost" onClick={() => abrirEvento(e.id, abrirResolver)} aria-label={`Resolver ${e.numero}`}>
                    <CircleCheck size={14} aria-hidden="true" />
                    Resolver
                  </Btn>
                )}
              </div>
            </Td>
          </Tr>
        ))}
      </Table>

      <DetalleEvento
        evento={detalle}
        onCerrar={() => setDetalle(null)}
        fotos={detalle && (
          <GaleriaDeFotos evento={detalle} puedeAgregar={delDetalle.agregarFoto} puedeEliminar={delDetalle.eliminarFoto}
            onCambio={alCambiarFotos} />
        )}
        acciones={detalle && (
          <>
            {delDetalle.modificar && (
              <Btn variant="outline" size="md" onClick={() => abrirFormulario(detalle)}>
                <Pencil size={16} aria-hidden="true" />Editar
              </Btn>
            )}
            {delDetalle.resolver && (
              <Btn size="md" onClick={() => abrirResolver(detalle)}>
                <CircleCheck size={16} aria-hidden="true" />Resolver
              </Btn>
            )}
          </>
        )}
      />

      {formulario && (
        <FormularioEvento evento={formulario.evento} puedeVerPedidos={puedeVerPedidos}
          onCerrar={() => setFormulario(null)} onGuardado={alGuardar} />
      )}

      {resolviendo && (
        <ResolverEvento evento={resolviendo} puedeAgregarFoto={acciones(resolviendo).agregarFoto}
          onCambio={(evento) => { setResolviendo(evento); cargar(); }} onCerrar={() => setResolviendo(null)} onHecho={alResolver} />
      )}
    </Layout>
  );
}
