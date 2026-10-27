import { useCallback, useEffect, useState } from 'react';
import { LoaderCircle, Pencil, Plus, UserCheck, UserX, Users } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import {
  Alert, Badge, BarraDeFiltros, Btn, Buscador, Cargando, Confirmar, ErrorDeCarga, EstadoVacio, Input, Modal,
  PageHeader, Selector,
} from '../components/ui';
import { avisar } from '../components/ui/avisar';
import { useAuth } from '../context/auth';
import { PERMISOS } from '../context/permisos';
import { formatearFecha } from '../utils/formato';
import { coincide } from '../utils/texto';

const { seguridad } = PERMISOS;
const SIN_FILTROS = { texto: '', rol: '', estado: '' };
const ESTADOS = [{ valor: 'activos', texto: 'Activos' }, { valor: 'inactivos', texto: 'Inactivos' }];

// Errores de la API que pertenecen a un campo del formulario: se muestran junto a él.
const CAMPO_DEL_ERROR = { correo_duplicado: 'email', rol_no_existe: 'rol', no_puede_cambiar_su_rol: 'rol' };

export default function Usuarios() {
  const { usuario: yo, tiene } = useAuth();
  const puedeCrear = tiene(seguridad.crearUsuario);
  const puedeEditar = tiene(seguridad.editarUsuario);
  const puedeActivar = tiene(seguridad.activarUsuario);

  const [usuarios, setUsuarios] = useState([]);
  const [roles, setRoles] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [filtros, setFiltros] = useState(SIN_FILTROS);

  const [formulario, setFormulario] = useState(null);
  const [erroresCampo, setErroresCampo] = useState({});
  const [errorFormulario, setErrorFormulario] = useState('');
  const [guardando, setGuardando] = useState(false);

  const [aDesactivar, setADesactivar] = useState(null);
  const [errorDesactivar, setErrorDesactivar] = useState('');
  const [procesandoId, setProcesandoId] = useState(null);

  const cargar = useCallback(() => Promise.all([
    api.get('/usuarios'),
    // La lista de roles es para el formulario: solo la pide quien crea o edita usuarios.
    puedeCrear || puedeEditar ? api.get('/roles') : Promise.resolve({ data: [] }),
  ])
    .then(([u, r]) => {
      setUsuarios(u.data);
      setRoles(r.data);
      setErrorCarga('');
    })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la lista de usuarios.')))
    .finally(() => { setCargando(false); setReintentando(false); }), [puedeCrear, puedeEditar]);

  useEffect(() => { cargar(); }, [cargar]);

  const reintentar = () => { setReintentando(true); cargar(); };

  const reemplazar = (actualizado) => setUsuarios((lista) => {
    const existe = lista.some((u) => u.id === actualizado.id);
    const nueva = existe ? lista.map((u) => (u.id === actualizado.id ? actualizado : u)) : [...lista, actualizado];
    return nueva.sort((a, b) => a.nombreCompleto.localeCompare(b.nombreCompleto, 'es'));
  });

  const abrirFormulario = (u) => {
    setErroresCampo({});
    setErrorFormulario('');
    setFormulario(u
      ? { id: u.id, nombreCompleto: u.nombreCompleto, email: u.email, rol: u.rol ?? '' }
      : { nombreCompleto: '', email: '', rol: '', password: '' });
  };

  const guardar = async (e) => {
    e.preventDefault();
    setGuardando(true);
    setErroresCampo({});
    setErrorFormulario('');
    const { id, ...datos } = formulario;
    try {
      const { data } = id ? await api.put(`/usuarios/${id}`, datos) : await api.post('/usuarios', datos);
      reemplazar(data);
      avisar.exito(id ? `Se guardaron los cambios de ${data.nombreCompleto}.` : `Se creó el usuario ${data.nombreCompleto}.`);
      setFormulario(null);
    } catch (err) {
      const campos = erroresPorCampo(err);
      const campoDelCodigo = CAMPO_DEL_ERROR[err.response?.data?.codigo];
      if (campoDelCodigo) campos[campoDelCodigo] = err.response.data.detail;
      setErroresCampo(campos);
      if (Object.keys(campos).length === 0) setErrorFormulario(mensajeDeError(err, 'No se pudo guardar el usuario.'));
    } finally {
      setGuardando(false);
    }
  };

  const cambiarActivo = async (u, activo) => {
    setProcesandoId(u.id);
    avisar.quitarError();
    setErrorDesactivar('');
    try {
      const { data } = await api.put(`/usuarios/${u.id}/${activo ? 'activar' : 'desactivar'}`);
      reemplazar(data);
      avisar.exito(activo ? `Se activó a ${data.nombreCompleto}.` : `Se desactivó a ${data.nombreCompleto}.`);
      setADesactivar(null);
    } catch (err) {
      const mensaje = mensajeDeError(err, activo ? 'No se pudo activar el usuario.' : 'No se pudo desactivar el usuario.');
      // Desactivar se confirma en un modal: el error va ahí. Activar es directo: el error va en un toast.
      if (activo) avisar.error(mensaje);
      else setErrorDesactivar(mensaje);
    } finally {
      setProcesandoId(null);
    }
  };

  const visibles = usuarios.filter((u) => coincide(filtros.texto, u.nombreCompleto, u.email)
    && (!filtros.rol || u.rol === filtros.rol)
    && (!filtros.estado || (filtros.estado === 'activos') === u.activo));
  const rolesDeLaLista = [...new Set(usuarios.map((u) => u.rol).filter(Boolean))].sort();
  const hayFiltros = filtros.texto !== '' || filtros.rol !== '' || filtros.estado !== '';
  const esYo = (u) => u.id === yo?.id;

  let estado = null;
  if (cargando) estado = <Cargando texto="Cargando usuarios…" />;
  else if (errorCarga && usuarios.length === 0) estado = <ErrorDeCarga mensaje={errorCarga} onReintentar={reintentar} reintentando={reintentando} />;
  else if (usuarios.length === 0) estado = (
    <EstadoVacio titulo="Todavía no hay usuarios" icono={Users}
      accion={puedeCrear && <Btn size="md" onClick={() => abrirFormulario(null)}><Plus size={16} aria-hidden="true" />Nuevo usuario</Btn>}>
      Crea el primero para que pueda iniciar sesión.
    </EstadoVacio>
  );

  return (
    <Layout>
      <PageHeader
        title="Usuarios"
        subtitle="Quién entra al sistema y con qué rol."
        action={puedeCrear && (
          <Btn size="md" onClick={() => abrirFormulario(null)}>
            <Plus size={16} aria-hidden="true" />
            Nuevo usuario
          </Btn>
        )}
      />

      <Table
        headers={['Usuario', 'Correo', 'Rol', 'Estado', 'Alta', '']}
        empty="Ningún usuario coincide con la búsqueda."
        estado={estado}
        barra={!cargando && usuarios.length > 0 && (
          <BarraDeFiltros
            resultado={hayFiltros ? `${visibles.length} de ${usuarios.length} usuarios` : `${usuarios.length} usuarios`}
            hayFiltros={hayFiltros}
            onLimpiar={() => setFiltros(SIN_FILTROS)}
          >
            <Buscador label="Buscar" placeholder="Nombre o correo…" value={filtros.texto}
              onChange={(texto) => setFiltros({ ...filtros, texto })} />
            <div className="w-full sm:w-52">
              <Selector label="Rol" todos="Todos los roles" value={filtros.rol}
                opciones={rolesDeLaLista.map((r) => ({ valor: r, texto: r }))}
                onChange={(rol) => setFiltros({ ...filtros, rol })} />
            </div>
            <div className="w-full sm:w-40">
              <Selector label="Estado" todos="Todos" value={filtros.estado} opciones={ESTADOS}
                onChange={(estadoElegido) => setFiltros({ ...filtros, estado: estadoElegido })} />
            </div>
          </BarraDeFiltros>
        )}
      >
        {visibles.map((u) => (
          <Tr key={u.id}>
            <Td>
              <div className="flex items-center gap-2.5">
                <span className="grid place-items-center w-8 h-8 rounded-full bg-slate-100 text-xs font-bold text-slate-700 shrink-0" aria-hidden="true">
                  {u.nombreCompleto?.[0]?.toUpperCase()}
                </span>
                <span className="font-medium">
                  {u.nombreCompleto}
                  {esYo(u) && <span className="ml-1.5 text-slate-600 font-normal">(tú)</span>}
                </span>
              </div>
            </Td>
            <Td>{u.email}</Td>
            <Td>{u.rol ?? <Badge color="yellow">Sin rol</Badge>}</Td>
            <Td>
              <Badge color={u.activo ? 'green' : 'gray'}>{u.activo ? 'Activo' : 'Inactivo'}</Badge>
            </Td>
            <Td className="whitespace-nowrap">{formatearFecha(u.fechaCreacion)}</Td>
            <Td>
              <div className="flex items-center justify-end gap-1">
                {puedeEditar && (
                  <Btn variant="ghost" onClick={() => abrirFormulario(u)} aria-label={`Editar a ${u.nombreCompleto}`}>
                    <Pencil size={14} aria-hidden="true" />
                    Editar
                  </Btn>
                )}
                {puedeActivar && u.activo && !esYo(u) && (
                  <Btn variant="ghost" onClick={() => { setErrorDesactivar(''); setADesactivar(u); }}
                    aria-label={`Desactivar a ${u.nombreCompleto}`}>
                    <UserX size={14} aria-hidden="true" />
                    Desactivar
                  </Btn>
                )}
                {puedeActivar && !u.activo && (
                  <Btn variant="ghost" onClick={() => cambiarActivo(u, true)} disabled={procesandoId === u.id}
                    aria-label={`Activar a ${u.nombreCompleto}`}>
                    {procesandoId === u.id
                      ? <LoaderCircle size={14} className="animate-spin" aria-hidden="true" />
                      : <UserCheck size={14} aria-hidden="true" />}
                    Activar
                  </Btn>
                )}
              </div>
            </Td>
          </Tr>
        ))}
      </Table>

      <Modal
        abierto={formulario !== null}
        onCerrar={() => setFormulario(null)}
        titulo={formulario?.id ? 'Editar usuario' : 'Nuevo usuario'}
        descripcion={formulario?.id ? undefined : 'Con su correo y esta contraseña va a poder iniciar sesión.'}
        pie={(
          <>
            <Btn variant="outline" size="md" onClick={() => setFormulario(null)} disabled={guardando}>Cancelar</Btn>
            <Btn type="submit" form="formulario-usuario" size="md" disabled={guardando}>
              {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
              {guardando ? 'Guardando…' : 'Guardar'}
            </Btn>
          </>
        )}
      >
        {formulario && (
          <form id="formulario-usuario" onSubmit={guardar} className="space-y-4" noValidate>
            {errorFormulario && <Alert type="error">{errorFormulario}</Alert>}
            <Input label="Nombre completo" value={formulario.nombreCompleto} error={erroresCampo.nombreCompleto}
              onChange={(e) => setFormulario({ ...formulario, nombreCompleto: e.target.value })} autoComplete="off" />
            <Input label="Correo" type="email" value={formulario.email} error={erroresCampo.email}
              ayuda="Es también el usuario para iniciar sesión." spellCheck={false}
              onChange={(e) => setFormulario({ ...formulario, email: e.target.value })} autoComplete="off" />
            <Selector label="Rol" value={formulario.rol} error={erroresCampo.rol} placeholder="Elige un rol"
              opciones={roles.map((r) => ({ valor: r.nombre, texto: r.nombre }))}
              disabled={formulario.id === yo?.id}
              ayuda={formulario.id === yo?.id ? 'No puedes cambiar tu propio rol.' : undefined}
              onChange={(rol) => setFormulario({ ...formulario, rol })} />
            {!formulario.id && (
              <Input label="Contraseña" type="password" value={formulario.password} error={erroresCampo.password}
                ayuda="8 caracteres o más, con al menos un número." autoComplete="new-password"
                onChange={(e) => setFormulario({ ...formulario, password: e.target.value })} />
            )}
          </form>
        )}
      </Modal>

      <Confirmar
        abierto={aDesactivar !== null}
        titulo={`¿Desactivar a ${aDesactivar?.nombreCompleto ?? ''}?`}
        textoAccion="Desactivar"
        enviando={procesandoId === aDesactivar?.id}
        error={errorDesactivar}
        onConfirmar={() => cambiarActivo(aDesactivar, false)}
        onCancelar={() => setADesactivar(null)}
      >
        <p>No va a poder iniciar sesión hasta que lo actives de nuevo. Sus ventas, cobros y registros se conservan.</p>
      </Confirmar>
    </Layout>
  );
}
