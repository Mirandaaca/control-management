import { useCallback, useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { Ban, CircleCheck, ClipboardList, Eye, LoaderCircle, Plus } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import CampoUbicacion from '../components/CampoUbicacion';
import Layout from '../components/Layout';
import LineasDeProductos from '../components/LineasDeProductos';
import MapaUbicacion from '../components/MapaUbicacion';
import { Table, Td, Tr } from '../components/Table';
import {
  Alert, AreaDeTexto, Badge, BarraDeFiltros, Btn, Buscador, Cargando, Confirmar, ErrorDeCarga, EstadoVacio, Input, Modal,
  PageHeader, Paginacion, Selector,
} from '../components/ui';
import { useAuth } from '../context/auth';
import { PERMISOS } from '../context/permisos';
import { useDemorado } from '../hooks/useDemorado';
import { useUbicacionDelNavegador } from '../hooks/useUbicacionDelNavegador';
import { mostrarPrimerProblema } from '../utils/foco';
import { formatearCantidad, formatearFecha, formatearMonto } from '../utils/formato';
import { totalDe } from '../utils/lineas';

const { ventas, seguridad } = PERMISOS;
const TAMANO_PAGINA = 25;
const SIN_FILTROS = { texto: '', estado: '', tipo: '', desde: '', hasta: '' };

// EstadoHP y TipoVenta de la API.
const BORRADOR = 0;
const CONFIRMADA = 1;
const ANULADA = 2;
const ESTADOS = {
  [BORRADOR]: { texto: 'Borrador', color: 'gray' },
  [CONFIRMADA]: { texto: 'Confirmada', color: 'green' },
  [ANULADA]: { texto: 'Anulada', color: 'red' },
};
const CREDITO = 1;
const TIPOS = { 0: 'Contado', [CREDITO]: 'Crédito' };
const ESTADOS_DE_COBRANZA = { 0: 'Pendiente', 1: 'Parcial', 2: 'Pagada', 3: 'Vencida', 4: 'Incobrable', 5: 'Anulada' };

// Errores de la API que pertenecen a un campo del formulario: se muestran junto a él.
const CAMPO_DEL_ERROR = { cliente_no_existe: 'clienteId', cliente_inactivo: 'clienteId' };

function EstadoHP({ estado }) {
  const { texto, color } = ESTADOS[estado] ?? { texto: 'Desconocido', color: 'gray' };
  return <Badge color={color}>{texto}</Badge>;
}

function Dato({ titulo, children }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs font-semibold text-slate-600">{titulo}</dt>
      <dd className="mt-0.5 text-sm text-slate-900 wrap-break-word">{children || '—'}</dd>
    </div>
  );
}

