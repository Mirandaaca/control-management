import { useCallback, useEffect, useState } from 'react';
import { CalendarX } from 'lucide-react';
import api from '../api/axios';
import { descargar } from '../api/descargas';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import { ErrorDeCarga, EstadoVacio } from '../components/ui';
import { avisar } from '../components/ui/avisar';
import { diaEnBolivia } from '../utils/formato';
import { usePeticionVigente } from './usePeticionVigente';
import { useRecarga } from './useRecarga';

/**
 * Pide un reporte cada vez que cambian sus parámetros (un objeto memorizado con useMemo). Si la API rechaza los
 * filtros, el reporte anterior no queda a la vista: ya no corresponde a lo que se pidió. Una respuesta que llega
 * después de otra más nueva se descarta.
 */
export function useReporte(ruta, parametros, mensajeDeFallo) {
  const [datos, setDatos] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [erroresFiltro, setErroresFiltro] = useState({});
  const nuevaPeticion = usePeticionVigente();

  const cargar = useCallback(() => {
    const siVigente = nuevaPeticion();
    return api.get(ruta, { params: parametros })
      .then(siVigente(({ data }) => {
        setDatos(data);
        setErrorCarga('');
        setErroresFiltro({});
      }))
      .catch(siVigente((err) => {
        setDatos(null);
        setErroresFiltro(erroresPorCampo(err));
        setErrorCarga(mensajeDeError(err, mensajeDeFallo));
      }))
      .finally(siVigente(() => {
        setCargando(false);
        setReintentando(false);
      }));
  }, [ruta, parametros, mensajeDeFallo, nuevaPeticion]);

  const { recargando, terminar } = useRecarga(cargar);
  useEffect(() => { cargar().finally(() => terminar(cargar)); }, [cargar, terminar]);

  const reintentar = () => {
    setReintentando(true);
    cargar();
  };

  // Lo que reemplaza a las filas de la tabla si falló; null si hay datos para mostrar. Mientras llega la primera carga,
  // la tabla muestra su esqueleto (cargando), y al cambiar los filtros, la barra de recarga (recargando).
  let estado = null;
  if (Object.keys(erroresFiltro).length > 0) estado = (
    <EstadoVacio titulo="Revisa los filtros" icono={CalendarX}>El error está marcado en su campo.</EstadoVacio>
  );
  else if (errorCarga) estado = <ErrorDeCarga mensaje={errorCarga} onReintentar={reintentar} reintentando={reintentando} />;

  return { datos, cargando, recargando, erroresFiltro, estado };
}

const FORMATOS = {
  excel: { extension: 'xlsx', nombre: 'Excel' },
  pdf: { extension: 'pdf', nombre: 'PDF' },
};

/**
 * "Exportar Excel" y "Exportar PDF" de un reporte (el PDF, desde la tarea 33): descarga `${rutaBase}/excel` o
 * `${rutaBase}/pdf` con los mismos filtros, o avisa el error en un toast. `exportando` dice cuál se está bajando.
 */
export function useExportar(rutaBase, parametros, prefijo) {
  const [exportando, setExportando] = useState(null);

  const exportar = async (formato) => {
    const { extension, nombre } = FORMATOS[formato];
    setExportando(formato);
    avisar.quitarError();
    try {
      await descargar(`${rutaBase}/${formato}`, parametros, `${prefijo}_${diaEnBolivia().replaceAll('-', '')}.${extension}`);
    } catch (err) {
      avisar.error(mensajeDeError(err, `No se pudo exportar el reporte a ${nombre}.`));
    } finally {
      setExportando(null);
    }
  };

  return { exportar, exportando };
}
