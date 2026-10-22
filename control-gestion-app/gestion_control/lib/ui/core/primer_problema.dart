import 'package:flutter/widgets.dart';

/// Un lugar del formulario que puede tener un problema: su llave, para llevar la vista hasta él, y su foco.
typedef LugarDelFormulario = ({GlobalKey llave, FocusNode? foco});

/// Lleva la vista al primer problema del formulario y le da el foco, como `mostrarPrimerProblema` del panel: quien
/// guarda está abajo, y un error arriba quedaría fuera de la vista (docs/diseno.md, "Formularios"). Los lugares van en
/// el orden de la pantalla; [tieneProblema] dice cuáles tienen uno.
void mostrarPrimerProblema(
  Iterable<LugarDelFormulario> lugares,
  bool Function(LugarDelFormulario lugar) tieneProblema,
) {
  for (final lugar in lugares) {
    if (!tieneProblema(lugar)) continue;
    final contexto = lugar.llave.currentContext;
    if (contexto == null) return;
    Scrollable.ensureVisible(contexto, duration: const Duration(milliseconds: 200), alignment: 0.1);
    lugar.foco?.requestFocus();
    return;
  }
}
