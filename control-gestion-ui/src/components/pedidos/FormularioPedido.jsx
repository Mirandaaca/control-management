import { useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { LoaderCircle } from 'lucide-react';
import api from '../../api/axios';
import { erroresPorCampo, mensajeDeError } from '../../api/errores';
import LineasDeProductos from '../LineasDeProductos';
import { Alert, AreaDeTexto, Btn, CampoFechaHora, Combobox, Input, Modal } from '../ui';
import { LIMITE_DE_CLIENTES, useOpcionesDeClientes } from '../../hooks/useOpcionesDeClientes';
import { mostrarPrimerProblema } from '../../utils/foco';
import { aCampoFechaHora, ahoraEnBolivia, formatearMonto } from '../../utils/formato';
import { totalDe } from '../../utils/lineas';

// Errores de la API que pertenecen a un campo del formulario: se muestran junto a él.
const CAMPO_DEL_ERROR = {
  cliente_no_existe: 'clienteId',
  cliente_inactivo: 'clienteId',
  fecha_en_el_pasado: 'fechaEntregaEstimada',
};

let siguienteClave = 1;
const nuevaLinea = (linea = {}) => ({
  clave: siguienteClave++,
  productoId: linea.productoId != null ? String(linea.productoId) : '',
  cantidad: linea.cantidad != null ? String(linea.cantidad) : '1',
  descuento: linea.descuento ? String(linea.descuento) : '',
});

function datosIniciales(pedido) {
  if (!pedido) return { clienteId: '', fechaEntregaEstimada: '', direccionEntrega: '', descuento: '', observaciones: '', lineas: [nuevaLinea()] };
  return {
    clienteId: String(pedido.cliente.id),
    fechaEntregaEstimada: aCampoFechaHora(pedido.fechaEntregaEstimada),
    direccionEntrega: pedido.direccionEntrega,
    descuento: pedido.descuento ? String(pedido.descuento) : '',
    observaciones: pedido.observaciones ?? '',
    lineas: pedido.detalles.map((d) => nuevaLinea(d)),
  };
}

/**
 * Nuevo pedido o editar uno (1.1). Se monta cada vez que se abre, así empieza limpio. La zona y el promotor no se
 * eligen: son los del cliente. Al elegir el cliente se propone su dirección como dirección de entrega.
 */
export default function FormularioPedido({ pedido, onCerrar, onGuardado }) {
  const esNuevo = !pedido;
  const [datos, setDatos] = useState(() => datosIniciales(pedido));
  const clientes = useOpcionesDeClientes();
  const [productos, setProductos] = useState([]);
  const [errores, setErrores] = useState({});
  const [errorFormulario, setErrorFormulario] = useState('');
  const [guardando, setGuardando] = useState(false);
  const formularioRef = useRef(null);
  // La dirección que se propuso del cliente: si nadie la cambió, se reemplaza al elegir otro cliente.
  const direccionPropuesta = useRef('');

  useEffect(() => {
    api.get('/productos/opciones')
      .then(({ data }) => setProductos(data))
      .catch((err) => setErrorFormulario(mensajeDeError(err, 'No se pudieron cargar los productos.')));
  }, []);

  // El error de un campo se borra en cuanto se cambia.
  const quitarError = (campo) =>
    setErrores((e) => Object.fromEntries(Object.entries(e).filter(([clave]) => clave !== campo)));

  const cambiar = (campo, valor) => {
    setDatos((d) => ({ ...d, [campo]: valor }));
    quitarError(campo);
  };

  const elegirCliente = (clienteId, opcion) => {
    const direccion = opcion?.cliente.direccionTexto ?? '';
    setDatos((d) => {
      const propuesta = d.direccionEntrega === '' || d.direccionEntrega === direccionPropuesta.current;
      return { ...d, clienteId, direccionEntrega: propuesta ? direccion : d.direccionEntrega };
    });
    direccionPropuesta.current = direccion;
    quitarError('clienteId');
    quitarError('direccionEntrega');
  };

  const cambiarLinea = (clave, indice, cambios) => {
    setDatos((d) => ({ ...d, lineas: d.lineas.map((l) => (l.clave === clave ? { ...l, ...cambios } : l)) }));
    Object.keys(cambios).forEach((campo) => quitarError(`detalles[${indice}].${campo}`));
  };

  const guardar = async () => {
    setGuardando(true);
    setErrores({});
    setErrorFormulario('');
    const cuerpo = {
      fechaEntregaEstimada: datos.fechaEntregaEstimada || null,
      direccionEntrega: datos.direccionEntrega,
      descuento: Number(datos.descuento) || 0,
      observaciones: datos.observaciones || null,
      detalles: datos.lineas.map((l) => ({
        productoId: Number(l.productoId) || 0,
        cantidad: Number(l.cantidad) || 0,
        descuento: Number(l.descuento) || 0,
      })),
    };
    try {
      const { data } = esNuevo
        ? await api.post('/pedidos', { ...cuerpo, clienteId: Number(datos.clienteId) || 0 })
        : await api.put(`/pedidos/${pedido.id}`, cuerpo);
      onGuardado(data, esNuevo);
    } catch (err) {
      const { codigo, detail } = err.response?.data ?? {};
      flushSync(() => {
        const campos = erroresPorCampo(err);
        if (CAMPO_DEL_ERROR[codigo]) campos[CAMPO_DEL_ERROR[codigo]] = detail;
        setErrores(campos);
        if (Object.keys(campos).length === 0) setErrorFormulario(mensajeDeError(err, 'No se pudo guardar el pedido.'));
      });
      mostrarPrimerProblema(formularioRef.current);
    } finally {
      setGuardando(false);
    }
  };

  const total = totalDe(datos.lineas, productos, datos.descuento);

  return (
    <Modal
      abierto
      onCerrar={onCerrar}
      ancho="lg"
      titulo={esNuevo ? 'Nuevo pedido' : `Editar ${pedido.numero}`}
      descripcion={esNuevo
        ? 'La zona y el promotor son los del cliente. El stock se descuenta recién al entregarlo.'
        : 'Se modifica mientras está pendiente o en proceso. El cliente no cambia: para otro, anula este pedido y registra uno nuevo.'}
      pie={(
        <>
          <Btn variant="outline" size="md" onClick={onCerrar} disabled={guardando}>Cancelar</Btn>
          <Btn type="submit" form="formulario-pedido" size="md" disabled={guardando}>
            {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
            {guardando && 'Guardando…'}
            {!guardando && (esNuevo ? 'Registrar pedido' : 'Guardar cambios')}
          </Btn>
        </>
      )}
    >
      <form id="formulario-pedido" ref={formularioRef} onSubmit={(e) => { e.preventDefault(); guardar(); }} className="space-y-4" noValidate>
        {errorFormulario && <Alert type="error">{errorFormulario}</Alert>}
        <div className="grid gap-4 md:grid-cols-2">
          {esNuevo ? (
            <Combobox label="Cliente" placeholder="Elige el cliente" placeholderBusqueda="Nombre, código o celular…"
              value={datos.clienteId} error={errores.clienteId}
              opciones={clientes.opciones} onBuscar={clientes.buscar} cargando={clientes.cargando} errorBusqueda={clientes.error}
              limite={LIMITE_DE_CLIENTES} onChange={elegirCliente} />
          ) : (
            <Input label="Cliente" value={pedido.cliente.nombreCompleto} disabled readOnly />
          )}
          <CampoFechaHora label="Fecha y hora de entrega" value={datos.fechaEntregaEstimada}
            min={ahoraEnBolivia()} error={errores.fechaEntregaEstimada} ayuda="En la hora de Bolivia."
            onChange={(e) => cambiar('fechaEntregaEstimada', e.target.value)} />
        </div>
        <Input label="Dirección de entrega" value={datos.direccionEntrega} autoComplete="off" maxLength={300}
          placeholder="Calle, número y una referencia…" error={errores.direccionEntrega}
          ayuda={esNuevo ? 'Se propone la del cliente: cámbiala si la entrega es en otro lugar.' : undefined}
          onChange={(e) => cambiar('direccionEntrega', e.target.value)} />

        <LineasDeProductos
          lineas={datos.lineas}
          productos={productos}
          errores={errores}
          sinStock="con esta cantidad no se podrá entregar."
          onCambiar={cambiarLinea}
          onQuitar={(clave) => setDatos((d) => ({ ...d, lineas: d.lineas.filter((l) => l.clave !== clave) }))}
          onAgregar={() => setDatos((d) => ({ ...d, lineas: [...d.lineas, nuevaLinea()] }))}
        />

        <div className="grid gap-4 sm:grid-cols-2 sm:items-end">
          <Input label="Descuento sobre el total (Bs, opcional)" type="number" inputMode="decimal" min="0" step="0.01"
            value={datos.descuento} placeholder="0,00" autoComplete="off" error={errores.descuento}
            onChange={(e) => cambiar('descuento', e.target.value)} />
          <p className="text-right text-base font-semibold text-slate-900 tabular-nums sm:pb-2" aria-live="polite">
            Total: {formatearMonto(total)}
          </p>
        </div>
        <AreaDeTexto label="Observaciones (opcional)" value={datos.observaciones} autoComplete="off" maxLength={500}
          placeholder="Por ejemplo, a qué hora atienden…" error={errores.observaciones}
          onChange={(e) => cambiar('observaciones', e.target.value)} />
      </form>
    </Modal>
  );
}
