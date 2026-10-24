import { useCallback, useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { Ban, ClipboardList, Eye, Printer } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import MapaUbicacion from '../components/MapaUbicacion';
import EstadoCobranza from '../components/cobranzas/EstadoCobranza';
import EstadoHP from '../components/hojas/EstadoHP';
import { ANULADA, CONFIRMADA, ESTADOS_DE_HP } from '../components/hojas/estados';
import { Table, Td, Tr } from '../components/Table';
import {
  Alert, AreaDeTexto, BarraDeFiltros, Btn, Buscador, Cargando, Confirmar, EnlaceBoton, ErrorDeCarga, EstadoVacio, Input, Modal,
  PageHeader, Paginacion, Selector,
} from '../components/ui';
import { useAuth } from '../context/auth';
import { PERMISOS } from '../context/permisos';
import { useDemorado } from '../hooks/useDemorado';
import { useFiltrosEnLaUrl } from '../hooks/useFiltrosEnLaUrl';
import { mostrarPrimerProblema } from '../utils/foco';
import { formatearCantidad, formatearFecha, formatearMonto } from '../utils/formato';

const { ventas, seguridad, reportes } = PERMISOS;
const TAMANO_PAGINA = 25;
const SIN_FILTROS = { texto: '', estado: '', tipo: '', desde: '', hasta: '' };

// TipoVenta de la API.
const CREDITO = 1;
const TIPOS = { 0: 'Contado', [CREDITO]: 'Crédito' };

function Dato({ titulo, children }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs font-semibold text-slate-600">{titulo}</dt>
      <dd className="mt-0.5 text-sm text-slate-900 wrap-break-word">{children || '—'}</dd>
    </div>
  );
}

/**
 * Hojas de pedido (2.1; `frmHojaPedidoPanel`): consultar, anular e imprimir. Registrar y confirmar una venta es del
 * promotor, en la app, con el GPS del celular (alcance de la 2.1 y 7.3.31, ext. 1a; tarea 17).
 */
export default function HojasPedido() {
  const { tiene } = useAuth();
  const puedeAnular = tiene(ventas.anularHp);
  const puedeImprimir = tiene(reportes.imprimirHp);
  const veCarteraCompleta = tiene(seguridad.verCarteraCompleta);

  const [pagina, setPagina] = useState(null);
  const { filtros, setFiltros, numeroPagina, setNumeroPagina } = useFiltrosEnLaUrl(SIN_FILTROS);
  const busqueda = useDemorado(filtros.texto.trim(), 300);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [aviso, setAviso] = useState('');
  const [errorAccion, setErrorAccion] = useState('');

  const [detalle, setDetalle] = useState(null);

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

  const puedeAnularla = (hp) => puedeAnular && hp.estadoHP !== ANULADA;

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

  let estado = null;
  if (cargando) estado = <Cargando texto="Cargando hojas de pedido…" />;
  else if (errorCarga && !pagina) estado = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
  );
  else if (pagina?.total === 0 && !hayFiltros) estado = (
    <EstadoVacio titulo="Todavía no hay hojas de pedido" icono={ClipboardList}>
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
                opciones={Object.entries(ESTADOS_DE_HP).map(([valor, e]) => ({ valor, texto: e.texto }))}
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
                {puedeImprimir && (
                  <EnlaceBoton variant="ghost" href={`/hojas-pedido/${hp.id}/imprimir`} nuevaPestana aria-label={`Imprimir ${hp.numero}`}>
                    <Printer size={14} aria-hidden="true" />
                    Imprimir
                  </EnlaceBoton>
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
            {detalle && puedeImprimir && (
              <EnlaceBoton size="md" href={`/hojas-pedido/${detalle.id}/imprimir`} nuevaPestana>
                <Printer size={16} aria-hidden="true" />Imprimir
              </EnlaceBoton>
            )}
            {detalle && puedeAnularla(detalle) && (
              <Btn variant="danger" size="md" onClick={() => { setAnulando({ hp: detalle, motivo: '', error: '', errorMotivo: '' }); setDetalle(null); }}>
                <Ban size={16} aria-hidden="true" />Anular
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
                {detalle.cobranza ? (
                  <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
                    {detalle.cobranza.numero}
                    <EstadoCobranza estado={detalle.cobranza.estado} />
                  </span>
                ) : 'Sin deuda'}
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
