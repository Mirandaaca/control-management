import 'package:flutter/material.dart';

/// Un campo de texto con su etiqueta encima, como en el panel. Dentro del campo, la etiqueta de Material va en una sola
/// línea y con la letra del celular al 200 % se corta ("Correo electróni…"); encima, pasa de línea. MergeSemantics une
/// la etiqueta con el campo, así el lector de pantalla dice "Correo electrónico, campo de texto".
class CampoDeTexto extends StatelessWidget {
  const CampoDeTexto({
    super.key,
    required this.controller,
    required this.etiqueta,
    this.error,
    this.ayuda,
    this.ejemplo,
    this.keyboardType,
    this.autofillHints,
    this.textInputAction = TextInputAction.next,
    this.onSubmitted,
    this.oculto = false,
    this.textCapitalization = TextCapitalization.none,
  });

  final TextEditingController controller;
  final String etiqueta;
  final String? error;
  final String? ayuda;

  /// El texto de ejemplo, que termina en "…" (docs/diseno.md, sección 8).
  final String? ejemplo;
  final TextInputType? keyboardType;
  final Iterable<String>? autofillHints;
  final TextInputAction textInputAction;
  final ValueChanged<String>? onSubmitted;

  /// Para una contraseña: el texto no se ve, ni se corrige ni se sugiere.
  final bool oculto;
  final TextCapitalization textCapitalization;

  @override
  Widget build(BuildContext context) {
    final sinCorrector = oculto || keyboardType == TextInputType.emailAddress;
    return MergeSemantics(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(etiqueta, style: Theme.of(context).textTheme.labelMedium?.copyWith(color: const Color(0xFF334155))),
          const SizedBox(height: 4),
          TextField(
            controller: controller,
            obscureText: oculto,
            autocorrect: !sinCorrector,
            enableSuggestions: !sinCorrector,
            keyboardType: keyboardType,
            autofillHints: autofillHints,
            textInputAction: textInputAction,
            textCapitalization: textCapitalization,
            onSubmitted: onSubmitted,
            decoration: InputDecoration(
              hintText: ejemplo,
              hintMaxLines: 3,
              helperText: ayuda,
              helperMaxLines: 4,
              errorText: error,
              errorMaxLines: 4,
            ),
          ),
        ],
      ),
    );
  }
}

/// La casilla "Mostrar contraseña", debajo de los campos: dentro del campo, el botón se uniría a su semántica.
class MostrarContrasena extends StatelessWidget {
  const MostrarContrasena({
    super.key,
    required this.visible,
    required this.alCambiar,
    this.texto = 'Mostrar contraseña',
  });

  final bool visible;
  final ValueChanged<bool> alCambiar;
  final String texto;

  @override
  Widget build(BuildContext context) {
    return CheckboxListTile(
      value: visible,
      onChanged: (valor) => alCambiar(valor ?? false),
      title: Text(texto),
      controlAffinity: ListTileControlAffinity.leading,
      contentPadding: EdgeInsets.zero,
    );
  }
}
