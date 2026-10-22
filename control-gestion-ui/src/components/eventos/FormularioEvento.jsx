import { useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { LoaderCircle, X } from 'lucide-react';
import api from '../../api/axios';
import { erroresPorCampo, mensajeDeError } from '../../api/errores';
import { subirFoto } from '../../api/fotos';
import BotonDeFoto from '../BotonDeFoto';
import CampoUbicacion from '../CampoUbicacion';
import { Alert, AreaDeTexto, Btn, Combobox, Modal, Selector } from '../ui';
import { LIMITE_DE_CLIENTES, useOpcionesDeClientes } from '../../hooks/useOpcionesDeClientes';
import { useUbicacionDelNavegador } from '../../hooks/useUbicacionDelNavegador';
import { mostrarPrimerProblema } from '../../utils/foco';
import { formatearFecha } from '../../utils/formato';
import { MAXIMO_DE_FOTOS } from './estados';

// Errores de la API que pertenecen a un campo del formulario: se muestran junto a él.
const CAMPO_DEL_ERROR = {
  tipo_no_valido: 'tipoEventoId',
  cliente_no_existe: 'clienteId',
  pedido_no_existe: 'pedidoId',
  cliente_no_coincide: 'pedidoId',
};

function datosIniciales(evento) {
  return {
    tipoEventoId: evento ? String(evento.tipo.id) : '',
    clienteId: evento?.cliente ? String(evento.cliente.id) : '',
    pedidoId: evento?.pedido ? String(evento.pedido.id) : '',
    descripcion: evento?.descripcion ?? '',
  };
}

/**
 * Nuevo evento o editar uno (4.1). Se monta cada vez que se abre, así empieza limpio. Al registrar, la ubicación se pide
 * sola al navegador, como haría el GPS en la app, o se marca en el mapa; al editar no cambia. El pedido es opcional y
 * se elige entre los del cliente. Las fotos elegidas se suben después de registrarlo: si alguna falla, el evento queda
 * registrado igual y onGuardado recibe cuáles fallaron, para agregarlas desde el detalle.
 */
export default function FormularioEvento({ evento, puedeVerPedidos, puedeAgregarFotos, onCerrar, onGuardado }) {
  const esNuevo = !evento;
  const [datos, setDatos] = useState(() => datosIniciales(evento));
  const [tipos, setTipos] = useState([]);
  const clientes = useOpcionesDeClientes();
  const [pedidos, setPedidos] = useState([]);
  const [ubicacion, setUbicacion] = useState(null);
  const [precision, setPrecision] = useState(null);
  const [fotos, setFotos] = useState([]);
  const [errores, setErrores] = useState({});
  const [errorFormulario, setErrorFormulario] = useState('');
  const [guardando, setGuardando] = useState(false);
  const formularioRef = useRef(null);
  const gps = useUbicacionDelNavegador();

  // El error de un campo se borra en cuanto se cambia.
  const quitarError = (...campos) =>
    setErrores((e) => Object.fromEntries(Object.entries(e).filter(([clave]) => !campos.includes(clave))));

  useEffect(() => {
    api.get('/eventos/tipos')
      .then(({ data }) => setTipos(data))
      .catch((err) => setErrorFormulario(mensajeDeError(err, 'No se pudieron cargar los tipos de evento.')));
  }, []);

  // Los pedidos del cliente elegido. Si se cambia de cliente antes de que llegue la respuesta, se descarta.
  useEffect(() => {
    if (!puedeVerPedidos || !datos.clienteId) return undefined;
    let vigente = true;
    api.get('/pedidos', { params: { clienteId: datos.clienteId, tamano: 100 } })
      .then(({ data }) => { if (vigente) setPedidos(data.items); })
      .catch((err) => { if (vigente) setErrorFormulario(mensajeDeError(err, 'No se pudieron cargar los pedidos del cliente.')); });
    return () => { vigente = false; };
  }, [puedeVerPedidos, datos.clienteId]);

  // Como el GPS en la app: al registrar, la ubicación se pide sola al abrir.
  const tomarUbicacion = gps.tomar;
  useEffect(() => {
    if (!esNuevo) return;
    tomarUbicacion({
      onUbicacion: (punto, metros) => { setUbicacion(punto); setPrecision(metros); },
      onError: (mensaje) => setErrores((e) => ({ ...e, latitud: mensaje })),
    });
  }, [esNuevo, tomarUbicacion]);

  const usarMiUbicacion = () => tomarUbicacion({
    onUbicacion: (punto, metros) => { setUbicacion(punto); setPrecision(metros); quitarError('latitud', 'longitud'); },
    onError: (mensaje) => setErrores((e) => ({ ...e, latitud: mensaje })),
  });

  const cambiar = (campo, valor) => {
    setDatos((d) => ({ ...d, [campo]: valor }));
    quitarError(campo);
  };

  const elegirCliente = (clienteId) => {
    setDatos((d) => ({ ...d, clienteId, pedidoId: '' }));
    setPedidos([]);
    quitarError('clienteId', 'pedidoId');
  };

  const guardar = async () => {
    setGuardando(true);
    setErrores({});
    setErrorFormulario('');
    const cuerpo = {
      tipoEventoId: Number(datos.tipoEventoId) || 0,
      clienteId: datos.clienteId ? Number(datos.clienteId) : null,
      pedidoId: datos.pedidoId ? Number(datos.pedidoId) : null,
      descripcion: datos.descripcion,
    };
    try {
      const { data } = esNuevo
        ? await api.post('/eventos', { ...cuerpo, latitud: ubicacion?.latitud ?? null, longitud: ubicacion?.longitud ?? null })
        : await api.put(`/eventos/${evento.id}`, cuerpo);
      const fallidas = [];
      for (const foto of fotos) {
        try {
          await subirFoto(data.id, foto);
        } catch (err) {
          fallidas.push(`${foto.name}: ${mensajeDeError(err, 'no se pudo subir.')}`);
        }
      }
      onGuardado(data, esNuevo, fallidas);
    } catch (err) {
      const { codigo, detail } = err.response?.data ?? {};
      flushSync(() => {
        const campos = erroresPorCampo(err);
        if (CAMPO_DEL_ERROR[codigo]) campos[CAMPO_DEL_ERROR[codigo]] = detail;
        setErrores(campos);
        if (Object.keys(campos).length === 0) setErrorFormulario(mensajeDeError(err, 'No se pudo guardar el evento.'));
      });
      mostrarPrimerProblema(formularioRef.current);
    } finally {
      setGuardando(false);
    }
  };

  // Al editar, el cliente o el pedido del evento pueden no estar entre las opciones (un cliente que pasó a inactivo, un
  // pedido de otra cartera): el cliente se muestra con textoInicial y el pedido se agrega a la lista.
  const opcionesDePedido = pedidos.map((p) => ({
    valor: String(p.id), texto: `${p.numero} · ${p.estado.nombre} · entrega ${formatearFecha(p.fechaEntregaEstimada)}`,
  }));
  if (evento?.pedido && String(evento.cliente?.id) === datos.clienteId && !pedidos.some((p) => p.id === evento.pedido.id))
    opcionesDePedido.unshift({ valor: String(evento.pedido.id), texto: evento.pedido.numero });
  const tipoElegido = tipos.find((t) => String(t.id) === datos.tipoEventoId);

  return (
    <Modal
      abierto
      onCerrar={onCerrar}
      ancho="lg"
      titulo={esNuevo ? 'Nuevo evento' : `Editar ${evento.numero}`}
      descripcion={esNuevo
        ? 'Una incidencia en campo, de un cliente o de uno de sus pedidos. Queda pendiente hasta que la secretaria la resuelva.'
        : 'Se modifica mientras está pendiente. La ubicación es la del registro y no cambia.'}
      pie={(
        <>
          <Btn variant="outline" size="md" onClick={onCerrar} disabled={guardando}>Cancelar</Btn>
          <Btn type="submit" form="formulario-evento" size="md" disabled={guardando}>
            {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
            {guardando && 'Guardando…'}
            {!guardando && (esNuevo ? 'Registrar evento' : 'Guardar cambios')}
          </Btn>
        </>
      )}
    >
      <form id="formulario-evento" ref={formularioRef} onSubmit={(e) => { e.preventDefault(); guardar(); }} className="space-y-4" noValidate>
        {errorFormulario && <Alert type="error">{errorFormulario}</Alert>}
        <Selector label="Tipo de evento" placeholder="Elige el tipo" value={datos.tipoEventoId} error={errores.tipoEventoId}
          opciones={tipos.map((t) => ({ valor: String(t.id), texto: t.requiereEvidencia ? `${t.nombre} · pide foto` : t.nombre }))}
          ayuda={tipoElegido?.requiereEvidencia ? 'Este tipo se resuelve con al menos una foto: agrégala ahora o después.' : undefined}
          onChange={(valor) => cambiar('tipoEventoId', valor)} />
        <div className="grid gap-4 md:grid-cols-2">
          <Combobox label="Cliente" placeholder="Elige el cliente" placeholderBusqueda="Nombre, código o celular…"
            value={datos.clienteId} error={errores.clienteId} textoInicial={evento?.cliente?.nombreCompleto}
            opciones={clientes.opciones} onBuscar={clientes.buscar} cargando={clientes.cargando} errorBusqueda={clientes.error}
            limite={LIMITE_DE_CLIENTES} onChange={elegirCliente} />
          {puedeVerPedidos && (
            <Selector label="Pedido (opcional)" todos="Sin pedido" value={datos.pedidoId} error={errores.pedidoId}
              opciones={opcionesDePedido} disabled={!datos.clienteId}
              ayuda={datos.clienteId ? undefined : 'Primero elige el cliente.'}
              onChange={(valor) => cambiar('pedidoId', valor)} />
          )}
        </div>
        <AreaDeTexto label="Qué pasó" rows={4} value={datos.descripcion} autoComplete="off" maxLength={1000}
          placeholder="Por ejemplo: el local estaba cerrado y nadie atendió…" error={errores.descripcion}
          onChange={(e) => cambiar('descripcion', e.target.value)} />
        {esNuevo && puedeAgregarFotos && (
          <div>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="text-sm font-medium text-slate-700">
                Fotos (opcional) <span className="font-normal text-slate-600 tabular-nums">· {fotos.length} de {MAXIMO_DE_FOTOS}</span>
              </p>
              <BotonDeFoto disabled={fotos.length >= MAXIMO_DE_FOTOS} onElegir={(foto) => setFotos((f) => [...f, foto])}
                aria-label="Elegir foto del evento">
                Elegir foto
              </BotonDeFoto>
            </div>
            {fotos.length > 0 && (
              <ul className="mt-2 divide-y divide-slate-200 rounded-lg border border-slate-200">
                {fotos.map((foto, i) => (
                  <li key={`${foto.name}-${i}`} className="flex items-center justify-between gap-2 px-3 py-1.5 text-sm">
                    <span className="min-w-0 truncate">{foto.name}</span>
                    <Btn variant="ghost" aria-label={`Quitar ${foto.name}`}
                      onClick={() => setFotos((f) => f.filter((_, j) => j !== i))}>
                      <X size={14} aria-hidden="true" />Quitar
                    </Btn>
                  </li>
                ))}
              </ul>
            )}
            <p className="mt-1 text-sm text-slate-600">JPEG, PNG o WebP. Se suben al registrar el evento.</p>
          </div>
        )}
        {esNuevo && (
          <CampoUbicacion
            titulo="Dónde pasó"
            valor={ubicacion}
            precision={precision}
            buscando={gps.buscando}
            error={errores.latitud ?? errores.longitud}
            etiquetaMapa="Mapa: haz clic donde pasó el evento para marcar la ubicación"
            sinPunto="Haz clic en el mapa donde pasó el evento."
            onMarcar={(punto) => { setUbicacion(punto); setPrecision(null); quitarError('latitud', 'longitud'); }}
            onUsarMiUbicacion={usarMiUbicacion}
          />
        )}
      </form>
    </Modal>
  );
}
