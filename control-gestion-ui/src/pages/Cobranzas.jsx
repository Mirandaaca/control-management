import { useCallback, useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { Ban, Banknote, CircleAlert, Clock, Eye, LoaderCircle, Wallet, X } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import { subirComprobante } from '../api/fotos';
import BotonDeFoto from '../components/BotonDeFoto';
import EstadoCobranza from '../components/cobranzas/EstadoCobranza';
import { ESTADOS_DE_COBRANZA, textoDeMora } from '../components/cobranzas/estados';
import ImagenConSesion from '../components/ImagenConSesion';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import {
  Alert, AreaDeTexto, Badge, BarraDeFiltros, Btn, Buscador, Cargando, Confirmar, ErrorDeCarga, EstadoVacio, Indicador, Input,
  Modal, PageHeader, Paginacion, Selector,
} from '../components/ui';
import { useAuth } from '../context/auth';
import { PERMISOS } from '../context/permisos';
import { useDemorado } from '../hooks/useDemorado';
import { mostrarPrimerProblema } from '../utils/foco';
import { formatearFecha, formatearMonto } from '../utils/formato';

const { cobranzas: permisos, seguridad } = PERMISOS;
const TAMANO_PAGINA = 25;
const SIN_FILTROS = { texto: '', estado: '', desde: '', hasta: '' };

// Las abiertas son las que todavía se deben: reciben pagos y cuentan en el saldo.
const ABIERTAS = [0, 1, 3];
const QR = 1;
const METODOS = { 0: 'Efectivo', [QR]: 'QR' };

// Errores de la API que pertenecen a un campo del formulario de pago: se muestran junto a él.
const CAMPO_DEL_ERROR = { monto_invalido: 'monto' };

function Dato({ titulo, children }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs font-semibold text-slate-600">{titulo}</dt>
      <dd className="mt-0.5 text-sm text-slate-900 wrap-break-word">{children || '—'}</dd>
    </div>
  );
}

