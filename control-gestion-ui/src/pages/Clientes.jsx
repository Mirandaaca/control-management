import { useCallback, useEffect, useState } from 'react';
import { Eye, LoaderCircle, LocateFixed, Pencil, Plus, Store, Tag } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import EstadoCliente from '../components/EstadoCliente';
import Layout from '../components/Layout';
import MapaUbicacion from '../components/MapaUbicacion';
import { Table, Td, Tr } from '../components/Table';
import {
  Alert, BarraDeFiltros, Btn, Buscador, Cargando, Casilla, ErrorDeCarga, EstadoVacio, Input, Modal, PageHeader,
  Paginacion, Selector,
} from '../components/ui';
import { useAuth } from '../context/auth';
import { PERMISOS } from '../context/permisos';
import { useDemorado } from '../hooks/useDemorado';
import { diasDesdeHoy, formatearFecha, formatearMonto } from '../utils/formato';

const { ventas, seguridad } = PERMISOS;
const TAMANO_PAGINA = 25;
const SIN_FILTROS = { texto: '', zonaId: '', estadoId: '', porVisitar: false };
const FORMULARIO_VACIO = {
  nombreCompleto: '', telefono: '', razonSocial: '', nit: '', email: '', direccionTexto: '', zonaId: '', ubicacion: null,
};
const EN_EL_CICLO = 'ciclo';

// Errores de la API que pertenecen a un campo del formulario: se muestran junto a él.
const CAMPO_DEL_ERROR = { zona_fuera_de_cartera: 'zonaId', zona_no_existe: 'zonaId' };

function ProximaVisita({ cliente }) {
  if (!cliente.estado?.delSistema) return <span className="text-slate-600">Fuera del ciclo</span>;
  const dias = diasDesdeHoy(cliente.proximaVisita);
  let texto;
  if (dias === null) texto = <span className="font-medium text-cliente-disponible-texto">Nunca visitado</span>;
  else if (dias < 0) texto = <span className="font-medium text-amber-800">Vencida hace {-dias} {dias === -1 ? 'día' : 'días'}</span>;
  else if (dias === 0) texto = <span className="font-medium text-amber-800">Hoy</span>;
  else texto = <span>En {dias} {dias === 1 ? 'día' : 'días'}</span>;
  return (
    <div className="whitespace-nowrap">
      {texto}
      {cliente.proximaVisita && <p className="text-xs text-slate-600">{formatearFecha(cliente.proximaVisita)}</p>}
    </div>
  );
}

function Dato({ titulo, children }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs font-semibold text-slate-600">{titulo}</dt>
      <dd className="mt-0.5 text-sm text-slate-900 wrap-break-word">{children || '—'}</dd>
    </div>
  );
}

