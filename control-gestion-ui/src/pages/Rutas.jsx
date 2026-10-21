import { useEffect, useState } from 'react';
import { MapContainer, TileLayer, Marker, Popup, Polyline, useMap } from 'react-leaflet';
import L from 'leaflet';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { PageHeader, Badge, Btn, Input, Card, Alert } from '../components/ui';
import { Table, Td, Tr } from '../components/Table';
import { formatearDia, formatearHora } from '../utils/formato';

// Fix leaflet default icon
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
});

const iconVisitado = new L.Icon({
  iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-green.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  iconSize: [25, 41], iconAnchor: [12, 41], popupAnchor: [1, -34],
});
const iconAusente = new L.Icon({
  iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-red.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  iconSize: [25, 41], iconAnchor: [12, 41], popupAnchor: [1, -34],
});
const iconPendiente = new L.Icon({
  iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-blue.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  iconSize: [25, 41], iconAnchor: [12, 41], popupAnchor: [1, -34],
});

const ICON_MAP = { 0: iconPendiente, 1: iconVisitado, 2: iconAusente, 3: iconPendiente };

const ESTADO_RUTA_COLOR = { 0: 'gray', 1: 'blue', 2: 'green', 3: 'red' };
const ESTADO_RUTA_LABEL = { 0: 'Planificada', 1: 'En Curso', 2: 'Completada', 3: 'Cancelada' };
const ESTADO_VISITA_COLOR = { 0: 'gray', 1: 'green', 2: 'red', 3: 'yellow' };
const ESTADO_VISITA_LABEL = { 0: 'Pendiente', 1: 'Visitado', 2: 'Ausente', 3: 'Reprogramado' };

function FitBounds({ positions }) {
  const map = useMap();
  useEffect(() => {
    if (positions.length > 0) {
      map.fitBounds(positions, { padding: [40, 40] });
    }
  }, [positions, map]);
  return null;
}

