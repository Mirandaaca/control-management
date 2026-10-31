import { useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { LoaderCircle } from 'lucide-react';
import api from '../../api/axios';
import { erroresPorCampo, mensajeDeError } from '../../api/errores';
import { Alert, AreaDeTexto, BotonCancelar, Btn, Combobox, Modal, Selector } from '../ui';
import { LIMITE_DE_CLIENTES, useOpcionesDeClientes } from '../../hooks/useOpcionesDeClientes';
import { useSinGuardar } from '../../hooks/useSinGuardar';
import { mostrarPrimerProblema } from '../../utils/foco';
import { formatearFecha } from '../../utils/formato';

// Errores de la API que pertenecen a un campo del formulario: se muestran junto a él.
const CAMPO_DEL_ERROR = {
  tipo_no_valido: 'tipoEventoId',
  cliente_no_existe: 'clienteId',
  pedido_no_existe: 'pedidoId',
  cliente_no_coincide: 'pedidoId',
};

function datosIniciales(evento) {
  return {
    tipoEventoId: String(evento.tipo.id),
    clienteId: evento.cliente ? String(evento.cliente.id) : '',
    pedidoId: evento.pedido ? String(evento.pedido.id) : '',
    descripcion: evento.descripcion,
  };
}

/**
 * Editar un evento (4.1; 7.3.13, paso 3). Se monta cada vez que se abre, así empieza limpio. La ubicación es la del
 * registro y no cambia. El evento se registra en la app, con el GPS del celular (7.3.13, paso 2; tarea 15). El pedido
 * es opcional y se elige entre los del cliente.
 */
export default function FormularioEvento({ evento, puedeVerPedidos, onCerrar, onGuardado }) {
  const [datos, setDatos] = useState(() => datosIniciales(evento));
  const sinGuardar = useSinGuardar(datos);
  const [tipos, setTipos] = useState([]);
  const clientes = useOpcionesDeClientes();
  const [pedidos, setPedidos] = useState([]);
  const [errores, setErrores] = useState({});
  const [errorFormulario, setErrorFormulario] = useState('');
  const [guardando, setGuardando] = useState(false);
  const formularioRef = useRef(null);

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
    try {
      const { data } = await api.put(`/eventos/${evento.id}`, {
        tipoEventoId: Number(datos.tipoEventoId) || 0,
        clienteId: datos.clienteId ? Number(datos.clienteId) : null,
        pedidoId: datos.pedidoId ? Number(datos.pedidoId) : null,
        descripcion: datos.descripcion,
      });
      onGuardado(data);
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

  // El cliente o el pedido del evento pueden no estar entre las opciones (un cliente que pasó a inactivo, un pedido de
  // otra cartera): el cliente se muestra con textoInicial y el pedido se agrega a la lista.
  const opcionesDePedido = pedidos.map((p) => ({
    valor: String(p.id), texto: `${p.numero} · ${p.estado.nombre} · entrega ${formatearFecha(p.fechaEntregaEstimada)}`,
  }));
  if (evento.pedido && String(evento.cliente?.id) === datos.clienteId && !pedidos.some((p) => p.id === evento.pedido.id))
    opcionesDePedido.unshift({ valor: String(evento.pedido.id), texto: evento.pedido.numero });
  const tipoElegido = tipos.find((t) => String(t.id) === datos.tipoEventoId);

  return (
    <Modal
      abierto
      onCerrar={onCerrar}
      sinGuardar={sinGuardar}
      ancho="lg"
      titulo={`Editar ${evento.numero}`}
      descripcion="Se modifica mientras está pendiente. La ubicación es la del registro y no cambia."
      pie={(
        <>
          <BotonCancelar disabled={guardando} />
          <Btn type="submit" form="formulario-evento" size="md" disabled={guardando}>
            {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
            {guardando ? 'Guardando…' : 'Guardar cambios'}
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
            value={datos.clienteId} error={errores.clienteId} textoInicial={evento.cliente?.nombreCompleto}
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
      </form>
    </Modal>
  );
}
