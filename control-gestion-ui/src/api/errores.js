// Los errores de la API llegan como ProblemDetails (RFC 9457): { title, detail, codigo, errors }.
// Si hay errores por campo se muestran ellos: el "detail" de la validación solo dice "Revisa los datos marcados."
export function mensajeDeError(err, porDefecto = 'Ocurrió un error.') {
  if (!err?.response) {
    return err?.isAxiosError ? 'No se pudo conectar con el servidor. Revisa tu conexión a internet y vuelve a intentarlo.' : porDefecto;
  }
  const { detail, errors } = err.response.data ?? {};
  const deLosCampos = Object.values(errors ?? {}).flat();
  return deLosCampos.length > 0 ? deLosCampos.join(' ') : detail ?? porDefecto;
}

// Los errores de validación por campo ({ email: ['…'] }), para mostrar cada uno debajo de su campo.
export function erroresPorCampo(err) {
  const errors = err?.response?.data?.errors ?? {};
  return Object.fromEntries(Object.entries(errors).map(([campo, mensajes]) => [campo, [].concat(mensajes).join(' ')]));
}