export default function Rutas() {
  const [rutas, setRutas] = useState([]);
  const [zonas, setZonas] = useState([]);
  const [clientes, setClientes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [rutaDetalle, setRutaDetalle] = useState(null);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({ nombre: '', zonaId: '', fechaPlanificada: '', observaciones: '' });
  const [clientesSeleccionados, setClientesSeleccionados] = useState([]);
  const [clienteBusqueda, setClienteBusqueda] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [marcandoVisita, setMarcandoVisita] = useState(null);
  const [formVisita, setFormVisita] = useState({ estadoVisita: '1', observaciones: '' });

  const fetchRutas = () => {
    return api.get('/rutas').then((r) => {
      setRutas(r.data);
      setLoading(false);
    });
  };

  const fetchDetalle = async (id) => {
    const r = await api.get(`/rutas/${id}`);
    setRutaDetalle(r.data);
  };

  useEffect(() => {
    Promise.all([
      api.get('/zonas').then(r => setZonas(r.data)),
      api.get('/clientes/opciones').then(r => setClientes(r.data)),
    ]);
    fetchRutas();
  }, []);

  const clientesFiltrados = clientes.filter(c =>
    c.nombreCompleto?.toLowerCase().includes(clienteBusqueda.toLowerCase()) &&
    !clientesSeleccionados.find(s => s.clienteId === c.id)
  );

  const agregarCliente = (cliente) => {
    setClientesSeleccionados(prev => [...prev, { clienteId: cliente.id, nombre: cliente.nombreCompleto, orden: prev.length + 1 }]);
  };

  const quitarCliente = (clienteId) => {
    setClientesSeleccionados(prev => {
      const filtered = prev.filter(c => c.clienteId !== clienteId);
      return filtered.map((c, i) => ({ ...c, orden: i + 1 }));
    });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (clientesSeleccionados.length === 0) { setError('Agrega al menos un cliente a la ruta.'); return; }
    setSaving(true); setError('');
    try {
      await api.post('/rutas', {
        nombre: form.nombre,
        zonaId: parseInt(form.zonaId),
        fechaPlanificada: form.fechaPlanificada,
        observaciones: form.observaciones || null,
        clientes: clientesSeleccionados.map(c => ({ clienteId: c.clienteId, orden: c.orden })),
      });
      setShowForm(false);
      setForm({ nombre: '', zonaId: '', fechaPlanificada: '', observaciones: '' });
      setClientesSeleccionados([]);
      fetchRutas();
    } catch (err) {
      setError(mensajeDeError(err, 'Error al crear la ruta.'));
    } finally { setSaving(false); }
  };

  const handleIniciar = async (id) => {
    await api.post(`/rutas/${id}/iniciar`);
    fetchRutas();
    if (rutaDetalle?.id === id) fetchDetalle(id);
  };

  const handleCompletar = async (id) => {
    await api.post(`/rutas/${id}/completar`);
    fetchRutas();
    if (rutaDetalle?.id === id) fetchDetalle(id);
  };

  const handleMarcarVisita = async () => {
    await api.post(`/rutas/${rutaDetalle.id}/detalles/${marcandoVisita.id}/visita`, {
      estadoVisita: parseInt(formVisita.estadoVisita),
      latitud: null,
      longitud: null,
      observaciones: formVisita.observaciones || null,
    });
    setMarcandoVisita(null);
    fetchDetalle(rutaDetalle.id);
  };

  const posicionesRuta = rutaDetalle?.detalles
    ?.filter(d => d.cliente?.latitud && d.cliente?.longitud)
    .map(d => [parseFloat(d.cliente.latitud), parseFloat(d.cliente.longitud)]) ?? [];

  return (
    <Layout>
      <PageHeader
        title="Rutas de Campo"
        subtitle="Planificación y seguimiento de visitas"
        action={
          <Btn variant="primary" size="md" onClick={() => { setShowForm(true); setError(''); }}>
            + Nueva ruta
          </Btn>
        }
      />

      {/* Formulario nueva ruta */}
      {showForm && (
        <Card className="mb-5">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-semibold text-slate-700">Planificar Ruta</h3>
            <Btn variant="ghost" onClick={() => setShowForm(false)}>✕</Btn>
          </div>
          {error && <Alert type="error" className="mb-3">{error}</Alert>}
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
              <Input label="Nombre de la ruta *" value={form.nombre} onChange={e => setForm({ ...form, nombre: e.target.value })} required />
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Zona *</label>
                <select value={form.zonaId} onChange={e => setForm({ ...form, zonaId: e.target.value })}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500" required>
                  <option value="">Seleccionar zona...</option>
                  {zonas.map(z => <option key={z.id} value={z.id}>{z.nombre}</option>)}
                </select>
              </div>
              <Input label="Fecha planificada *" type="date" value={form.fechaPlanificada} onChange={e => setForm({ ...form, fechaPlanificada: e.target.value })} required />
            </div>

            {/* Agregar clientes */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Buscar clientes</label>
                <input
                  type="text"
                  placeholder="Nombre del cliente..."
                  value={clienteBusqueda}
                  onChange={e => setClienteBusqueda(e.target.value)}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
                <div className="mt-2 max-h-40 overflow-y-auto border border-slate-100 rounded-lg">
                  {clientesFiltrados.slice(0, 20).map(c => (
                    <button key={c.id} type="button" onClick={() => agregarCliente(c)}
                      className="w-full text-left px-3 py-2 text-sm hover:bg-blue-50 hover:text-blue-700 transition-colors border-b border-slate-50 last:border-0">
                      {c.nombreCompleto}
                    </button>
                  ))}
                  {clientesFiltrados.length === 0 && (
                    <p className="text-xs text-slate-400 px-3 py-2">Sin resultados</p>
                  )}
                </div>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">
                  Orden de visita ({clientesSeleccionados.length} clientes)
                </label>
                <div className="max-h-48 overflow-y-auto border border-slate-100 rounded-lg">
                  {clientesSeleccionados.length === 0 && (
                    <p className="text-xs text-slate-400 px-3 py-4 text-center">Sin clientes agregados</p>
                  )}
                  {clientesSeleccionados.map((c, i) => (
                    <div key={c.clienteId} className="flex items-center gap-2 px-3 py-2 border-b border-slate-50 last:border-0">
                      <span className="w-5 h-5 rounded-full bg-blue-100 text-blue-700 text-xs font-bold flex items-center justify-center shrink-0">{i + 1}</span>
                      <span className="text-sm flex-1 truncate">{c.nombre}</span>
                      <button type="button" onClick={() => quitarCliente(c.clienteId)}
                        className="text-slate-400 hover:text-red-500 text-xs">✕</button>
                    </div>
                  ))}
                </div>
              </div>
            </div>

            <div className="flex gap-2">
              <Btn type="submit" variant="primary" size="md" disabled={saving}>
                {saving ? 'Creando...' : 'Crear ruta'}
              </Btn>
              <Btn variant="ghost" size="md" onClick={() => setShowForm(false)}>Cancelar</Btn>
            </div>
          </form>
        </Card>
      )}

      {/* Modal detalle + mapa */}
      {rutaDetalle && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl w-full max-w-5xl max-h-[90vh] flex flex-col overflow-hidden shadow-2xl">
            {/* Header */}
            <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100">
              <div>
                <h3 className="font-bold text-slate-800">{rutaDetalle.numero} — {rutaDetalle.nombre}</h3>
                <p className="text-xs text-slate-500">{rutaDetalle.zona?.nombre} · {rutaDetalle.promotor?.nombreCompleto}</p>
              </div>
              <div className="flex items-center gap-3">
                <Badge color={ESTADO_RUTA_COLOR[rutaDetalle.estado]}>{ESTADO_RUTA_LABEL[rutaDetalle.estado]}</Badge>
                {rutaDetalle.estado === 0 && <Btn variant="primary" size="md" onClick={() => handleIniciar(rutaDetalle.id)}>Iniciar ruta</Btn>}
                {rutaDetalle.estado === 1 && <Btn variant="primary" size="md" onClick={() => handleCompletar(rutaDetalle.id)}>Completar</Btn>}
                <Btn variant="ghost" onClick={() => setRutaDetalle(null)}>✕</Btn>
              </div>
            </div>

            <div className="flex flex-1 overflow-hidden">
              {/* Lista de paradas */}
              <div className="w-72 shrink-0 border-r border-slate-100 overflow-y-auto">
                <div className="p-3 space-y-1">
                  {rutaDetalle.detalles?.map(d => (
                    <div key={d.id} className="rounded-lg border border-slate-100 px-3 py-2.5">
                      <div className="flex items-center gap-2 mb-1">
                        <span className="w-5 h-5 rounded-full bg-slate-100 text-slate-600 text-xs font-bold flex items-center justify-center shrink-0">{d.orden}</span>
                        <span className="text-sm font-medium text-slate-800 truncate flex-1">{d.cliente?.nombreCompleto}</span>
                      </div>
                      <div className="flex items-center justify-between">
                        <Badge color={ESTADO_VISITA_COLOR[d.estadoVisita]}>{ESTADO_VISITA_LABEL[d.estadoVisita]}</Badge>
                        {rutaDetalle.estado === 1 && d.estadoVisita === 0 && (
                          <Btn variant="ghost" onClick={() => { setMarcandoVisita(d); setFormVisita({ estadoVisita: '1', observaciones: '' }); }}>
                            Marcar
                          </Btn>
                        )}
                      </div>
                      {d.horaVisita && (
                        <p className="text-xs text-slate-400 mt-1">{formatearHora(d.horaVisita)}</p>
                      )}
                    </div>
                  ))}
                </div>
              </div>

              {/* Mapa */}
              <div className="flex-1">
                {posicionesRuta.length > 0 ? (
                  <MapContainer
                    center={posicionesRuta[0]}
                    zoom={13}
                    style={{ height: '100%', width: '100%' }}
                  >
                    <TileLayer
                      url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                      attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                    />
                    <FitBounds positions={posicionesRuta} />
                    {rutaDetalle.detalles?.filter(d => d.cliente?.latitud && d.cliente?.longitud).map(d => (
                      <Marker
                        key={d.id}
                        position={[parseFloat(d.cliente.latitud), parseFloat(d.cliente.longitud)]}
                        icon={ICON_MAP[d.estadoVisita]}
                      >
                        <Popup>
                          <div className="text-sm">
                            <p className="font-semibold">#{d.orden} — {d.cliente.nombreCompleto}</p>
                            <p className="text-slate-500">{d.cliente.direccionTexto}</p>
                            <p className="mt-1">
                              <span className={`font-medium ${d.estadoVisita === 1 ? 'text-green-600' : d.estadoVisita === 2 ? 'text-red-500' : 'text-slate-500'}`}>
                                {ESTADO_VISITA_LABEL[d.estadoVisita]}
                              </span>
                            </p>
                          </div>
                        </Popup>
                      </Marker>
                    ))}
                    {posicionesRuta.length > 1 && (
                      <Polyline positions={posicionesRuta} color="#3b82f6" weight={2} dashArray="6,4" />
                    )}
                  </MapContainer>
                ) : (
                  <div className="flex items-center justify-center h-full text-slate-400 text-sm flex-col gap-2">
                    <span className="text-4xl">🗺</span>
                    <p>Los clientes de esta ruta no tienen coordenadas GPS registradas.</p>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Modal marcar visita */}
      {marcandoVisita && (
        <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-[60] p-4">
          <Card className="w-full max-w-sm">
            <h3 className="text-sm font-semibold text-slate-700 mb-3">
              Marcar visita — {marcandoVisita.cliente?.nombreCompleto}
            </h3>
            <div className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Resultado de la visita</label>
                <select value={formVisita.estadoVisita} onChange={e => setFormVisita({ ...formVisita, estadoVisita: e.target.value })}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
                  <option value="1">Visitado</option>
                  <option value="2">Ausente</option>
                  <option value="3">Reprogramado</option>
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Observaciones</label>
                <textarea value={formVisita.observaciones} onChange={e => setFormVisita({ ...formVisita, observaciones: e.target.value })}
                  rows={2} className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
              </div>
            </div>
            <div className="flex gap-2 mt-3">
              <Btn variant="primary" size="md" onClick={handleMarcarVisita}>Confirmar</Btn>
              <Btn variant="ghost" size="md" onClick={() => setMarcandoVisita(null)}>Cancelar</Btn>
            </div>
          </Card>
        </div>
      )}

      {/* Tabla */}
      {loading ? (
        <div className="flex items-center justify-center h-40 text-slate-400 text-sm">Cargando rutas...</div>
      ) : (
        <Table headers={['Número', 'Nombre', 'Zona', 'Promotor', 'Fecha', 'Progreso', 'Estado', 'Acciones']}
          empty="Sin rutas planificadas.">
          {rutas.map(r => (
            <Tr key={r.id}>
              <Td mono>{r.numero}</Td>
              <Td><span className="font-medium text-slate-800">{r.nombre}</span></Td>
              <Td>{r.zona?.nombre}</Td>
              <Td>{r.promotor?.nombreCompleto}</Td>
              <Td>{formatearDia(r.fechaPlanificada)}</Td>
              <Td>
                <div className="flex items-center gap-2">
                  <div className="flex-1 h-1.5 bg-slate-100 rounded-full overflow-hidden w-20">
                    <div
                      className="h-full bg-green-500 rounded-full"
                      style={{ width: r.totalClientes > 0 ? `${(r.visitados / r.totalClientes) * 100}%` : '0%' }}
                    />
                  </div>
                  <span className="text-xs text-slate-500">{r.visitados}/{r.totalClientes}</span>
                </div>
              </Td>
              <Td><Badge color={ESTADO_RUTA_COLOR[r.estado]}>{ESTADO_RUTA_LABEL[r.estado]}</Badge></Td>
              <Td>
                <Btn variant="ghost" onClick={() => fetchDetalle(r.id)}>Ver mapa</Btn>
              </Td>
            </Tr>
          ))}
        </Table>
      )}
    </Layout>
  );
}