export default function Clientes() {
  const { usuario: yo, tiene } = useAuth();
  const puedeRegistrar = tiene(ventas.registrarCliente);
  const puedeEditar = tiene(ventas.editarCliente);
  const puedeAsignarEstado = tiene(ventas.asignarEstadoCliente);
  const veCarteraCompleta = tiene(seguridad.verCarteraCompleta);

  const [pagina, setPagina] = useState(null);
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const busqueda = useDemorado(filtros.texto.trim(), 300);
  const [zonas, setZonas] = useState([]);
  const [estados, setEstados] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [aviso, setAviso] = useState('');
  const [errorAccion, setErrorAccion] = useState('');

  const [detalle, setDetalle] = useState(null);
  const [formulario, setFormulario] = useState(null);
  const [erroresCampo, setErroresCampo] = useState({});
  const [errorFormulario, setErrorFormulario] = useState('');
  const [avisoTelefono, setAvisoTelefono] = useState('');
  const [guardando, setGuardando] = useState(false);
  const [buscandoGps, setBuscandoGps] = useState(false);

  const [cambioEstado, setCambioEstado] = useState(null);
  const [errorEstado, setErrorEstado] = useState('');
  const [guardandoEstado, setGuardandoEstado] = useState(false);

  // Un promotor sin cartera completa solo registra o mueve clientes a sus zonas: la API rechaza las demás.
  const zonasElegibles = veCarteraCompleta ? zonas : zonas.filter((z) => z.promotorId === yo?.id);

  useEffect(() => {
    Promise.all([api.get('/zonas'), api.get('/clientes/estados')])
      .then(([z, e]) => { setZonas(z.data); setEstados(e.data); })
      .catch((err) => setErrorAccion(mensajeDeError(err, 'No se pudieron cargar las zonas y los estados.')));
  }, []);

  const cargar = useCallback(() => api.get('/clientes', {
    params: {
      pagina: numeroPagina,
      tamano: TAMANO_PAGINA,
      buscar: busqueda || undefined,
      zonaId: filtros.zonaId || undefined,
      estadoId: filtros.estadoId || undefined,
      porVisitar: filtros.porVisitar || undefined,
    },
  })
    .then(({ data }) => { setPagina(data); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la lista de clientes.')))
    .finally(() => { setCargando(false); setReintentando(false); }),
  [numeroPagina, busqueda, filtros.zonaId, filtros.estadoId, filtros.porVisitar]);

  useEffect(() => { cargar(); }, [cargar]);

  // Cambiar un filtro vuelve a la primera página.
  const filtrar = (cambios) => { setFiltros((f) => ({ ...f, ...cambios })); setNumeroPagina(1); };

  // La lista no trae la dirección ni la ubicación: el detalle y el formulario las piden aparte. Si falla, avisa y da null.
  const pedirDetalle = (id) => api.get(`/clientes/${id}`)
    .then(({ data }) => data)
    .catch((err) => { setErrorAccion(mensajeDeError(err, 'No se pudo abrir el cliente.')); return null; });

  const quitarErrorDeUbicacion = () =>
    setErroresCampo((e) => Object.fromEntries(Object.entries(e).filter(([campo]) => campo !== 'latitud')));

  const verDetalle = (id) => { setErrorAccion(''); pedirDetalle(id).then((d) => d && setDetalle(d)); };

  const abrirFormulario = (cliente) => {
    setErroresCampo({});
    setErrorFormulario('');
    setAvisoTelefono('');
    setDetalle(null);
    setFormulario(cliente
      ? {
        id: cliente.id,
        nombreCompleto: cliente.nombreCompleto,
        telefono: cliente.telefono,
        razonSocial: cliente.razonSocial ?? '',
        nit: cliente.nit ?? '',
        email: cliente.email ?? '',
        direccionTexto: cliente.direccionTexto,
        zonaId: String(cliente.zona.id),
        ubicacion: { latitud: cliente.latitud, longitud: cliente.longitud },
      }
      : { ...FORMULARIO_VACIO, zonaId: zonasElegibles.length === 1 ? String(zonasElegibles[0].id) : '' });
  };

  const editar = (id) => { setErrorAccion(''); pedirDetalle(id).then((d) => d && abrirFormulario(d)); };

  const usarMiUbicacion = () => {
    if (!navigator.geolocation) {
      setErroresCampo((e) => ({ ...e, latitud: 'Este navegador no puede dar tu ubicación: márcala en el mapa.' }));
      return;
    }
    setBuscandoGps(true);
    navigator.geolocation.getCurrentPosition(
      ({ coords }) => {
        setFormulario((f) => ({ ...f, ubicacion: { latitud: coords.latitude, longitud: coords.longitude } }));
        quitarErrorDeUbicacion();
        setBuscandoGps(false);
      },
      () => {
        setErroresCampo((e) => ({ ...e, latitud: 'No se pudo obtener tu ubicación: márcala en el mapa.' }));
        setBuscandoGps(false);
      },
      { enableHighAccuracy: true, timeout: 10000 },
    );
  };

  const guardar = async (confirmarTelefonoRepetido = false) => {
    setGuardando(true);
    setAviso('');
    setErroresCampo({});
    setErrorFormulario('');
    setAvisoTelefono('');
    const { id, ubicacion, zonaId, razonSocial, nit, email, ...resto } = formulario;
    const datos = {
      ...resto,
      razonSocial: razonSocial || null,
      nit: nit || null,
      email: email || null,
      zonaId: Number(zonaId) || 0,
      latitud: ubicacion?.latitud ?? null,
      longitud: ubicacion?.longitud ?? null,
      confirmarTelefonoRepetido,
    };
    try {
      if (id) await api.put(`/clientes/${id}`, datos);
      else await api.post('/clientes', datos);
      setAviso(id ? `Se guardaron los cambios de ${datos.nombreCompleto}.` : `Se registró a ${datos.nombreCompleto}.`);
      setFormulario(null);
      cargar();
    } catch (err) {
      const { codigo, detail } = err.response?.data ?? {};
      if (codigo === 'telefono_repetido') {
        setAvisoTelefono(detail);
        return;
      }
      const campos = erroresPorCampo(err);
      if (CAMPO_DEL_ERROR[codigo]) campos[CAMPO_DEL_ERROR[codigo]] = detail;
      setErroresCampo(campos);
      if (Object.keys(campos).length === 0) setErrorFormulario(mensajeDeError(err, 'No se pudo guardar el cliente.'));
    } finally {
      setGuardando(false);
    }
  };

  const abrirCambioEstado = (cliente) => {
    setErrorEstado('');
    setCambioEstado({ cliente, valor: cliente.estado.delSistema ? EN_EL_CICLO : String(cliente.estado.id) });
  };

  const guardarEstado = async () => {
    setGuardandoEstado(true);
    setErrorEstado('');
    setAviso('');
    try {
      const estadoClienteId = cambioEstado.valor === EN_EL_CICLO ? null : Number(cambioEstado.valor);
      const { data } = await api.put(`/clientes/${cambioEstado.cliente.id}/estado`, { estadoClienteId });
      setAviso(`${data.nombreCompleto} quedó en "${data.estado.nombre}".`);
      setCambioEstado(null);
      cargar();
    } catch (err) {
      setErrorEstado(mensajeDeError(err, 'No se pudo cambiar el estado.'));
    } finally {
      setGuardandoEstado(false);
    }
  };

  const hayFiltros = filtros.texto !== '' || filtros.zonaId !== '' || filtros.estadoId !== '' || filtros.porVisitar;
  const clientes = pagina?.items ?? [];
  const zonaDelFormulario = zonas.find((z) => String(z.id) === formulario?.zonaId);
  const opcionesDeEstado = [
    { valor: EN_EL_CICLO, texto: 'En el ciclo de visitas (lo decide el sistema)' },
    ...estados.filter((e) => !e.delSistema).map((e) => ({ valor: String(e.id), texto: e.nombre })),
  ];

  let estado = null;
  if (cargando) estado = <Cargando texto="Cargando clientes…" />;
  else if (errorCarga && !pagina) estado = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
  );
  else if (pagina?.total === 0 && !hayFiltros) estado = (
    <EstadoVacio titulo="Todavía no hay clientes" icono={Store}
      accion={puedeRegistrar && <Btn size="md" onClick={() => abrirFormulario(null)}><Plus size={16} aria-hidden="true" />Nuevo cliente</Btn>}>
      {veCarteraCompleta ? 'Registra el primero con su ubicación.' : 'Los clientes de tus zonas aparecen aquí.'}
    </EstadoVacio>
  );

  const encabezados = ['Cliente', 'Celular', 'Zona', ...(veCarteraCompleta ? ['Promotor'] : []), 'Estado', 'Próxima visita', 'Saldo', ''];

  return (
    <Layout>
      <PageHeader
        title="Clientes"
        subtitle={veCarteraCompleta ? 'Los clientes de todas las zonas, con su ubicación y su próxima visita.' : 'Los clientes de tus zonas, con su ubicación y su próxima visita.'}
        action={puedeRegistrar && (
          <Btn size="md" onClick={() => abrirFormulario(null)}>
            <Plus size={16} aria-hidden="true" />
            Nuevo cliente
          </Btn>
        )}
      />

      {aviso && <Alert type="success" className="mb-4">{aviso}</Alert>}
      {errorAccion && <Alert type="error" className="mb-4">{errorAccion}</Alert>}
      {errorCarga && pagina && <Alert type="error" className="mb-4">{errorCarga}</Alert>}

      <Table
        headers={encabezados}
        empty="Ningún cliente coincide con la búsqueda."
        estado={estado}
        barra={!cargando && pagina && (pagina.total > 0 || hayFiltros) && (
          <BarraDeFiltros
            resultado={`${pagina.total} ${pagina.total === 1 ? 'cliente' : 'clientes'}`}
            hayFiltros={hayFiltros}
            onLimpiar={() => { setFiltros(SIN_FILTROS); setNumeroPagina(1); }}
          >
            <Buscador label="Buscar" placeholder="Nombre, código, celular o NIT…" value={filtros.texto}
              onChange={(texto) => filtrar({ texto })} />
            <div className="w-full sm:w-48">
              <Selector label="Zona" todos="Todas las zonas" value={filtros.zonaId}
                opciones={zonasElegibles.map((z) => ({ valor: String(z.id), texto: z.nombre }))}
                onChange={(zonaId) => filtrar({ zonaId })} />
            </div>
            <div className="w-full sm:w-52">
              <Selector label="Estado" todos="Todos los estados" value={filtros.estadoId}
                opciones={estados.map((e) => ({ valor: String(e.id), texto: e.nombre }))}
                onChange={(estadoId) => filtrar({ estadoId })} />
            </div>
            <div className="sm:h-10 sm:flex sm:items-center">
              <Casilla checked={filtros.porVisitar} onCheckedChange={(v) => filtrar({ porVisitar: v === true })}>
                Solo por visitar
              </Casilla>
            </div>
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
              <p className="text-xs text-slate-600">{c.codigo}</p>
            </Td>
            <Td className="whitespace-nowrap tabular-nums">{c.telefono}</Td>
            <Td>{c.zona.nombre}</Td>
            {veCarteraCompleta && <Td>{c.promotor?.nombreCompleto ?? <span className="text-slate-600">Sin promotor</span>}</Td>}
            <Td><EstadoCliente estado={c.estado} /></Td>
            <Td><ProximaVisita cliente={c} /></Td>
            <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(c.saldoPendiente)}</Td>
            <Td>
              <div className="flex items-center justify-end gap-1">
                <Btn variant="ghost" onClick={() => verDetalle(c.id)} aria-label={`Ver a ${c.nombreCompleto}`}>
                  <Eye size={14} aria-hidden="true" />
                  Ver
                </Btn>
                {puedeEditar && (
                  <Btn variant="ghost" onClick={() => editar(c.id)} aria-label={`Editar a ${c.nombreCompleto}`}>
                    <Pencil size={14} aria-hidden="true" />
                    Editar
                  </Btn>
                )}
                {puedeAsignarEstado && (
                  <Btn variant="ghost" onClick={() => abrirCambioEstado(c)} aria-label={`Cambiar el estado de ${c.nombreCompleto}`}>
                    <Tag size={14} aria-hidden="true" />
                    Estado
                  </Btn>
                )}
              </div>
            </Td>
          </Tr>
        ))}
      </Table>

      {/* Detalle */}
      <Modal
        abierto={detalle !== null}
        onCerrar={() => setDetalle(null)}
        ancho="lg"
        titulo={detalle ? `${detalle.nombreCompleto} · ${detalle.codigo}` : ''}
        pie={(
          <>
            <Btn variant="outline" size="md" onClick={() => setDetalle(null)}>Cerrar</Btn>
            {puedeEditar && <Btn size="md" onClick={() => abrirFormulario(detalle)}><Pencil size={16} aria-hidden="true" />Editar</Btn>}
          </>
        )}
      >
        {detalle && (
          <div className="space-y-4">
            <dl className="grid gap-4 sm:grid-cols-3">
              <Dato titulo="Estado"><EstadoCliente estado={detalle.estado} /></Dato>
              <Dato titulo="Celular">{detalle.telefono}</Dato>
              <Dato titulo="Saldo pendiente">{formatearMonto(detalle.saldoPendiente)}</Dato>
              <Dato titulo="Zona">{detalle.zona.nombre}</Dato>
              <Dato titulo="Promotor">{detalle.promotor?.nombreCompleto ?? 'Sin promotor'}</Dato>
              <Dato titulo="Correo">{detalle.email}</Dato>
              <Dato titulo="Razón social">{detalle.razonSocial}</Dato>
              <Dato titulo="NIT">{detalle.nit}</Dato>
              <Dato titulo="Registrado el">{formatearFecha(detalle.fechaCreacion)}</Dato>
              <Dato titulo="Última visita">{detalle.ultimaVisita ? formatearFecha(detalle.ultimaVisita, { hora: true }) : 'Nunca'}</Dato>
              <Dato titulo="Próxima visita">{detalle.proximaVisita ? formatearFecha(detalle.proximaVisita) : '—'}</Dato>
            </dl>
            <Dato titulo="Dirección">{detalle.direccionTexto}</Dato>
            <MapaUbicacion valor={detalle} alto={240} />
          </div>
        )}
      </Modal>

      {/* Registrar o editar */}
      <Modal
        abierto={formulario !== null}
        onCerrar={() => setFormulario(null)}
        ancho="lg"
        titulo={formulario?.id ? 'Editar cliente' : 'Nuevo cliente'}
        descripcion="El promotor del cliente es el de su zona. Sin la ubicación marcada no se puede guardar."
        pie={(
          <>
            <Btn variant="outline" size="md" onClick={() => setFormulario(null)} disabled={guardando}>Cancelar</Btn>
            <Btn type="submit" form="formulario-cliente" size="md" disabled={guardando || zonasElegibles.length === 0}>
              {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
              {guardando ? 'Guardando…' : 'Guardar'}
            </Btn>
          </>
        )}
      >
        {formulario && (
          <form id="formulario-cliente" onSubmit={(e) => { e.preventDefault(); guardar(); }} className="space-y-4" noValidate>
            {errorFormulario && <Alert type="error">{errorFormulario}</Alert>}
            {zonasElegibles.length === 0 && (
              <Alert type="warning">No tienes zonas asignadas: pídele al gerente general que te asigne una.</Alert>
            )}
            {avisoTelefono && (
              <Alert type="warning">
                <p>{avisoTelefono} ¿Es otro cliente? Si es el mismo, cancela y búscalo en la lista.</p>
                <div className="mt-2">
                  <Btn variant="outline" onClick={() => guardar(true)} disabled={guardando}>Registrar igual</Btn>
                </div>
              </Alert>
            )}
            <div className="grid gap-4 md:grid-cols-2">
              <Input label="Nombre completo" value={formulario.nombreCompleto} error={erroresCampo.nombreCompleto} autoComplete="off"
                onChange={(e) => setFormulario({ ...formulario, nombreCompleto: e.target.value })} />
              <Input label="Celular" type="tel" inputMode="numeric" value={formulario.telefono} error={erroresCampo.telefono} autoComplete="off"
                onChange={(e) => setFormulario({ ...formulario, telefono: e.target.value })} />
              <Input label="Razón social (opcional)" value={formulario.razonSocial} error={erroresCampo.razonSocial} autoComplete="off"
                onChange={(e) => setFormulario({ ...formulario, razonSocial: e.target.value })} />
              <Input label="NIT (opcional)" value={formulario.nit} error={erroresCampo.nit} autoComplete="off"
                onChange={(e) => setFormulario({ ...formulario, nit: e.target.value })} />
              <Input label="Correo (opcional)" type="email" value={formulario.email} error={erroresCampo.email} autoComplete="off" spellCheck={false}
                onChange={(e) => setFormulario({ ...formulario, email: e.target.value })} />
              <Selector label="Zona" placeholder="Elige la zona" value={formulario.zonaId} error={erroresCampo.zonaId}
                opciones={zonasElegibles.map((z) => ({ valor: String(z.id), texto: z.nombre }))}
                ayuda={zonaDelFormulario
                  ? (zonaDelFormulario.promotor ? `Promotor: ${zonaDelFormulario.promotor.nombreCompleto}.` : 'Esta zona no tiene promotor asignado.')
                  : undefined}
                onChange={(zonaId) => setFormulario({ ...formulario, zonaId })} />
            </div>
            <Input label="Dirección" value={formulario.direccionTexto} error={erroresCampo.direccionTexto} autoComplete="off"
              placeholder="Calle, número y una referencia…"
              onChange={(e) => setFormulario({ ...formulario, direccionTexto: e.target.value })} />
            <div>
              <p className="text-sm font-medium text-slate-700 mb-1">Ubicación</p>
              <MapaUbicacion
                valor={formulario.ubicacion}
                etiqueta="Mapa: haz clic donde está el cliente para marcar su ubicación"
                onCambiar={(ubicacion) => {
                  setFormulario((f) => ({ ...f, ubicacion }));
                  quitarErrorDeUbicacion();
                }}
              />
              <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
                <p className="text-sm text-slate-600 tabular-nums" aria-live="polite">
                  {formulario.ubicacion
                    ? `Marcada en ${Number(formulario.ubicacion.latitud).toFixed(6)}, ${Number(formulario.ubicacion.longitud).toFixed(6)}.`
                    : 'Haz clic en el mapa donde está el cliente.'}
                </p>
                <Btn variant="outline" onClick={usarMiUbicacion} disabled={buscandoGps}>
                  {buscandoGps ? <LoaderCircle size={14} className="animate-spin" aria-hidden="true" /> : <LocateFixed size={14} aria-hidden="true" />}
                  Usar mi ubicación
                </Btn>
              </div>
              {erroresCampo.latitud && <Alert type="error" className="mt-2">{erroresCampo.latitud}</Alert>}
            </div>
          </form>
        )}
      </Modal>

      {/* Estado asignado a mano */}
      <Modal
        abierto={cambioEstado !== null}
        onCerrar={() => setCambioEstado(null)}
        titulo={`Estado de ${cambioEstado?.cliente.nombreCompleto ?? ''}`}
        descripcion="Un estado asignado a mano (Inactivo y otros) saca al cliente del ciclo de visitas: deja de salir entre los clientes por visitar. Activo y Disponible los asigna el sistema según las visitas."
        pie={(
          <>
            <Btn variant="outline" size="md" onClick={() => setCambioEstado(null)} disabled={guardandoEstado}>Cancelar</Btn>
            <Btn size="md" onClick={guardarEstado} disabled={guardandoEstado}>
              {guardandoEstado && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
              Cambiar estado
            </Btn>
          </>
        )}
      >
        {cambioEstado && (
          <div className="space-y-4">
            <p className="text-sm text-slate-700">Ahora: <EstadoCliente estado={cambioEstado.cliente.estado} /></p>
            <Selector label="Nuevo estado" value={cambioEstado.valor} opciones={opcionesDeEstado}
              onChange={(valor) => setCambioEstado({ ...cambioEstado, valor })} />
            {errorEstado && <Alert type="error">{errorEstado}</Alert>}
          </div>
        )}
      </Modal>
    </Layout>
  );
}
