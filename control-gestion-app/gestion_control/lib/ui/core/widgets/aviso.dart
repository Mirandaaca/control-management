import 'package:flutter/material.dart';

import '../../../data/services/error_de_api.dart';

/// Los cuatro tipos de mensaje de docs/diseno.md (sección 2, "Mensajes"), con su ícono: nunca van solo con el color.
enum TipoDeAviso {
  error(Color(0xFFB91C1C), Color(0xFFFEF2F2), Color(0xFFFECACA), Icons.error_outline),
  exito(Color(0xFF166534), Color(0xFFF0FDF4), Color(0xFFBBF7D0), Icons.check_circle_outline),
  aviso(Color(0xFF92400E), Color(0xFFFFFBEB), Color(0xFFFDE68A), Icons.warning_amber_outlined),
  informacion(Color(0xFF1C3A66), Color(0xFFEFF5FB), Color(0xFFB9D1EB), Icons.info_outline);

  const TipoDeAviso(this.texto, this.fondo, this.borde, this.icono);

  final Color texto;
  final Color fondo;
  final Color borde;
  final IconData icono;
}

/// Un enlace dentro de un aviso, como "Volver a capturar" en la tarjeta de ubicación.
class AccionDeAviso {
  const AccionDeAviso(this.texto, this.alTocar);

  final String texto;
  final VoidCallback? alTocar;
}

/// Un recuadro con un mensaje, como `Alert` del panel. El lector de pantalla lo lee apenas aparece.
///
/// Con [titulo], el título va en negrita y [texto] es el detalle, como en los avisos del 8.6.12 ("El GPS está
/// desactivado." y qué hacer). [accion] es un enlace dentro del recuadro, con su toque de 48 dp.
class Aviso extends StatelessWidget {
  const Aviso(
    this.texto, {
    super.key,
    this.tipo = TipoDeAviso.error,
    this.titulo,
    this.accion,
    this.icono,
    this.buscando = false,
  });

  /// El error de la API: con detalle (sin internet), el mensaje es el título.
  factory Aviso.deError(ErrorDeApi error, {Key? key, AccionDeAviso? accion}) => error.detalle == null
      ? Aviso(error.mensaje, key: key, accion: accion)
      : Aviso(error.detalle!, key: key, titulo: error.mensaje, accion: accion);

  final String texto;
  final TipoDeAviso tipo;
  final String? titulo;
  final AccionDeAviso? accion;

  /// Reemplaza el ícono del tipo: la ubicación usa `location_on`.
  final IconData? icono;

  /// Un indicador giratorio en lugar del ícono, mientras algo se busca ("Buscando la señal del GPS…").
  final bool buscando;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      container: true,
      liveRegion: true,
      child: Container(
        padding: EdgeInsets.fromLTRB(16, 12, 16, accion == null ? 12 : 0),
        decoration: BoxDecoration(
          color: tipo.fondo,
          border: Border.all(color: tipo.borde),
          borderRadius: BorderRadius.circular(8),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.only(top: 2),
              child: buscando
                  ? SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2, color: tipo.texto))
                  : Icon(icono ?? tipo.icono, color: tipo.texto, size: 20),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (titulo != null)
                    Text(
                      titulo!,
                      style: TextStyle(color: tipo.texto, fontWeight: FontWeight.w600),
                    ),
                  if (texto.isNotEmpty) Text(texto, style: TextStyle(color: tipo.texto)),
                  if (accion != null)
                    TextButton(
                      onPressed: accion!.alTocar,
                      style: TextButton.styleFrom(
                        foregroundColor: tipo == TipoDeAviso.informacion ? const Color(0xFF22477D) : tipo.texto,
                        minimumSize: const Size(48, 48),
                        padding: EdgeInsets.zero,
                        alignment: Alignment.centerLeft,
                      ),
                      child: Text(accion!.texto),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
