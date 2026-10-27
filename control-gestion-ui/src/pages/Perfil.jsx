import { useCallback, useEffect, useState } from 'react';
import { KeyRound, LoaderCircle, UserRound } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Alert, Btn, Card, Cargando, ErrorDeCarga, Input, PageHeader } from '../components/ui';
import { avisar } from '../components/ui/avisar';
import { useAuth } from '../context/auth';
import { formatearFecha } from '../utils/formato';

const SIN_CONTRASENAS = { actual: '', nueva: '', confirmar: '' };

// Errores de la API que pertenecen a un campo: se muestran junto a él.
const CAMPO_DEL_ERROR = { correo_duplicado: 'email', contrasena_actual_incorrecta: 'actual', contrasena_invalida: 'nueva' };

function camposDelError(err) {
  const campos = erroresPorCampo(err);
  const campoDelCodigo = CAMPO_DEL_ERROR[err.response?.data?.codigo];
  if (campoDelCodigo) campos[campoDelCodigo] = err.response.data.detail;
  return campos;
}

function Dato({ titulo, children }) {
  return (
    <div>
      <dt className="text-sm font-medium text-slate-700">{titulo}</dt>
      <dd className="mt-1 text-sm text-slate-900">{children}</dd>
    </div>
  );
}

function Encabezado({ icono: Icono, titulo, children }) {
  return (
    <div className="flex items-start gap-3 mb-5">
      <span className="grid place-items-center w-9 h-9 rounded-lg bg-marca-50 text-marca-700 shrink-0" aria-hidden="true">
        <Icono size={18} />
      </span>
      <div>
        <h3 className="text-base font-semibold text-slate-900">{titulo}</h3>
        <p className="text-sm text-slate-600">{children}</p>
      </div>
    </div>
  );
}

export default function Perfil() {
  const { refrescar } = useAuth();
  const [perfil, setPerfil] = useState(null);
  const [datos, setDatos] = useState({ nombreCompleto: '', email: '' });
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');

  const [erroresDatos, setErroresDatos] = useState({});
  const [errorDatos, setErrorDatos] = useState('');
  const [guardandoDatos, setGuardandoDatos] = useState(false);

  const [contrasenas, setContrasenas] = useState(SIN_CONTRASENAS);
  const [erroresContrasena, setErroresContrasena] = useState({});
  const [errorContrasena, setErrorContrasena] = useState('');
  const [cambiando, setCambiando] = useState(false);

  const cargar = useCallback(() => api.get('/perfil')
    .then(({ data }) => {
      setPerfil(data);
      setDatos({ nombreCompleto: data.nombreCompleto, email: data.email });
      setErrorCarga('');
    })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar tu perfil.')))
    .finally(() => { setCargando(false); setReintentando(false); }), []);

  useEffect(() => { cargar(); }, [cargar]);

  const guardarDatos = async (e) => {
    e.preventDefault();
    setGuardandoDatos(true);
    setErroresDatos({});
    setErrorDatos('');
    try {
      const { data } = await api.put('/perfil', datos);
      setPerfil(data);
      avisar.exito('Se guardaron tus datos.');
      // El nombre del menú sale de la sesión.
      refrescar().catch((err) => console.warn('No se pudo actualizar la sesión.', err));
    } catch (err) {
      const campos = camposDelError(err);
      setErroresDatos(campos);
      if (Object.keys(campos).length === 0) setErrorDatos(mensajeDeError(err, 'No se pudieron guardar tus datos.'));
    } finally {
      setGuardandoDatos(false);
    }
  };

  const cambiarContrasena = async (e) => {
    e.preventDefault();
    setErroresContrasena({});
    setErrorContrasena('');
    if (contrasenas.nueva !== contrasenas.confirmar) {
      setErroresContrasena({ confirmar: 'Las dos contraseñas nuevas no coinciden.' });
      return;
    }
    setCambiando(true);
    try {
      await api.put('/perfil/contrasena', { actual: contrasenas.actual, nueva: contrasenas.nueva });
      setContrasenas(SIN_CONTRASENAS);
      avisar.exito('Tu contraseña cambió. Se cerraron tus otras sesiones abiertas.');
    } catch (err) {
      const campos = camposDelError(err);
      setErroresContrasena(campos);
      if (Object.keys(campos).length === 0) setErrorContrasena(mensajeDeError(err, 'No se pudo cambiar la contraseña.'));
    } finally {
      setCambiando(false);
    }
  };

  let contenido;
  if (cargando) contenido = <Card><Cargando texto="Cargando tu perfil…" /></Card>;
  else if (errorCarga) contenido = (
    <Card className="p-0">
      <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
    </Card>
  );
  else contenido = (
    <div className="grid gap-6 lg:grid-cols-2 lg:items-start">
      <Card>
        <Encabezado icono={UserRound} titulo="Tus datos">Tu nombre y el correo con el que inicias sesión.</Encabezado>
        <form onSubmit={guardarDatos} className="space-y-4" noValidate>
          {errorDatos && <Alert type="error">{errorDatos}</Alert>}
          <Input label="Nombre completo" value={datos.nombreCompleto} error={erroresDatos.nombreCompleto} autoComplete="name"
            onChange={(e) => setDatos({ ...datos, nombreCompleto: e.target.value })} />
          <Input label="Correo" type="email" value={datos.email} error={erroresDatos.email} autoComplete="email" spellCheck={false}
            ayuda="Es también tu usuario para iniciar sesión."
            onChange={(e) => setDatos({ ...datos, email: e.target.value })} />
          <dl className="grid grid-cols-2 gap-4 pt-1">
            <Dato titulo="Rol">{perfil.rol ?? 'Sin rol'}</Dato>
            <Dato titulo="Fecha de alta">{formatearFecha(perfil.fechaCreacion)}</Dato>
          </dl>
          <div className="flex justify-end pt-1">
            <Btn type="submit" size="md" disabled={guardandoDatos}>
              {guardandoDatos && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
              {guardandoDatos ? 'Guardando…' : 'Guardar cambios'}
            </Btn>
          </div>
        </form>
      </Card>

      <Card>
        <Encabezado icono={KeyRound} titulo="Contraseña">Para cambiarla necesitas la actual.</Encabezado>
        <form onSubmit={cambiarContrasena} className="space-y-4" noValidate>
          {errorContrasena && <Alert type="error">{errorContrasena}</Alert>}
          <Input label="Contraseña actual" type="password" value={contrasenas.actual} error={erroresContrasena.actual}
            autoComplete="current-password" onChange={(e) => setContrasenas({ ...contrasenas, actual: e.target.value })} />
          <Input label="Contraseña nueva" type="password" value={contrasenas.nueva} error={erroresContrasena.nueva}
            ayuda="8 caracteres o más, con al menos un número." autoComplete="new-password"
            onChange={(e) => setContrasenas({ ...contrasenas, nueva: e.target.value })} />
          <Input label="Repite la contraseña nueva" type="password" value={contrasenas.confirmar} error={erroresContrasena.confirmar}
            autoComplete="new-password" onChange={(e) => setContrasenas({ ...contrasenas, confirmar: e.target.value })} />
          <div className="flex justify-end pt-1">
            <Btn type="submit" size="md" disabled={cambiando}>
              {cambiando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
              {cambiando ? 'Cambiando…' : 'Cambiar contraseña'}
            </Btn>
          </div>
        </form>
      </Card>
    </div>
  );

  return (
    <Layout>
      <PageHeader title="Mi perfil" subtitle="Tus datos y tu contraseña." />
      {contenido}
    </Layout>
  );
}
