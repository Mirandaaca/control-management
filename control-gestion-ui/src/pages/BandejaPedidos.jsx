import { useCallback, useEffect, useState } from 'react';
import { Eye, Inbox, Truck } from 'lucide-react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import DetallePedido from '../components/pedidos/DetallePedido';
import EntregarPedido from '../components/pedidos/EntregarPedido';
import EstadoPedido from '../components/pedidos/EstadoPedido';
import { EN_RUTA } from '../components/pedidos/estados';
import { Alert, Btn, Cargando, ErrorDeCarga, EstadoVacio, Input, PageHeader } from '../components/ui';
import { useAuth } from '../context/auth';
import { PERMISOS } from '../context/permisos';
import { diaEnBolivia, formatearDia, formatearHora, formatearMonto } from '../utils/formato';

/**
 * La bandeja del día (1.3): los pedidos que el promotor tiene asignados para entregar ese día, por hora. Desde aquí
 * registra la entrega de los que ya están en ruta. En la app será su pantalla de reparto.
 */
export default function BandejaPedidos() {
  const { tiene } = useAuth();
  const puedeEntregar = tiene(PERMISOS.pedidos.registrarEntrega);

  const [dia, setDia] = useState(diaEnBolivia());
  const [pedidos, setPedidos] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [aviso, setAviso] = useState('');
  const [errorAccion, setErrorAccion] = useState('');
  const [detalle, setDetalle] = useState(null);
  const [entregando, setEntregando] = useState(null);

  const cargar = useCallback(() => api.get('/pedidos/bandeja', { params: { dia: dia || undefined } })
    .then(({ data }) => { setPedidos(data); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la bandeja.')))
    .finally(() => { setCargando(false); setReintentando(false); }),
  [dia]);

  useEffect(() => { cargar(); }, [cargar]);

  const cambiarDia = (valor) => { setDia(valor); setCargando(true); setAviso(''); };

  const verDetalle = (id) => {
    setErrorAccion('');
    api.get(`/pedidos/${id}`)
      .then(({ data }) => setDetalle(data))
      .catch((err) => setErrorAccion(mensajeDeError(err, 'No se pudo abrir el pedido.')));
  };

  const abrirEntrega = (pedido) => { setAviso(''); setDetalle(null); setEntregando(pedido); };

  const alEntregar = (mensaje) => {
    setEntregando(null);
    setAviso(mensaje);
    cargar();
  };

  const puedeEntregarlo = (pedido) => puedeEntregar && pedido.estado.id === EN_RUTA;
  const esHoy = dia === diaEnBolivia();

  let estado = null;
  if (cargando) estado = <Cargando texto="Cargando la bandeja…" />;
  else if (errorCarga && !pedidos) estado = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
  );
  else if (pedidos?.length === 0) estado = (
    <EstadoVacio titulo={esHoy ? 'No tienes entregas para hoy' : `No tienes entregas para el ${formatearDia(dia)}`} icono={Inbox}>
      Aquí aparecen los pedidos que la secretaria te asigna para ese día, menos los entregados y los anulados.
    </EstadoVacio>
  );

  return (
    <Layout>
      <PageHeader title="Bandeja del día" subtitle="Los pedidos que tienes asignados para entregar, por hora de entrega." />

      {aviso && <Alert type="success" className="mb-4">{aviso}</Alert>}
      {errorAccion && <Alert type="error" className="mb-4">{errorAccion}</Alert>}
      {errorCarga && pedidos && <Alert type="error" className="mb-4">{errorCarga}</Alert>}

      <Table
        headers={['Hora', 'Número', 'Cliente', 'Dirección de entrega', { texto: 'Total', derecha: true }, 'Estado', '']}
        estado={estado}
        barra={(
          <div className="flex flex-col gap-3 p-4 border-b border-slate-200 sm:flex-row sm:items-end sm:justify-between">
            <div className="w-full sm:w-44">
              <Input label="Día" type="date" autoComplete="off" value={dia} onChange={(e) => cambiarDia(e.target.value)} />
            </div>
            {pedidos && !cargando && (
              <p className="text-sm text-slate-600 sm:h-10 sm:flex sm:items-center" aria-live="polite">
                {pedidos.length === 1 ? '1 entrega' : `${pedidos.length} entregas`}
              </p>
            )}
          </div>
        )}
      >
        {(pedidos ?? []).map((p) => (
          <Tr key={p.id}>
            <Td className="font-semibold tabular-nums whitespace-nowrap">{formatearHora(p.fechaEntregaEstimada)}</Td>
            <Td mono>{p.numero}</Td>
            <Td><span className="font-medium">{p.cliente.nombreCompleto}</span></Td>
            <Td className="min-w-48">{p.direccionEntrega}</Td>
            <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(p.total)}</Td>
            <Td><EstadoPedido estado={p.estado} /></Td>
            <Td>
              <div className="flex items-center justify-end gap-1">
                <Btn variant="ghost" onClick={() => verDetalle(p.id)} aria-label={`Ver ${p.numero}`}>
                  <Eye size={14} aria-hidden="true" />
                  Ver
                </Btn>
                {puedeEntregarlo(p) && (
                  <Btn variant="ghost" onClick={() => abrirEntrega(p)} aria-label={`Entregar ${p.numero}`}>
                    <Truck size={14} aria-hidden="true" />
                    Entregar
                  </Btn>
                )}
              </div>
            </Td>
          </Tr>
        ))}
      </Table>

      <DetallePedido
        pedido={detalle}
        onCerrar={() => setDetalle(null)}
        acciones={detalle && puedeEntregarlo(detalle) && (
          <Btn size="md" onClick={() => abrirEntrega(detalle)}>
            <Truck size={16} aria-hidden="true" />Registrar entrega
          </Btn>
        )}
      />

      {entregando && <EntregarPedido pedido={entregando} onCerrar={() => setEntregando(null)} onHecho={alEntregar} />}
    </Layout>
  );
}
