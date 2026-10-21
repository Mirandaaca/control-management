import { useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import api from '../../api/axios';
import { erroresPorCampo, mensajeDeError } from '../../api/errores';
import CampoUbicacion from '../CampoUbicacion';
import { AreaDeTexto, Confirmar, Input, Selector } from '../ui';
import { useUbicacionDelNavegador } from '../../hooks/useUbicacionDelNavegador';
import { mostrarPrimerProblema } from '../../utils/foco';
import { formatearMonto } from '../../utils/formato';
import { CONTADO, CREDITO } from './estados';

const TIPOS = [{ valor: String(CONTADO), texto: 'Al contado' }, { valor: String(CREDITO), texto: 'A crédito' }];

/**
 * Registrar la entrega (1.2). La entrega es la venta: la API crea y confirma una HP con los productos del pedido,
 * descuenta el stock y, si es a crédito, crea la deuda. No se deshace, por eso se confirma con la acción primaria.
 * Se monta al abrirse y pide la ubicación al navegador, como haría el GPS en la app.
 */
export default function EntregarPedido({ pedido, onCerrar, onHecho }) {
  const [recibidoPor, setRecibidoPor] = useState('');
  const [tipoVenta, setTipoVenta] = useState('');
  const [observacion, setObservacion] = useState('');
  const [ubicacion, setUbicacion] = useState(null);
  const [precision, setPrecision] = useState(null);
  const [errores, setErrores] = useState({});
  const [error, setError] = useState('');
  const [enviando, setEnviando] = useState(false);
  const contenedorRef = useRef(null);
  const gps = useUbicacionDelNavegador();

  const quitarError = (campo) => setErrores((e) => ({ ...e, [campo]: undefined }));

  const tomarUbicacion = gps.tomar;
  const usarMiUbicacion = () => tomarUbicacion({
    onUbicacion: (punto, metros) => { setUbicacion(punto); setPrecision(metros); quitarError('latitud'); },
    onError: (mensaje) => setErrores((e) => ({ ...e, latitud: mensaje })),
  });

  // Como el GPS en la app: la ubicación se pide sola al abrir.
  useEffect(() => {
    tomarUbicacion({
      onUbicacion: (punto, metros) => { setUbicacion(punto); setPrecision(metros); },
      onError: (mensaje) => setErrores((e) => ({ ...e, latitud: mensaje })),
    });
  }, [tomarUbicacion]);

  const entregar = async () => {
    setEnviando(true);
    setErrores({});
    setError('');
    try {
      const { data } = await api.post(`/pedidos/${pedido.id}/entregar`, {
        recibidoPor,
        tipoVenta: tipoVenta === '' ? null : Number(tipoVenta),
        latitud: ubicacion?.latitud ?? null,
        longitud: ubicacion?.longitud ?? null,
        precisionGPS: precision,
        observacion: observacion || null,
      });
      const hp = data.hojaPedido;
      onHecho(hp.cobranza
        ? `${data.numero} quedó entregado: nació la venta ${hp.numero}, se descontó el stock y nació la deuda ${hp.cobranza.numero}.`
        : `${data.numero} quedó entregado: nació la venta ${hp.numero} al contado y se descontó el stock.`);
    } catch (err) {
      flushSync(() => {
        const campos = erroresPorCampo(err);
        setErrores(campos);
        if (!campos.recibidoPor && !campos.tipoVenta && !campos.latitud)
          setError(mensajeDeError(err, 'No se pudo registrar la entrega.'));
      });
      mostrarPrimerProblema(contenedorRef.current);
    } finally {
      setEnviando(false);
    }
  };

  const aCredito = tipoVenta === String(CREDITO);

  return (
    <Confirmar
      abierto
      ancho="lg"
      titulo={`¿Registrar la entrega de ${pedido.numero}?`}
      textoAccion="Registrar entrega"
      variante="primary"
      enviando={enviando}
      error={error}
      onConfirmar={entregar}
      onCancelar={onCerrar}
    >
      <div ref={contenedorRef} className="space-y-4">
        <p>
          Entrega de <span className="font-semibold tabular-nums">{formatearMonto(pedido.total)}</span> a {pedido.cliente.nombreCompleto},
          en {pedido.direccionEntrega}. Se crea y se confirma la venta (HP) con los productos del pedido y se descuenta el
          stock{aCredito ? '; como es a crédito, nace la deuda, que vence en 30 días' : ''}. No se puede deshacer: si hay un
          error, la secretaria tendrá que anular la HP.
        </p>
        <div className="grid gap-4 sm:grid-cols-2">
          <Input label="Quién recibió" value={recibidoPor} autoComplete="off" maxLength={150} placeholder="Nombre de quien recibió…"
            error={errores.recibidoPor} onChange={(e) => { setRecibidoPor(e.target.value); quitarError('recibidoPor'); }} />
          <Selector label="Tipo de venta" placeholder="Elige contado o crédito" value={tipoVenta} opciones={TIPOS}
            error={errores.tipoVenta} onChange={(valor) => { setTipoVenta(valor); quitarError('tipoVenta'); }} />
        </div>
        <CampoUbicacion
          titulo="Dónde se entregó"
          valor={ubicacion}
          precision={precision}
          buscando={gps.buscando}
          error={errores.latitud}
          etiquetaMapa="Mapa: haz clic donde se entregó el pedido para marcar la ubicación"
          sinPunto="Haz clic en el mapa donde se entregó el pedido."
          onMarcar={(punto) => { setUbicacion(punto); setPrecision(null); quitarError('latitud'); }}
          onUsarMiUbicacion={usarMiUbicacion}
        />
        <AreaDeTexto label="Observación (opcional)" value={observacion} autoComplete="off" maxLength={500}
          placeholder="Por ejemplo, dónde se dejó la mercadería…" error={errores.observacion}
          onChange={(e) => setObservacion(e.target.value)} />
      </div>
    </Confirmar>
  );
}