export default function Cobranzas() {
  const { tiene } = useAuth();
  const puedePagar = tiene(permisos.registrarPago);
  const puedeAnularPago = tiene(permisos.anularPago);
  const puedeMarcarIncobrable = tiene(permisos.marcarIncobrable);
  const veCarteraCompleta = tiene(seguridad.verCarteraCompleta);

  const [pagina, setPagina] = useState(null);
  const [resumen, setResumen] = useState(null);
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const busqueda = useDemorado(filtros.texto.trim(), 300);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [aviso, setAviso] = useState('');
  const [errorAccion, setErrorAccion] = useState('');

  const [detalle, setDetalle] = useState(null);
  // Un aviso dentro del detalle: el comprobante que se adjuntó, o el que no se pudo subir. { tipo, texto }
  const [avisoDetalle, setAvisoDetalle] = useState(null);
  const [comprobanteAbierto, setComprobanteAbierto] = useState(null);
  const [subiendoComprobante, setSubiendoComprobante] = useState(null);
  // Reemplazar borra el comprobante anterior: se confirma. { pago, archivo }
  const [reemplazo, setReemplazo] = useState(null);

  const [pago, setPago] = useState(null);
  const pagoRef = useRef(null);
  const [erroresPago, setErroresPago] = useState({});
  const [errorPago, setErrorPago] = useState('');
  const [guardando, setGuardando] = useState(false);

  // Anular un pago y marcar incobrable piden un motivo: { tipo: 'pago' | 'incobrable', cobranza, pago?, motivo, error }.
  const [motivo, setMotivo] = useState(null);
  const motivoRef = useRef(null);
  const [enviando, setEnviando] = useState(false);

  const cargar = useCallback(() => {
    const params = {
      buscar: busqueda || undefined,
      estado: filtros.estado || undefined,
      desde: filtros.desde || undefined,
      hasta: filtros.hasta || undefined,
    };
    return Promise.all([
      api.get('/cobranzas', { params: { ...params, pagina: numeroPagina, tamano: TAMANO_PAGINA } }),
      api.get('/cobranzas/resumen', { params }),
    ])
      .then(([lista, totales]) => { setPagina(lista.data); setResumen(totales.data); setErrorCarga(''); })
      .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la lista de cobranzas.')))
      .finally(() => { setCargando(false); setReintentando(false); });
  }, [numeroPagina, busqueda, filtros.estado, filtros.desde, filtros.hasta]);

  useEffect(() => { cargar(); }, [cargar]);

  const filtrar = (cambios) => { setFiltros((f) => ({ ...f, ...cambios })); setNumeroPagina(1); };

  const abrirDetalle = (id, aviso = null) => {
    setErrorAccion('');
    return api.get(`/cobranzas/${id}`)
      .then(({ data }) => { setDetalle(data); setAvisoDetalle(aviso); })
      .catch((err) => setErrorAccion(mensajeDeError(err, 'No se pudo abrir la cobranza.')));
  };

  const cerrarDetalle = () => { setDetalle(null); setAvisoDetalle(null); };

  // Una clave nueva cada vez que se abre el formulario: si el mismo pago se envía dos veces, la API no lo duplica.
  // crypto.randomUUID solo existe en localhost o con HTTPS; si falta, el pago se envía sin clave.
  const abrirPago = (cobranza, desdeDetalle = false) => {
    setAviso('');
    setErroresPago({});
    setErrorPago('');
    setDetalle(null);
    setPago({
      cobranza, desdeDetalle, monto: '', metodoPago: '0', referencia: '', observaciones: '', comprobante: null,
      claveIdempotencia: crypto.randomUUID?.() ?? null,
    });
  };

  const cambiarPago = (campo, valor) => {
    setPago((p) => ({ ...p, [campo]: valor }));
    setErroresPago((e) => Object.fromEntries(Object.entries(e).filter(([clave]) => clave !== campo)));
  };

  const registrarPago = async () => {
    setGuardando(true);
    setErroresPago({});
    setErrorPago('');
    try {
      const { data } = await api.post(`/cobranzas/${pago.cobranza.id}/pagos`, {
        monto: Number(pago.monto) || 0,
        metodoPago: Number(pago.metodoPago),
        referencia: pago.referencia.trim() || null,
        observaciones: pago.observaciones.trim() || null,
        claveIdempotencia: pago.claveIdempotencia,
      });
      const { desdeDetalle, cobranza, comprobante } = pago;
      // El comprobante se sube aparte, con el id del pago. Si falla, el pago igual quedó registrado: nunca lo frena.
      let fallo = '';
      if (comprobante) {
        try {
          await subirComprobante(cobranza.id, data.id, comprobante);
        } catch (err) {
          fallo = mensajeDeError(err, 'No se pudo subir el comprobante.');
        }
      }
      const conComprobante = comprobante && !fallo ? ', con su comprobante' : '';
      setAviso(data.saldoPendiente > 0
        ? `Se registró el pago en ${cobranza.numero}${conComprobante}: quedan ${formatearMonto(data.saldoPendiente)} por cobrar.`
        : `Se registró el pago${conComprobante}: ${cobranza.numero} quedó pagada.`);
      setPago(null);
      cargar();
      if (fallo) {
        abrirDetalle(cobranza.id, {
          tipo: 'warning',
          texto: `El pago quedó registrado, pero el comprobante no se subió: ${fallo} Adjúntalo desde los pagos, con “Adjuntar”.`,
        });
      } else if (desdeDetalle) {
        abrirDetalle(cobranza.id);
      }
    } catch (err) {
      const { codigo, detail } = err.response?.data ?? {};
      flushSync(() => {
        const campos = erroresPorCampo(err);
        if (CAMPO_DEL_ERROR[codigo]) campos[CAMPO_DEL_ERROR[codigo]] = detail;
        setErroresPago(campos);
        if (Object.keys(campos).length === 0) setErrorPago(mensajeDeError(err, 'No se pudo registrar el pago.'));
      });
      mostrarPrimerProblema(pagoRef.current);
    } finally {
      setGuardando(false);
    }
  };

  const adjuntarComprobante = async (pagoElegido, archivo) => {
    setReemplazo(null);
    setSubiendoComprobante(pagoElegido.id);
    setAvisoDetalle(null);
    try {
      setDetalle(await subirComprobante(detalle.id, pagoElegido.id, archivo));
      setAvisoDetalle({ tipo: 'success', texto: `Se adjuntó el comprobante del pago de ${formatearMonto(pagoElegido.monto)}.` });
    } catch (err) {
      setAvisoDetalle({ tipo: 'error', texto: mensajeDeError(err, 'No se pudo subir el comprobante.') });
    } finally {
      setSubiendoComprobante(null);
    }
  };

  const elegirComprobante = (pagoElegido, archivo) => {
    if (pagoElegido.comprobanteId) setReemplazo({ pago: pagoElegido, archivo });
    else adjuntarComprobante(pagoElegido, archivo);
  };

  const pedirMotivo = (tipo, cobranza, pagoElegido = null) => {
    setAviso('');
    cerrarDetalle();
    setMotivo({ tipo, cobranza, pago: pagoElegido, texto: '', error: '', errorCampo: '' });
  };

  const enviarMotivo = async () => {
    setEnviando(true);
    setMotivo((m) => ({ ...m, error: '', errorCampo: '' }));
    const { tipo, cobranza, pago: pagoElegido, texto } = motivo;
    try {
      const { data } = tipo === 'pago'
        ? await api.post(`/cobranzas/${cobranza.id}/pagos/${pagoElegido.id}/anular`, { motivo: texto })
        : await api.post(`/cobranzas/${cobranza.id}/incobrable`, { observacion: texto });
      setAviso(tipo === 'pago'
        ? `Se anuló el pago de ${formatearMonto(pagoElegido.monto)}: ${data.numero} quedó con ${formatearMonto(data.saldoPendiente)} por cobrar.`
        : `${data.numero} quedó marcada como incobrable.`);
      setMotivo(null);
      setDetalle(data);
      cargar();
    } catch (err) {
      const campos = erroresPorCampo(err);
      const errorCampo = campos.motivo ?? campos.observacion;
      flushSync(() => setMotivo((m) => ({
        ...m,
        errorCampo: errorCampo ?? '',
        error: errorCampo ? '' : mensajeDeError(err, 'No se pudo guardar el cambio.'),
      })));
      mostrarPrimerProblema(motivoRef.current);
    } finally {
      setEnviando(false);
    }
  };

  const hayFiltros = Object.values(filtros).some((v) => v !== '');
  const cobranzas = pagina?.items ?? [];
  const esQR = Number(pago?.metodoPago) === QR;
  const abierta = (c) => ABIERTAS.includes(c.estado);

  let estado = null;
  if (cargando) estado = <Cargando texto="Cargando cobranzas…" />;
  else if (errorCarga && !pagina) estado = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
  );
  else if (pagina?.total === 0 && !hayFiltros) estado = (
    <EstadoVacio titulo="Todavía no hay cobranzas" icono={Wallet}>
      Una cobranza nace al confirmar una hoja de pedido a crédito.
    </EstadoVacio>
  );

  const encabezados = [
    'Número', 'Cliente', ...(veCarteraCompleta ? ['Promotor'] : []), 'HP',
    { texto: 'Total', derecha: true }, { texto: 'Pagado', derecha: true }, { texto: 'Saldo', derecha: true },
    'Vence', 'Estado', '',
  ];

  return (
    <Layout>
      <PageHeader
        title="Cobranzas"
        subtitle={veCarteraCompleta ? 'Las deudas de las ventas a crédito, sus pagos y su mora.' : 'Las deudas de los clientes de tu cartera y sus pagos.'}
      />

      {resumen && (
        <div className="grid gap-4 sm:grid-cols-3 mb-6">
          <Indicador titulo="Por cobrar" icono={Wallet} valor={formatearMonto(resumen.porCobrar)}
            detalle={hayFiltros ? 'De las cobranzas filtradas.' : 'En las cobranzas abiertas.'} />
          <Indicador titulo="Al día" icono={Clock} valor={resumen.pendientes}
            detalle={resumen.pendientes === 1 ? 'Cobranza pendiente o parcial.' : 'Cobranzas pendientes o parciales.'} />
          <Indicador titulo="Vencidas" icono={CircleAlert} valor={resumen.vencidas}
            detalle={resumen.vencidas === 1 ? 'Cobranza con el plazo cumplido.' : 'Cobranzas con el plazo cumplido.'} />
        </div>
      )}

      {aviso && <Alert type="success" className="mb-4">{aviso}</Alert>}
      {errorAccion && <Alert type="error" className="mb-4">{errorAccion}</Alert>}
      {errorCarga && pagina && <Alert type="error" className="mb-4">{errorCarga}</Alert>}

      <Table
        headers={encabezados}
        empty="Ninguna cobranza coincide con los filtros."
        estado={estado}
        barra={!cargando && pagina && (pagina.total > 0 || hayFiltros) && (
          <BarraDeFiltros
            resultado={`${pagina.total} ${pagina.total === 1 ? 'cobranza' : 'cobranzas'}`}
            hayFiltros={hayFiltros}
            onLimpiar={() => { setFiltros(SIN_FILTROS); setNumeroPagina(1); }}
          >
            <Buscador label="Buscar" placeholder="Número, cliente o código…" value={filtros.texto} onChange={(texto) => filtrar({ texto })} />
            <div className="w-full sm:w-40">
              <Selector label="Estado" todos="Todos" value={filtros.estado}
                opciones={Object.entries(ESTADOS_DE_COBRANZA).map(([valor, e]) => ({ valor, texto: e.texto }))}
                onChange={(valor) => filtrar({ estado: valor })} />
            </div>
            <div className="w-full sm:w-40">
              <Input label="Emitida desde" type="date" autoComplete="off" value={filtros.desde} onChange={(e) => filtrar({ desde: e.target.value })} />
            </div>
            <div className="w-full sm:w-40">
              <Input label="Hasta" type="date" autoComplete="off" value={filtros.hasta} onChange={(e) => filtrar({ hasta: e.target.value })} />
            </div>
          </BarraDeFiltros>
        )}
        pie={pagina && <Paginacion pagina={pagina.numeroPagina} totalPaginas={pagina.totalPaginas} onCambiar={setNumeroPagina} />}
      >
        {cobranzas.map((c) => (
          <Tr key={c.id}>
            <Td mono>{c.numero}</Td>
            <Td><span className="font-medium">{c.cliente.nombreCompleto}</span></Td>
            {veCarteraCompleta && <Td>{c.promotor.nombreCompleto}</Td>}
            <Td mono>{c.hojaPedido?.numero ?? '—'}</Td>
            <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(c.montoTotal)}</Td>
            <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(c.montoPagado)}</Td>
            {/* Lo que queda de una cobranza incobrable o anulada ya no se cobra: se ve atenuado. */}
            <Td className={`text-right tabular-nums whitespace-nowrap ${abierta(c) ? 'font-semibold' : 'text-slate-600'}`}>
              {formatearMonto(c.saldoPendiente)}
            </Td>
            <Td className="whitespace-nowrap tabular-nums">
              {formatearFecha(c.fechaVencimiento)}
              {c.diasDeMora != null && <span className="block text-xs font-medium text-red-700">{textoDeMora(c.diasDeMora)}</span>}
            </Td>
            <Td><EstadoCobranza estado={c.estado} /></Td>
            <Td>
              <div className="flex items-center justify-end gap-1">
                <Btn variant="ghost" onClick={() => abrirDetalle(c.id)} aria-label={`Ver ${c.numero}`}>
                  <Eye size={14} aria-hidden="true" />
                  Ver
                </Btn>
                {puedePagar && abierta(c) && (
                  <Btn variant="ghost" onClick={() => abrirPago(c)} aria-label={`Registrar un pago en ${c.numero}`}>
                    <Banknote size={14} aria-hidden="true" />
                    Pagar
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
        onCerrar={cerrarDetalle}
        ancho="lg"
        titulo={detalle ? `${detalle.numero} · ${detalle.cliente.nombreCompleto}` : ''}
        pie={(
          <>
            <Btn variant="outline" size="md" onClick={cerrarDetalle}>Cerrar</Btn>
            {detalle && puedeMarcarIncobrable && abierta(detalle) && (
              <Btn variant="danger" size="md" onClick={() => pedirMotivo('incobrable', detalle)}>
                <Ban size={16} aria-hidden="true" />Marcar incobrable
              </Btn>
            )}
            {detalle && puedePagar && abierta(detalle) && (
              <Btn size="md" onClick={() => abrirPago(detalle, true)}>
                <Banknote size={16} aria-hidden="true" />Registrar pago
              </Btn>
            )}
          </>
        )}
      >
        {detalle && (
          <div className="space-y-4">
            {avisoDetalle && <Alert type={avisoDetalle.tipo}>{avisoDetalle.texto}</Alert>}
            {detalle.estado === 4 && detalle.observaciones && (
              <Alert type="warning">Marcada como incobrable: {detalle.observaciones}</Alert>
            )}
            {detalle.estado === 5 && <Alert type="info">Su hoja de pedido se anuló: esta deuda ya no se cobra.</Alert>}
            <dl className="grid gap-4 sm:grid-cols-3">
              <Dato titulo="Estado"><EstadoCobranza estado={detalle.estado} /></Dato>
              <Dato titulo="Total"><span className="tabular-nums">{formatearMonto(detalle.montoTotal)}</span></Dato>
              <Dato titulo="Saldo">
                {detalle.estado === 4 || detalle.estado === 5
                  ? <span className="text-slate-600 tabular-nums">{formatearMonto(detalle.saldoPendiente)} (ya no se cobra)</span>
                  : <span className="font-semibold tabular-nums">{formatearMonto(detalle.saldoPendiente)}</span>}
              </Dato>
              <Dato titulo="Emitida el">{formatearFecha(detalle.fechaEmision)}</Dato>
              <Dato titulo="Vence el">
                {formatearFecha(detalle.fechaVencimiento)}
                {detalle.diasDeMora != null && <span className="block text-red-700">{textoDeMora(detalle.diasDeMora)}</span>}
              </Dato>
              <Dato titulo="Pagada el">{detalle.fechaPagoCompleto ? formatearFecha(detalle.fechaPagoCompleto) : '—'}</Dato>
              <Dato titulo="Promotor">{detalle.promotor.nombreCompleto}</Dato>
              <Dato titulo="Hoja de pedido">{detalle.hojaPedido?.numero}</Dato>
            </dl>
            <div>
              <h3 className="text-base font-semibold text-slate-900 mb-2">Pagos</h3>
              {detalle.pagos.length === 0 ? (
                <p className="text-sm text-slate-600">Todavía no tiene pagos.</p>
              ) : (
                <Table headers={['Fecha', 'Método', 'Registrado por', { texto: 'Monto', derecha: true }, 'Comprobante',
                  ...(puedeAnularPago ? [{ texto: 'Anular', oculto: true }] : [])]}>
                  {detalle.pagos.map((p) => (
                    <Tr key={p.id}>
                      <Td className="whitespace-nowrap tabular-nums">{formatearFecha(p.fechaPago, { hora: true })}</Td>
                      <Td>
                        <p>{METODOS[p.metodoPago] ?? '—'}{p.referencia && <span className="text-slate-600"> · {p.referencia}</span>}</p>
                        {p.anulado && (
                          <p className="mt-0.5 text-xs text-slate-700">
                            <Badge color="gray">Anulado</Badge> <span className="wrap-break-word">{p.motivoAnulacion}</span>
                          </p>
                        )}
                      </Td>
                      <Td className="whitespace-nowrap">{p.registradoPor.nombreCompleto}</Td>
                      <Td className={`text-right tabular-nums whitespace-nowrap ${p.anulado ? 'line-through text-slate-600' : ''}`}>
                        {formatearMonto(p.monto)}
                      </Td>
                      <Td>
                        <div className="flex items-center gap-1">
                          {p.comprobanteId && (
                            <Btn variant="ghost" onClick={() => setComprobanteAbierto(p)}
                              aria-label={`Ver el comprobante del pago de ${formatearMonto(p.monto)}`}>
                              <Eye size={14} aria-hidden="true" />
                              Ver
                            </Btn>
                          )}
                          {puedePagar && !p.anulado && (
                            <BotonDeFoto variant="ghost" enviando={subiendoComprobante === p.id} disabled={subiendoComprobante !== null}
                              onElegir={(archivo) => elegirComprobante(p, archivo)}
                              aria-label={`${p.comprobanteId ? 'Reemplazar' : 'Adjuntar'} el comprobante del pago de ${formatearMonto(p.monto)}`}>
                              {p.comprobanteId ? 'Reemplazar' : 'Adjuntar'}
                            </BotonDeFoto>
                          )}
                          {!p.comprobanteId && !(puedePagar && !p.anulado) && <span className="text-slate-600">—</span>}
                        </div>
                      </Td>
                      {puedeAnularPago && (
                        <Td>
                          <div className="flex justify-end">
                            {!p.anulado && (
                              <Btn variant="ghost" onClick={() => pedirMotivo('pago', detalle, p)} aria-label={`Anular el pago de ${formatearMonto(p.monto)}`}>
                                <Ban size={14} aria-hidden="true" />
                                Anular
                              </Btn>
                            )}
                          </div>
                        </Td>
                      )}
                    </Tr>
                  ))}
                </Table>
              )}
            </div>
          </div>
        )}
      </Modal>

      {/* Registrar un pago */}
      <Modal
        abierto={pago !== null}
        onCerrar={() => setPago(null)}
        titulo={`Pago en ${pago?.cobranza.numero ?? ''}`}
        descripcion={pago ? `${pago.cobranza.cliente.nombreCompleto} debe ${formatearMonto(pago.cobranza.saldoPendiente)}.` : undefined}
        pie={(
          <>
            <Btn variant="outline" size="md" onClick={() => setPago(null)} disabled={guardando}>Cancelar</Btn>
            <Btn type="submit" form="formulario-pago" size="md" disabled={guardando}>
              {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
              {guardando ? 'Registrando…' : 'Registrar pago'}
            </Btn>
          </>
        )}
      >
        {pago && (
          <form id="formulario-pago" ref={pagoRef} onSubmit={(e) => { e.preventDefault(); registrarPago(); }} className="space-y-4" noValidate>
            {errorPago && <Alert type="error">{errorPago}</Alert>}
            <div className="grid gap-4 sm:grid-cols-2">
              <Input label="Monto (Bs)" type="number" inputMode="decimal" min="0" step="0.01" value={pago.monto} autoComplete="off"
                error={erroresPago.monto} onChange={(e) => cambiarPago('monto', e.target.value)} />
              <Selector label="Método" value={pago.metodoPago} error={erroresPago.metodoPago}
                opciones={Object.entries(METODOS).map(([valor, texto]) => ({ valor, texto }))}
                onChange={(metodoPago) => cambiarPago('metodoPago', metodoPago)} />
            </div>
            <Input label={esQR ? 'Número de referencia del QR' : 'Referencia (opcional)'} value={pago.referencia} autoComplete="off"
              spellCheck={false} error={erroresPago.referencia}
              ayuda={esQR ? 'Está en el comprobante del banco: sirve para encontrar el pago.' : undefined}
              onChange={(e) => cambiarPago('referencia', e.target.value)} />
            <AreaDeTexto label="Observaciones (opcional)" rows={2} value={pago.observaciones} autoComplete="off"
              error={erroresPago.observaciones} onChange={(e) => cambiarPago('observaciones', e.target.value)} />
            <div>
              <p className="text-sm font-medium text-slate-700 mb-1">Comprobante (opcional)</p>
              {pago.comprobante ? (
                <div className="flex items-center justify-between gap-2 rounded-lg border border-slate-200 px-3 py-1.5 text-sm">
                  <span className="min-w-0 truncate">{pago.comprobante.name}</span>
                  <Btn variant="ghost" onClick={() => cambiarPago('comprobante', null)} aria-label={`Quitar ${pago.comprobante.name}`}>
                    <X size={14} aria-hidden="true" />Quitar
                  </Btn>
                </div>
              ) : (
                <BotonDeFoto onElegir={(archivo) => cambiarPago('comprobante', archivo)} aria-label="Elegir foto del comprobante">
                  Elegir foto
                </BotonDeFoto>
              )}
              <p className="mt-1 text-sm text-slate-600">La foto del comprobante del QR o del recibo. Se sube después de registrar el pago.</p>
            </div>
          </form>
        )}
      </Modal>

      <Modal
        abierto={comprobanteAbierto !== null}
        onCerrar={() => setComprobanteAbierto(null)}
        ancho="lg"
        titulo={comprobanteAbierto ? `Comprobante del pago de ${formatearMonto(comprobanteAbierto.monto)}` : ''}
        descripcion={comprobanteAbierto
          ? `Pago del ${formatearFecha(comprobanteAbierto.fechaPago, { hora: true })}, registrado por ${comprobanteAbierto.registradoPor.nombreCompleto}.`
          : undefined}
        pie={<Btn variant="outline" size="md" onClick={() => setComprobanteAbierto(null)}>Cerrar</Btn>}
      >
        {comprobanteAbierto && (
          <ImagenConSesion evidenciaId={comprobanteAbierto.comprobanteId}
            alt={`Comprobante del pago de ${formatearMonto(comprobanteAbierto.monto)}`}
            className="mx-auto max-h-[65vh] w-auto max-w-full rounded-lg object-contain" />
        )}
      </Modal>

      <Confirmar
        abierto={reemplazo !== null}
        titulo="¿Reemplazar el comprobante?"
        textoAccion="Reemplazar comprobante"
        enviando={subiendoComprobante !== null}
        onConfirmar={() => adjuntarComprobante(reemplazo.pago, reemplazo.archivo)}
        onCancelar={() => setReemplazo(null)}
      >
        {reemplazo && (
          <p>
            El pago de {formatearMonto(reemplazo.pago.monto)} ya tiene un comprobante. Se cambia por «{reemplazo.archivo.name}» y
            el anterior se borra: no se puede deshacer.
          </p>
        )}
      </Confirmar>

      <Confirmar
        abierto={motivo !== null}
        titulo={motivo?.tipo === 'pago'
          ? `¿Anular el pago de ${formatearMonto(motivo.pago.monto)}?`
          : `¿Marcar ${motivo?.cobranza.numero ?? ''} como incobrable?`}
        textoAccion={motivo?.tipo === 'pago' ? 'Anular pago' : 'Marcar incobrable'}
        enviando={enviando}
        error={motivo?.error}
        onConfirmar={enviarMotivo}
        onCancelar={() => setMotivo(null)}
      >
        {motivo && (
          <div ref={motivoRef} className="space-y-3">
            <p>
              {motivo.tipo === 'pago'
                ? `Deja de contar: ${motivo.cobranza.numero} vuelve a deber ese monto, y el saldo del cliente también sube. El pago queda en el historial, marcado como anulado.`
                : `Deja de contar en el saldo de ${motivo.cobranza.cliente.nombreCompleto}: ya no se espera cobrarla. Sus pagos se conservan.`}
            </p>
            <AreaDeTexto label="Motivo" value={motivo.texto} error={motivo.errorCampo} autoComplete="off"
              placeholder={motivo.tipo === 'pago' ? 'Por qué se anula el pago…' : 'Por qué ya no se puede cobrar…'}
              onChange={(e) => setMotivo({ ...motivo, texto: e.target.value, errorCampo: '' })} />
          </div>
        )}
      </Confirmar>
    </Layout>
  );
}
