import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

const _colorDelError = Color(0xFFB91C1C);
const _colorDeLaEtiqueta = Color(0xFF334155);

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
    this.focusNode,
    this.inputFormatters,
    this.onChanged,
    this.textAlign = TextAlign.start,
    this.lineas = 1,
    this.debajo,
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
  final FocusNode? focusNode;
  final List<TextInputFormatter>? inputFormatters;
  final ValueChanged<String>? onChanged;

  /// Los montos y las cantidades van a la derecha (docs/diseno.md, "Formularios").
  final TextAlign textAlign;

  /// Más de una línea, como la dirección: el campo crece hasta ese alto.
  final int lineas;

  /// Algo que va debajo del campo, como el aviso del celular repetido.
  final Widget? debajo;

  @override
  Widget build(BuildContext context) {
    final sinCorrector = oculto || keyboardType == TextInputType.emailAddress;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        MergeSemantics(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(etiqueta, style: Theme.of(context).textTheme.labelMedium?.copyWith(color: _colorDeLaEtiqueta)),
              const SizedBox(height: 4),
              TextField(
                controller: controller,
                focusNode: focusNode,
                obscureText: oculto,
                autocorrect: !sinCorrector,
                enableSuggestions: !sinCorrector,
                keyboardType: keyboardType ?? (lineas > 1 ? TextInputType.multiline : null),
                autofillHints: autofillHints,
                textInputAction: lineas > 1 ? TextInputAction.newline : textInputAction,
                textCapitalization: textCapitalization,
                inputFormatters: inputFormatters,
                onChanged: onChanged,
                onSubmitted: onSubmitted,
                textAlign: textAlign,
                minLines: lineas > 1 ? 2 : null,
                maxLines: lineas,
                decoration: InputDecoration(
                  hintText: ejemplo,
                  hintMaxLines: 3,
                  helperText: ayuda,
                  helperMaxLines: 4,
                  error: error == null ? null : ErrorDeCampo(error!),
                ),
              ),
            ],
          ),
        ),
        ?debajo,
      ],
    );
  }
}

/// El error debajo de un campo: el mensaje en rojo-700 con el ícono `circle-alert` (docs/diseno.md, "Formularios").
class ErrorDeCampo extends StatelessWidget {
  const ErrorDeCampo(this.mensaje, {super.key});

  final String mensaje;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Padding(
          padding: EdgeInsets.only(top: 1),
          child: Icon(Icons.error_outline, size: 18, color: _colorDelError),
        ),
        const SizedBox(width: 6),
        Expanded(
          child: Text(mensaje, style: const TextStyle(color: _colorDelError, fontSize: 14, height: 20 / 14)),
        ),
      ],
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
