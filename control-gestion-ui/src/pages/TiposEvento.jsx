import { useCallback, useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { CircleCheck, CircleOff, ListChecks, LoaderCircle, Pencil, Plus, Trash2 } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import { PRIORIDADES } from '../components/eventos/estados';
import {
  Alert, Badge, BarraDeFiltros, BotonCancelar, Btn, Buscador, Casilla, Confirmar, ErrorDeCarga, EstadoVacio, Input, Modal,
  PageHeader, Selector,
} from '../components/ui';
import { avisar } from '../components/ui/avisar';
import { useAuth } from '../context/auth';
import { PERMISOS } from '../context/permisos';
import { useSinGuardar } from '../hooks/useSinGuardar';
import { mostrarPrimerProblema } from '../utils/foco';
import { coincide } from '../utils/texto';

const { parametros } = PERMISOS;
const SIN_FILTROS = { texto: '', estado: '' };
const ESTADOS = [{ valor: 'activos', texto: 'Activos' }, { valor: 'inactivos', texto: 'Inactivos' }];
const OPCIONES_DE_PRIORIDAD = Object.entries(PRIORIDADES).map(([valor, texto]) => ({ valor, texto }));
const MEDIA = '1';

// Errores de la API que pertenecen a un campo del formulario: se muestran junto a él.
const CAMPO_DEL_ERROR = { tipo_evento_repetido: 'nombre' };

const enPlural = (n, singular, plural) => (n === 1 ? `1 ${singular}` : `${n} ${plural}`);
const susEventos = (n) => (n === 1 ? 'su evento' : `sus ${n} eventos`);

// Como la API: los activos primero, cada grupo en el orden en que se registraron (el de los formularios).
const ordenar = (lista) => [...lista].sort((a, b) => Number(b.activo) - Number(a.activo) || a.id - b.id);

/**
 * Los tipos de evento (7.3.13, extensión de la tarea 32): los que el promotor elige al registrar un evento en la app.
 * Uno inactivo ya no se elige, pero sus eventos lo conservan y se siguen filtrando por él. Uno con eventos no se elimina,
 * y siempre queda al menos uno activo.
 */
export default function TiposEvento() {
  const { tiene } = useAuth();
  const puedeRegistrar = tiene(parametros.registrarTipoEvento);
  const puedeModificar = tiene(parametros.modificarTipoEvento);
  const puedeDesactivar = tiene(parametros.desactivarTipoEvento);

  const [tipos, setTipos] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [filtros, setFiltros] = useState(SIN_FILTROS);

  const [editor, setEditor] = useState(null);
  const sinGuardar = useSinGuardar(editor);
  const editorRef = useRef(null);
  const [erroresCampo, setErroresCampo] = useState({});
  const [errorEditor, setErrorEditor] = useState('');
  const [guardando, setGuardando] = useState(false);

  const [aDesactivar, setADesactivar] = useState(null);
  const [errorDesactivar, setErrorDesactivar] = useState('');
  const [procesandoId, setProcesandoId] = useState(null);

  const [aEliminar, setAEliminar] = useState(null);
  const [errorEliminar, setErrorEliminar] = useState('');
  const [eliminando, setEliminando] = useState(false);

  const cargar = useCallback(() => api.get('/tipos-evento')
    .then(({ data }) => { setTipos(data); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la lista de tipos de evento.')))
    .finally(() => { setCargando(false); setReintentando(false); }), []);

  useEffect(() => { cargar(); }, [cargar]);

  const reemplazar = (tipo) => setTipos((lista) => ordenar(lista.map((t) => (t.id === tipo.id ? tipo : t))));

  // "original" es el nombre al abrir: el título de la ventana no cambia mientras se escribe.
  const abrirEditor = (tipo) => {
    setErroresCampo({});
    setErrorEditor('');
    setEditor(tipo
      ? {
        id: tipo.id, original: tipo.nombre, eventos: tipo.eventos, nombre: tipo.nombre,
        requiereEvidencia: tipo.requiereEvidencia, nivelPrioridad: String(tipo.nivelPrioridad),
      }
      : { nombre: '', requiereEvidencia: false, nivelPrioridad: MEDIA });
  };

  const guardar = async (ev) => {
    ev.preventDefault();
    setGuardando(true);
    setErroresCampo({});
    setErrorEditor('');
    const datos = {
      nombre: editor.nombre,
      requiereEvidencia: editor.requiereEvidencia,
      nivelPrioridad: editor.nivelPrioridad === '' ? null : Number(editor.nivelPrioridad),
    };
    try {
      const { data } = editor.id ? await api.put(`/tipos-evento/${editor.id}`, datos) : await api.post('/tipos-evento', datos);
      if (editor.id) reemplazar(data);
      else setTipos((lista) => ordenar([...lista, data]));
      avisar.exito(editor.id ? `Se guardó el tipo de evento ${data.nombre}.` : `Se registró el tipo de evento ${data.nombre}.`);
      setEditor(null);
    } catch (err) {
      const campos = erroresPorCampo(err);
      const campoDelCodigo = CAMPO_DEL_ERROR[err.response?.data?.codigo];
      if (campoDelCodigo) campos[campoDelCodigo] = err.response.data.detail;
      flushSync(() => {
        setErroresCampo(campos);
        if (Object.keys(campos).length === 0) setErrorEditor(mensajeDeError(err, 'No se pudo guardar el tipo de evento.'));
      });
      mostrarPrimerProblema(editorRef.current);
    } finally {
      setGuardando(false);
    }
  };

  const cambiarActivo = async (tipo, activo) => {
    setProcesandoId(tipo.id);
    avisar.quitarError();
    setErrorDesactivar('');
    try {
      const { data } = await api.put(`/tipos-evento/${tipo.id}/${activo ? 'activar' : 'desactivar'}`);
      reemplazar(data);
      avisar.exito(activo ? `Se activó el tipo de evento ${data.nombre}.` : `Se desactivó el tipo de evento ${data.nombre}.`);
      setADesactivar(null);
    } catch (err) {
      const mensaje = mensajeDeError(err, activo ? 'No se pudo activar el tipo de evento.' : 'No se pudo desactivar el tipo de evento.');
      // Desactivar se confirma en una ventana: el error va ahí. Activar es directo: el error va en un toast.
      if (activo) avisar.error(mensaje);
      else setErrorDesactivar(mensaje);
    } finally {
      setProcesandoId(null);
    }
  };

  const eliminar = async () => {
    setEliminando(true);
    setErrorEliminar('');
    try {
      await api.delete(`/tipos-evento/${aEliminar.id}`);
      setTipos((lista) => lista.filter((t) => t.id !== aEliminar.id));
      avisar.exito(`Se eliminó el tipo de evento ${aEliminar.nombre}.`);
      setAEliminar(null);
    } catch (err) {
      setErrorEliminar(mensajeDeError(err, 'No se pudo eliminar el tipo de evento.'));
    } finally {
      setEliminando(false);
    }
  };

  const visibles = tipos.filter((t) => coincide(filtros.texto, t.nombre)
    && (!filtros.estado || (filtros.estado === 'activos') === t.activo));
  const total = tipos.length;
  const hayFiltros = filtros.texto !== '' || filtros.estado !== '';
  // Sin un tipo activo, la app no podría registrar eventos: el último no se desactiva ni se elimina.
  const elUnicoActivo = (t) => t.activo && tipos.filter((o) => o.activo).length === 1;
  const porQueNoSeElimina = (t) => (t.eventos > 0
    ? 'Tiene eventos: desactívalo en lugar de eliminarlo.'
    : 'Es el único tipo activo: sin él no se podrían registrar eventos.');
  const botonNuevo = puedeRegistrar && (
    <Btn size="md" onClick={() => abrirEditor(null)}>
      <Plus size={16} aria-hidden="true" />
      Nuevo tipo de evento
    </Btn>
  );

  let estado = null;
  if (errorCarga) estado = <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />;
  else if (total === 0) estado = (
    <EstadoVacio titulo="Todavía no hay tipos de evento" icono={ListChecks} accion={botonNuevo}>
      Registra el primero: el promotor lo elige al registrar un evento en la app.
    </EstadoVacio>
  );

  return (
    <Layout>
      <PageHeader
        title="Tipos de evento"
        subtitle="Los que el promotor elige al registrar un evento en la app. Uno inactivo ya no se elige, pero sus eventos lo conservan."
        action={botonNuevo}
      />

      <Table
        cargando={cargando && 'Cargando tipos de evento…'}
        headers={['Tipo de evento', 'Pide foto', 'Prioridad', { texto: 'Eventos', derecha: true }, { texto: 'Acciones', oculto: true }]}
        empty="Ningún tipo de evento coincide con los filtros."
        estado={estado}
        barra={(cargando || (!errorCarga && total > 0)) && (
          <BarraDeFiltros
            resultado={cargando ? '' : (hayFiltros ? `${visibles.length} de ${enPlural(total, 'tipo', 'tipos')}` : enPlural(total, 'tipo', 'tipos'))}
            hayFiltros={hayFiltros}
            onLimpiar={() => setFiltros(SIN_FILTROS)}
          >
            <Buscador label="Buscar" placeholder="Nombre del tipo…" value={filtros.texto}
              onChange={(texto) => setFiltros({ ...filtros, texto })} />
            <div className="w-full sm:w-40">
              <Selector label="Estado" todos="Todos" value={filtros.estado} opciones={ESTADOS}
                onChange={(estadoElegido) => setFiltros({ ...filtros, estado: estadoElegido })} />
            </div>
          </BarraDeFiltros>
        )}
      >
        {visibles.map((t) => (
          <Tr key={t.id}>
            <Td>
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-medium">{t.nombre}</span>
                {!t.activo && <Badge color="gray">Inactivo</Badge>}
              </div>
            </Td>
            <Td>{t.requiereEvidencia ? 'Sí' : 'No'}</Td>
            <Td>{PRIORIDADES[t.nivelPrioridad]}</Td>
            <Td className="text-right tabular-nums">{t.eventos}</Td>
            <Td>
              <div className="flex items-center justify-end gap-1">
                {puedeModificar && (
                  <Btn variant="ghost" onClick={() => abrirEditor(t)} aria-label={`Editar el tipo ${t.nombre}`}>
                    <Pencil size={14} aria-hidden="true" />
                    Editar
                  </Btn>
                )}
                {puedeDesactivar && t.activo && (
                  <Btn
                    variant="ghost"
                    onClick={() => { setErrorDesactivar(''); setADesactivar(t); }}
                    disabled={elUnicoActivo(t)}
                    title={elUnicoActivo(t) ? 'Es el único tipo activo: sin él no se podrían registrar eventos.' : undefined}
                    aria-label={`Desactivar el tipo ${t.nombre}`}
                  >
                    <CircleOff size={14} aria-hidden="true" />
                    Desactivar
                  </Btn>
                )}
                {puedeDesactivar && !t.activo && (
                  <Btn variant="ghost" onClick={() => cambiarActivo(t, true)} disabled={procesandoId === t.id}
                    aria-label={`Activar el tipo ${t.nombre}`}>
                    {procesandoId === t.id
                      ? <LoaderCircle size={14} className="animate-spin" aria-hidden="true" />
                      : <CircleCheck size={14} aria-hidden="true" />}
                    Activar
                  </Btn>
                )}
                {puedeDesactivar && (
                  <Btn
                    variant="ghost"
                    onClick={() => { setErrorEliminar(''); setAEliminar(t); }}
                    disabled={t.eventos > 0 || elUnicoActivo(t)}
                    title={t.eventos > 0 || elUnicoActivo(t) ? porQueNoSeElimina(t) : undefined}
                    aria-label={`Eliminar el tipo ${t.nombre}`}
                  >
                    <Trash2 size={14} aria-hidden="true" />
                    Eliminar
                  </Btn>
                )}
              </div>
            </Td>
          </Tr>
        ))}
      </Table>
      {puedeDesactivar && tipos.some((t) => t.eventos > 0) && (
        <p className="mt-3 text-sm text-slate-600">
          Un tipo con eventos no se puede eliminar: desactívalo, y sus eventos lo conservan. Siempre queda al menos un tipo activo.
        </p>
      )}

      <Modal
        abierto={editor !== null}
        onCerrar={() => setEditor(null)}
        sinGuardar={sinGuardar}
        titulo={editor?.id ? `Editar el tipo ${editor.original}` : 'Nuevo tipo de evento'}
        descripcion={editor?.id ? undefined : 'El promotor lo elige al registrar un evento en la app.'}
        pie={(
          <>
            <BotonCancelar disabled={guardando} />
            <Btn type="submit" form="editor-tipo-evento" size="md" disabled={guardando}>
              {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
              {guardando ? 'Guardando…' : 'Guardar'}
            </Btn>
          </>
        )}
      >
        {editor && (
          <form id="editor-tipo-evento" ref={editorRef} onSubmit={guardar} className="space-y-4" noValidate>
            {errorEditor && <Alert type="error">{errorEditor}</Alert>}
            <Input
              label="Nombre"
              value={editor.nombre}
              maxLength={80}
              error={erroresCampo.nombre}
              placeholder="Mercadería dañada, local cerrado…"
              ayuda={editor.eventos > 0 ? `El nombre nuevo se ve también en ${susEventos(editor.eventos)}.` : undefined}
              onChange={(e) => setEditor({ ...editor, nombre: e.target.value })}
              autoComplete="off"
            />
            <Casilla
              checked={editor.requiereEvidencia}
              onCheckedChange={(marcada) => setEditor({ ...editor, requiereEvidencia: marcada === true })}
              ayuda={editor.id
                ? 'El evento se resuelve con al menos una foto. Un cambio vale también para los eventos pendientes de este tipo.'
                : 'El evento se resuelve con al menos una foto.'}
            >
              Pide una foto para resolverlo
            </Casilla>
            <Selector
              label="Prioridad"
              value={editor.nivelPrioridad}
              opciones={OPCIONES_DE_PRIORIDAD}
              error={erroresCampo.nivelPrioridad}
              ayuda="Se ve en el detalle del evento y en el reporte de incidencias."
              onChange={(nivelPrioridad) => setEditor({ ...editor, nivelPrioridad })}
            />
          </form>
        )}
      </Modal>

      <Confirmar
        abierto={aDesactivar !== null}
        titulo={`¿Desactivar el tipo ${aDesactivar?.nombre ?? ''}?`}
        textoAccion="Desactivar"
        enviando={procesandoId === aDesactivar?.id}
        error={errorDesactivar}
        onConfirmar={() => cambiarActivo(aDesactivar, false)}
        onCancelar={() => setADesactivar(null)}
      >
        <p>
          Deja de aparecer al registrar o modificar un evento.
          {aDesactivar?.eventos === 1 && ' Su evento lo conserva, y se sigue pudiendo filtrar por él.'}
          {aDesactivar?.eventos > 1 && ` Sus ${aDesactivar.eventos} eventos lo conservan, y se sigue pudiendo filtrar por él.`}
          {' '}Puedes activarlo de nuevo cuando quieras.
        </p>
      </Confirmar>

      <Confirmar
        abierto={aEliminar !== null}
        titulo={`¿Eliminar el tipo ${aEliminar?.nombre ?? ''}?`}
        textoAccion="Eliminar"
        enviando={eliminando}
        error={errorEliminar}
        onConfirmar={eliminar}
        onCancelar={() => setAEliminar(null)}
      >
        <p>El tipo deja de existir y ya no se puede elegir. Esto no se puede deshacer.</p>
      </Confirmar>
    </Layout>
  );
}