export default function HojasPedido() {
  const { usuario: yo, tiene } = useAuth();
  const puedeRegistrar = tiene(ventas.registrarHp);
  const puedeConfirmar = tiene(ventas.confirmarHp);
  const puedeAnular = tiene(ventas.anularHp);
  const veCarteraCompleta = tiene(seguridad.verCarteraCompleta);

  const [pagina, setPagina] = useState(null);
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const busqueda = useDemorado(filtros.texto.trim(), 300);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [aviso, setAviso] = useState('');
  const [errorAccion, setErrorAccion] = useState('');

  const [detalle, setDetalle] = useState(null);

  const [formulario, setFormulario] = useState(null);
  const formularioRef = useRef(null);
  const siguienteLinea = useRef(1);
  const [clientes, setClientes] = useState([]);
  const [productos, setProductos] = useState([]);
  const [erroresCampo, setErroresCampo] = useState({});
  const [errorFormulario, setErrorFormulario] = useState('');
  const [guardando, setGuardando] = useState(false);
  const gps = useUbicacionDelNavegador();

  const [confirmando, setConfirmando] = useState(null);
  const [anulando, setAnulando] = useState(null);
  const anulacionRef = useRef(null);
  const [enviando, setEnviando] = useState(false);

  const cargar = useCallback(() => api.get('/hojas-pedido', {
    params: {
      pagina: numeroPagina,
      tamano: TAMANO_PAGINA,
      buscar: busqueda || undefined,
      estado: filtros.estado || undefined,
      tipoVenta: filtros.tipo || undefined,
      desde: filtros.desde || undefined,
      hasta: filtros.hasta || undefined,
    },
  })
    .then(({ data }) => { setPagina(data); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la lista de hojas de pedido.')))
    .finally(() => { setCargando(false); setReintentando(false); }),
  [numeroPagina, busqueda, filtros.estado, filtros.tipo, filtros.desde, filtros.hasta]);

  useEffect(() => { cargar(); }, [cargar]);

  const filtrar = (cambios) => { setFiltros((f) => ({ ...f, ...cambios })); setNumeroPagina(1); };

  const verDetalle = (id) => {
    setErrorAccion('');
    api.get(`/hojas-pedido/${id}`)
      .then(({ data }) => setDetalle(data))
      .catch((err) => setErrorAccion(mensajeDeError(err, 'No se pudo abrir la hoja de pedido.')));
  };

  // Solo quien registró la HP la confirma: la API responde hp_de_otro_promotor a cualquier otro.
  const puedeConfirmarla = (hp) => puedeConfirmar && hp.estadoHP === BORRADOR && hp.promotor.id === yo?.id;
  const puedeAnularla = (hp) => puedeAnular && hp.estadoHP !== ANULADA;

  // El error de un campo se borra en cuanto se cambia: si no, seguiría diciendo "Elige el cliente" con uno elegido.
  const quitarError = (campo) =>
    setErroresCampo((e) => Object.fromEntries(Object.entries(e).filter(([clave]) => clave !== campo)));
  const quitarErrorDeUbicacion = () => quitarError('latitud');

  // La ubicación de la venta la da el GPS del celular (en la app). En el panel se toma del navegador o se marca en el mapa.
  const tomarUbicacion = () => gps.tomar({
    onUbicacion: (ubicacion, precisionGPS) => {
      setFormulario((f) => f && { ...f, ubicacion, precisionGPS });
      quitarErrorDeUbicacion();
    },
    onError: (mensaje) => setErroresCampo((e) => ({ ...e, latitud: mensaje })),
  });

  const nuevaLinea = () => ({ clave: siguienteLinea.current++, productoId: '', cantidad: '1', descuento: '' });

  const abrirFormulario = () => {
    setAviso('');
    setErroresCampo({});
    setErrorFormulario('');
    setFormulario({ clienteId: '', tipoVenta: '0', descuento: '', lineas: [nuevaLinea()], ubicacion: null, precisionGPS: null });
    Promise.all([api.get('/clientes/opciones'), api.get('/productos/opciones')])
      .then(([c, p]) => { setClientes(c.data); setProductos(p.data); })
      .catch((err) => setErrorFormulario(mensajeDeError(err, 'No se pudieron cargar los clientes y los productos.')));
    tomarUbicacion();
  };

  const cambiarLinea = (clave, indice, cambios) => {
    setFormulario((f) => ({ ...f, lineas: f.lineas.map((l) => (l.clave === clave ? { ...l, ...cambios } : l)) }));
    Object.keys(cambios).forEach((campo) => quitarError(`detalles[${indice}].${campo}`));
  };

  const cambiarCampo = (campo, valor) => {
    setFormulario((f) => ({ ...f, [campo]: valor }));
    quitarError(campo);
  };

  const guardar = async () => {
    setGuardando(true);
    setErroresCampo({});
    setErrorFormulario('');
    const datos = {
      clienteId: Number(formulario.clienteId) || 0,
      tipoVenta: Number(formulario.tipoVenta),
      descuento: Number(formulario.descuento) || 0,
      latitud: formulario.ubicacion?.latitud ?? null,
      longitud: formulario.ubicacion?.longitud ?? null,
      precisionGPS: formulario.precisionGPS,
      detalles: formulario.lineas.map((l) => ({
        productoId: Number(l.productoId) || 0,
        cantidad: Number(l.cantidad) || 0,
        descuento: Number(l.descuento) || 0,
      })),
    };
    try {
      const { data } = await api.post('/hojas-pedido', datos);
      setAviso(`Se registró ${data.numero} por ${formatearMonto(data.total)}. Queda en borrador hasta que la confirmes.`);
      setFormulario(null);
      cargar();
    } catch (err) {
      const { codigo, detail } = err.response?.data ?? {};
      flushSync(() => {
        const campos = erroresPorCampo(err);
        if (CAMPO_DEL_ERROR[codigo]) campos[CAMPO_DEL_ERROR[codigo]] = detail;
        setErroresCampo(campos);
        if (Object.keys(campos).length === 0) setErrorFormulario(mensajeDeError(err, 'No se pudo registrar la hoja de pedido.'));
      });
      mostrarPrimerProblema(formularioRef.current);
    } finally {
      setGuardando(false);
    }
  };

  const confirmar = async () => {
    setEnviando(true);
    setConfirmando((c) => ({ ...c, error: '' }));
    try {
      const { data } = await api.post(`/hojas-pedido/${confirmando.hp.id}/confirmar`);
      setAviso(data.cobranza
        ? `${data.numero} quedó confirmada: se descontó el stock y nació la deuda ${data.cobranza.numero}.`
        : `${data.numero} quedó confirmada: se descontó el stock.`);
      setConfirmando(null);
      setDetalle(null);
      cargar();
    } catch (err) {
      setConfirmando((c) => ({ ...c, error: mensajeDeError(err, 'No se pudo confirmar la hoja de pedido.') }));
    } finally {
      setEnviando(false);
    }
  };

  const anular = async () => {
    setEnviando(true);
    setAnulando((a) => ({ ...a, error: '', errorMotivo: '' }));
    try {
      const { data } = await api.post(`/hojas-pedido/${anulando.hp.id}/anular`, { motivo: anulando.motivo });
      setAviso(anulando.hp.estadoHP === CONFIRMADA
        ? `${data.numero} quedó anulada: el stock volvió al inventario${data.cobranza ? ` y la deuda ${data.cobranza.numero} se anuló` : ''}.`
        : `${data.numero} quedó anulada.`);
      setAnulando(null);
      setDetalle(null);
      cargar();
    } catch (err) {
      const errorMotivo = erroresPorCampo(err).motivo;
      flushSync(() => setAnulando((a) => ({
        ...a,
        errorMotivo: errorMotivo ?? '',
        error: errorMotivo ? '' : mensajeDeError(err, 'No se pudo anular la hoja de pedido.'),
      })));
      mostrarPrimerProblema(anulacionRef.current);
    } finally {
      setEnviando(false);
    }
  };

  const hayFiltros = Object.values(filtros).some((v) => v !== '');
  const hojas = pagina?.items ?? [];
  const totalFormulario = formulario ? totalDe(formulario.lineas, productos, formulario.descuento) : 0;

  let estado = null;
  if (cargando) estado = <Cargando texto="Cargando hojas de pedido…" />;
  else if (errorCarga && !pagina) estado = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
  );
  else if (pagina?.total === 0 && !hayFiltros) estado = (
    <EstadoVacio titulo="Todavía no hay hojas de pedido" icono={ClipboardList}
      accion={puedeRegistrar && <Btn size="md" onClick={abrirFormulario}><Plus size={16} aria-hidden="true" />Nueva HP</Btn>}>
      {veCarteraCompleta ? 'Las ventas que registran los promotores aparecen aquí.' : 'Las ventas a los clientes de tu cartera aparecen aquí.'}
    </EstadoVacio>
  );

  const encabezados = [
    'Número', 'Cliente', ...(veCarteraCompleta ? ['Promotor'] : []), 'Tipo', { texto: 'Total', derecha: true }, 'Estado', 'Fecha', '',
  ];

  return (
    <Layout>
      <PageHeader
        title="Hojas de pedido"
        subtitle={veCarteraCompleta ? 'Las ventas en campo de todos los promotores.' : 'Las ventas a los clientes de tu cartera.'}
        action={puedeRegistrar && (
          <Btn size="md" onClick={abrirFormulario}>
            <Plus size={16} aria-hidden="true" />
            Nueva HP
          </Btn>
        )}
      />

      {aviso && <Alert type="success" className="mb-4">{aviso}</Alert>}
      {errorAccion && <Alert type="error" className="mb-4">{errorAccion}</Alert>}
      {errorCarga && pagina && <Alert type="error" className="mb-4">{errorCarga}</Alert>}

      <Table
        headers={encabezados}
        empty="Ninguna hoja de pedido coincide con los filtros."
        estado={estado}
        barra={!cargando && pagina && (pagina.total > 0 || hayFiltros) && (
          <BarraDeFiltros
            resultado={`${pagina.total} ${pagina.total === 1 ? 'hoja de pedido' : 'hojas de pedido'}`}
            hayFiltros={hayFiltros}
            onLimpiar={() => { setFiltros(SIN_FILTROS); setNumeroPagina(1); }}
          >
            <Buscador label="Buscar" placeholder="Número, cliente o código…" value={filtros.texto} onChange={(texto) => filtrar({ texto })} />
            <div className="w-full sm:w-40">
              <Selector label="Estado" todos="Todos" value={filtros.estado}
                opciones={Object.entries(ESTADOS).map(([valor, e]) => ({ valor, texto: e.texto }))}
                onChange={(valor) => filtrar({ estado: valor })} />
            </div>
            <div className="w-full sm:w-36">
              <Selector label="Tipo" todos="Todos" value={filtros.tipo}
                opciones={Object.entries(TIPOS).map(([valor, texto]) => ({ valor, texto }))}
                onChange={(tipo) => filtrar({ tipo })} />
            </div>
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
        {hojas.map((hp) => (
          <Tr key={hp.id}>
            <Td mono>{hp.numero}</Td>
            <Td><span className="font-medium">{hp.cliente.nombreCompleto}</span></Td>
            {veCarteraCompleta && <Td>{hp.promotor.nombreCompleto}</Td>}
            <Td>{TIPOS[hp.tipoVenta]}</Td>
            <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(hp.total)}</Td>
            <Td><EstadoHP estado={hp.estadoHP} /></Td>
            <Td className="whitespace-nowrap tabular-nums">{formatearFecha(hp.fechaRegistro, { hora: true })}</Td>
            <Td>
              <div className="flex items-center justify-end gap-1">
                <Btn variant="ghost" onClick={() => verDetalle(hp.id)} aria-label={`Ver ${hp.numero}`}>
                  <Eye size={14} aria-hidden="true" />
                  Ver
                </Btn>
                {puedeConfirmarla(hp) && (
                  <Btn variant="ghost" onClick={() => setConfirmando({ hp, error: '' })} aria-label={`Confirmar ${hp.numero}`}>
                    <CircleCheck size={14} aria-hidden="true" />
                    Confirmar
                  </Btn>
                )}
                {puedeAnularla(hp) && (
                  <Btn variant="ghost" onClick={() => setAnulando({ hp, motivo: '', error: '', errorMotivo: '' })} aria-label={`Anular ${hp.numero}`}>
                    <Ban size={14} aria-hidden="true" />
                    Anular
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
        titulo={detalle ? `${detalle.numero} · ${detalle.cliente.nombreCompleto}` : ''}
        pie={(
          <>
            <Btn variant="outline" size="md" onClick={() => setDetalle(null)}>Cerrar</Btn>
            {detalle && puedeAnularla(detalle) && (
              <Btn variant="danger" size="md" onClick={() => { setAnulando({ hp: detalle, motivo: '', error: '', errorMotivo: '' }); setDetalle(null); }}>
                <Ban size={16} aria-hidden="true" />Anular
              </Btn>
            )}
            {detalle && puedeConfirmarla(detalle) && (
              <Btn size="md" onClick={() => { setConfirmando({ hp: detalle, error: '' }); setDetalle(null); }}>
                <CircleCheck size={16} aria-hidden="true" />Confirmar
              </Btn>
            )}
          </>
        )}
      >
        {detalle && (
          <div className="space-y-4">
            {detalle.estadoHP === ANULADA && (
              <Alert type="warning">
                Anulada el {formatearFecha(detalle.fechaAnulacion, { hora: true })}
                {detalle.anuladoPor && ` por ${detalle.anuladoPor.nombreCompleto}`}: {detalle.observacionesAnulacion}
              </Alert>
            )}
            <dl className="grid gap-4 sm:grid-cols-3">
              <Dato titulo="Estado"><EstadoHP estado={detalle.estadoHP} /></Dato>
              <Dato titulo="Tipo de venta">{TIPOS[detalle.tipoVenta]}</Dato>
              <Dato titulo="Registrada el">{formatearFecha(detalle.fechaRegistro, { hora: true })}</Dato>
              <Dato titulo="Promotor">{detalle.promotor.nombreCompleto}</Dato>
              <Dato titulo="Zona">{detalle.zona.nombre}</Dato>
              <Dato titulo="Cobranza">
                {detalle.cobranza ? `${detalle.cobranza.numero} (${ESTADOS_DE_COBRANZA[detalle.cobranza.estado]})` : 'Sin deuda'}
              </Dato>
            </dl>
            <Table headers={['Producto', { texto: 'Cantidad', derecha: true }, { texto: 'Precio', derecha: true },
              { texto: 'Descuento', derecha: true }, { texto: 'Subtotal', derecha: true }]}>
              {detalle.detalles.map((d) => (
                <Tr key={d.id}>
                  <Td>
                    <p className="font-medium">{d.descripcion}</p>
                    <p className="text-xs text-slate-600 font-mono">{d.sku}</p>
                  </Td>
                  <Td className="text-right tabular-nums">{formatearCantidad(d.cantidad)}</Td>
                  <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(d.precioUnitario)}</Td>
                  <Td className="text-right tabular-nums whitespace-nowrap">{d.descuento ? formatearMonto(d.descuento) : '—'}</Td>
                  <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(d.subtotal)}</Td>
                </Tr>
              ))}
            </Table>
            <dl className="ml-auto w-full max-w-xs space-y-1 text-sm tabular-nums">
              <div className="flex justify-between"><dt className="text-slate-600">Subtotal</dt><dd>{formatearMonto(detalle.subTotal)}</dd></div>
              <div className="flex justify-between"><dt className="text-slate-600">Descuento</dt><dd>{formatearMonto(detalle.descuento)}</dd></div>
              <div className="flex justify-between border-t border-slate-200 pt-1 font-semibold text-slate-900">
                <dt>Total</dt><dd>{formatearMonto(detalle.total)}</dd>
              </div>
            </dl>
            <div>
              <p className="text-sm font-medium text-slate-700 mb-1">
                Dónde se registró
                {detalle.precisionGPS != null && <span className="font-normal text-slate-600"> (precisión de {detalle.precisionGPS} m)</span>}
              </p>
              <MapaUbicacion valor={detalle} alto={220} etiqueta="Mapa con la ubicación de la venta" />
            </div>
          </div>
        )}
      </Modal>

      {/* Nueva HP */}
      <Modal
        abierto={formulario !== null}
        onCerrar={() => setFormulario(null)}
        ancho="lg"
        titulo="Nueva hoja de pedido"
        descripcion="Queda en borrador: el stock se descuenta y la deuda nace recién al confirmarla. La zona es la del cliente."
        pie={(
          <>
            <Btn variant="outline" size="md" onClick={() => setFormulario(null)} disabled={guardando}>Cancelar</Btn>
            <Btn type="submit" form="formulario-hp" size="md" disabled={guardando}>
              {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
              {guardando ? 'Registrando…' : 'Registrar HP'}
            </Btn>
          </>
        )}
      >
        {formulario && (
          <form id="formulario-hp" ref={formularioRef} onSubmit={(e) => { e.preventDefault(); guardar(); }} className="space-y-4" noValidate>
            {errorFormulario && <Alert type="error">{errorFormulario}</Alert>}
            <div className="grid gap-4 md:grid-cols-2">
              <Selector label="Cliente" placeholder="Elige el cliente" value={formulario.clienteId} error={erroresCampo.clienteId}
                opciones={clientes.map((c) => ({ valor: String(c.id), texto: `${c.nombreCompleto} · ${c.codigo}` }))}
                onChange={(clienteId) => cambiarCampo('clienteId', clienteId)} />
              <Selector label="Tipo de venta" value={formulario.tipoVenta} error={erroresCampo.tipoVenta}
                opciones={Object.entries(TIPOS).map(([valor, texto]) => ({ valor, texto }))}
                ayuda={Number(formulario.tipoVenta) === CREDITO ? 'Al confirmarla nace la deuda, que vence en 30 días.' : undefined}
                onChange={(tipoVenta) => cambiarCampo('tipoVenta', tipoVenta)} />
            </div>

            <LineasDeProductos
              lineas={formulario.lineas}
              productos={productos}
              errores={erroresCampo}
              sinStock="con esta cantidad no se podrá confirmar."
              onCambiar={cambiarLinea}
              onQuitar={(clave) => setFormulario((f) => ({ ...f, lineas: f.lineas.filter((l) => l.clave !== clave) }))}
              onAgregar={() => setFormulario((f) => ({ ...f, lineas: [...f.lineas, nuevaLinea()] }))}
            />

            <div className="grid gap-4 sm:grid-cols-2 sm:items-end">
              <Input label="Descuento sobre el total (Bs, opcional)" type="number" inputMode="decimal" min="0" step="0.01"
                value={formulario.descuento} placeholder="0,00" autoComplete="off" error={erroresCampo.descuento}
                onChange={(e) => cambiarCampo('descuento', e.target.value)} />
              <p className="text-right text-base font-semibold text-slate-900 tabular-nums sm:pb-2" aria-live="polite">
                Total: {formatearMonto(totalFormulario)}
              </p>
            </div>

            <CampoUbicacion
              titulo="Ubicación de la venta"
              valor={formulario.ubicacion}
              precision={formulario.precisionGPS}
              buscando={gps.buscando}
              error={erroresCampo.latitud}
              etiquetaMapa="Mapa: haz clic donde se hace la venta para marcar la ubicación"
              sinPunto="Haz clic en el mapa donde se hace la venta."
              onMarcar={(ubicacion) => {
                setFormulario((f) => ({ ...f, ubicacion, precisionGPS: null }));
                quitarErrorDeUbicacion();
              }}
              onUsarMiUbicacion={tomarUbicacion}
            />
          </form>
        )}
      </Modal>

      <Confirmar
        abierto={confirmando !== null}
        titulo={`¿Confirmar ${confirmando?.hp.numero ?? ''}?`}
        textoAccion="Confirmar venta"
        variante="primary"
        enviando={enviando}
        error={confirmando?.error}
        onConfirmar={confirmar}
        onCancelar={() => setConfirmando(null)}
      >
        {confirmando && (
          <>
            <p>
              Venta {confirmando.hp.tipoVenta === CREDITO ? 'a crédito' : 'al contado'} de{' '}
              <span className="font-semibold tabular-nums">{formatearMonto(confirmando.hp.total)}</span> a {confirmando.hp.cliente.nombreCompleto}.
            </p>
            <p>
              Se descuenta el stock de sus productos
              {confirmando.hp.tipoVenta === CREDITO ? ' y nace la deuda, que vence en 30 días' : ''}.
              No se puede deshacer: si hay un error, la secretaria tendrá que anularla.
            </p>
          </>
        )}
      </Confirmar>

      <Confirmar
        abierto={anulando !== null}
        titulo={`¿Anular ${anulando?.hp.numero ?? ''}?`}
        textoAccion="Anular HP"
        enviando={enviando}
        error={anulando?.error}
        onConfirmar={anular}
        onCancelar={() => setAnulando(null)}
      >
        {anulando && (
          <div ref={anulacionRef} className="space-y-3">
            <p>
              {anulando.hp.estadoHP === CONFIRMADA
                ? 'El stock de sus productos vuelve al inventario y su deuda, si es a crédito, se anula. Si la deuda ya tiene pagos, primero hay que anularlos en Cobranzas.'
                : 'Todavía no movió el stock ni creó una deuda: solo queda anulada.'}
            </p>
            <AreaDeTexto label="Motivo" value={anulando.motivo} error={anulando.errorMotivo} autoComplete="off"
              placeholder="Por qué se anula la venta…"
              onChange={(e) => setAnulando({ ...anulando, motivo: e.target.value })} />
          </div>
        )}
      </Confirmar>
    </Layout>
  );
}
