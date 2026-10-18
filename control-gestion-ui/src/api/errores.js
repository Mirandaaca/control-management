// Los errores de la API llegan como ProblemDetails (RFC 9457): { title, detail, codigo, errors }.
// Si hay errores por campo se muestran ellos: el "detail" de la validación solo dice "Revisa los datos marcados."
export function mensajeDeError(err, porDefecto = 'Ocurrió un error.') {
  if (!err?.response) return err?.isAxiosError ? 'No se pudo conectar con el servidor.' : porDefecto;
  const { detail, errors } = err.response.data ?? {};
  const deLosCampos = Object.values(errors ?? {}).flat();
  return deLosCampos.length > 0 ? deLosCampos.join(' ') : detail ?? porDefecto;
}
