import { useCallback, useEffect, useRef, useState } from 'react';
import { CalendarX } from 'lucide-react';
import api from '../api/axios';
import { descargar } from '../api/descargas';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import { ErrorDeCarga, EstadoVacio } from '../components/ui';
import { avisar } from '../components/ui/avisar';
import { diaEnBolivia } from '../utils/formato';
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
  const ultimaPeticion = useRef(0);

  const cargar = useCallback(() => {
    const peticion = ++ultimaPeticion.current;
    const vigente = () => peticion === ultimaPeticion.current;
    return api.get(ruta, { params: parametros })
      .then(({ data }) => {
        if (!vigente()) return;
        setDatos(data);
        setErrorCarga('');
        setErroresFiltro({});
      })
      .catch((err) => {
        if (!vigente()) return;
        setDatos(null);
        setErroresFiltro(erroresPorCampo(err));
        setErrorCarga(mensajeDeError(err, mensajeDeFallo));
      })
      .finally(() => {
        if (!vigente()) return;
        setCargando(false);
        setReintentando(false);
      });
  }, [ruta, parametros, mensajeDeFallo]);

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

/** "Exportar Excel" de un reporte: descarga el archivo con los mismos filtros, o avisa el error en un toast. */
export function useExportar(ruta, parametros, prefijo) {
  const [exportando, setExportando] = useState(false);

  const exportar = async () => {
    setExportando(true);
    avisar.quitarError();
    try {
      await descargar(ruta, parametros, `${prefijo}_${diaEnBolivia().replaceAll('-', '')}.xlsx`);
    } catch (err) {
      avisar.error(mensajeDeError(err, 'No se pudo exportar el reporte a Excel.'));
    } finally {
      setExportando(false);
    }
  };

  return { exportar, exportando };
}
