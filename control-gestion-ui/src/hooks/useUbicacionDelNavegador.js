import { useCallback, useState } from 'react';

/**
 * Pide la ubicación al navegador: en el panel reemplaza al GPS del celular, que la da solo en la app. Si el navegador
 * no puede o el usuario no la permite, se avisa para marcarla en el mapa.
 */
export function useUbicacionDelNavegador() {
  const [buscando, setBuscando] = useState(false);

  const tomar = useCallback(({ onUbicacion, onError }) => {
    if (!navigator.geolocation) {
      onError('Este navegador no puede dar tu ubicación: márcala en el mapa.');
      return;
    }
    setBuscando(true);
    navigator.geolocation.getCurrentPosition(
      ({ coords }) => {
        onUbicacion({ latitud: coords.latitude, longitud: coords.longitude }, Math.round(coords.accuracy));
        setBuscando(false);
      },
      () => {
        onError('No se pudo obtener tu ubicación: márcala en el mapa.');
        setBuscando(false);
      },
      { enableHighAccuracy: true, timeout: 10000 },
    );
  }, []);

  return { buscando, tomar };
}
