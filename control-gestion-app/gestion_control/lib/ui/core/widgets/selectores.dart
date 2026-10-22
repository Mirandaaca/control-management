import 'package:flutter/material.dart';

import '../tema.dart';
import 'campo_de_texto.dart';

/// Un campo que se elige en vez de escribirse, con la etiqueta encima, como [CampoDeTexto]. El ícono dice cómo se
/// elige (docs/diseno.md, "Selectores"): ▼ abre una lista corta y ⇅ una pantalla de búsqueda.
class CampoDeEleccion extends StatelessWidget {
  const CampoDeEleccion({
    super.key,
    required this.etiqueta,
    required this.valor,
    required this.alTocar,
    this.buscando = false,
    this.ejemplo = 'Elige…',
    this.error,
    this.ayuda,
    this.focusNode,
    this.icono,
  });

  final String etiqueta;

  /// Lo elegido, o null si todavía no se eligió nada.
  final String? valor;
  final VoidCallback? alTocar;

  /// ⇅: se elige buscando, en una pantalla aparte (cliente, producto).
  final bool buscando;
  final String ejemplo;
  final String? error;
  final String? ayuda;

  /// Para darle el foco cuando es el primer error del formulario.
  final FocusNode? focusNode;

  /// Otro ícono en lugar de ▼ o ⇅, como el calendario de una fecha.
  final IconData? icono;

  @override
  Widget build(BuildContext context) {
    final habilitado = alTocar != null;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ExcludeSemantics(
          child: Text(
            etiqueta,
            style: Theme.of(context).textTheme.labelMedium?.copyWith(color: const Color(0xFF334155)),
          ),
        ),
        const SizedBox(height: 4),
        // Su propio nodo: dentro de una Card (la línea de producto), la Card lo absorbía y el botón ocupaba toda la
        // tarjeta, con el centro sobre "Cantidad". Voice Access toca ese centro.
        Semantics(
          container: true,
          button: true,
          enabled: habilitado,
          label: etiqueta,
          // El campo excluye su propia semántica: el error se lee junto al valor.
          value: error == null ? (valor ?? ejemplo) : '${valor ?? ejemplo}. $error',
          // La ayuda va en la pista: la del producto es su precio y su stock.
          hint: [?ayuda, if (buscando) 'Abre la búsqueda'].join('. '),
          excludeSemantics: true,
          child: InkWell(
            focusNode: focusNode,
            onTap: alTocar,
            borderRadius: BorderRadius.circular(8),
            child: InputDecorator(
              isEmpty: valor == null,
              decoration: InputDecoration(
                enabled: habilitado,
                hintText: ejemplo,
                hintMaxLines: 2,
                helperText: ayuda,
                helperMaxLines: 4,
                error: error == null ? null : ErrorDeCampo(error!),
                suffixIcon: Icon(
                  icono ?? (buscando ? Icons.unfold_more : Icons.arrow_drop_down),
                  color: textoSecundario,
                ),
              ),
              child: valor == null ? null : Text(valor!),
            ),
          ),
        ),
      ],
    );
  }
}

/// La lista corta de un campo ▼ (la zona del cliente): se abre desde abajo y devuelve lo elegido, o null.
Future<T?> elegirDeLaLista<T>(
  BuildContext context, {
  required String titulo,
  required List<(T, String)> opciones,
  T? actual,
}) {
  return showModalBottomSheet<T>(
    context: context,
    showDragHandle: true,
    isScrollControlled: true,
    builder: (contexto) => SafeArea(
      child: ListView(
        shrinkWrap: true,
        padding: const EdgeInsets.only(bottom: 16),
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
            child: Semantics(
              header: true,
              container: true,
              child: Text(titulo, style: Theme.of(contexto).textTheme.titleMedium),
            ),
          ),
          for (final (valor, texto) in opciones)
            ListTile(
              minTileHeight: 48,
              title: Text(texto),
              selected: valor == actual,
              trailing: valor == actual ? const Icon(Icons.check) : null,
              onTap: () => Navigator.of(contexto).pop(valor),
            ),
        ],
      ),
    ),
  );
}
