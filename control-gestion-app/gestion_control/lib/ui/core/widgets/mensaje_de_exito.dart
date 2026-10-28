import 'package:flutter/material.dart';

import 'aviso.dart';

/// El éxito de una acción (registrar, guardar, confirmar) como SnackBar flotante, con los colores y el ícono del aviso de
/// éxito (tarea 23): como el toast del panel, se va solo a los 5 segundos y no empuja el contenido. Queda encima de la
/// barra de acciones, y el lector de pantalla lo lee al aparecer. Si ya había uno, lo reemplaza.
void avisarExito(BuildContext context, String texto) {
  final mensajero = ScaffoldMessenger.of(context);
  mensajero.hideCurrentSnackBar();
  mensajero.showSnackBar(
    SnackBar(
      behavior: SnackBarBehavior.floating,
      duration: const Duration(seconds: 5),
      backgroundColor: TipoDeAviso.exito.fondo,
      elevation: 2,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(8),
        side: BorderSide(color: TipoDeAviso.exito.borde),
      ),
      content: Semantics(
        liveRegion: true,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.only(top: 2),
              child: Icon(TipoDeAviso.exito.icono, color: TipoDeAviso.exito.texto, size: 20),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(texto, style: TextStyle(color: TipoDeAviso.exito.texto, fontSize: 16)),
            ),
          ],
        ),
      ),
    ),
  );
}
