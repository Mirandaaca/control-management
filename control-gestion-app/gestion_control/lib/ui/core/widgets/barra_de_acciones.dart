import 'package:flutter/material.dart';

// Menos que los 24 de Material: así "Guardar" entra en un tercio del ancho, como en el 8.6.5.
const _relleno = 12.0;

/// Una acción de la barra de abajo. Mientras se envía, se deshabilita y cambia su texto ("Guardando…").
class Accion {
  const Accion(this.texto, this.alTocar, {this.textoEnviando, this.enviando = false});

  final String texto;
  final VoidCallback? alTocar;
  final String? textoEnviando;
  final bool enviando;

  String get rotulo => enviando ? (textoEnviando ?? texto) : texto;
}

/// Lo de abajo de una pantalla, al alcance del pulgar (docs/diseno.md, "Botones"): la acción principal a lo ancho o,
/// con una secundaria, las dos lado a lado. Arriba de los botones van, si hay, el total o el aviso de un envío que
/// falló: así se ven donde está quien tocó el botón.
class BarraDeAcciones extends StatelessWidget {
  const BarraDeAcciones({
    super.key,
    required this.principal,
    this.secundaria,
    this.arriba = const [],
    this.conEstiloSecundario = false,
  });

  final Accion principal;

  /// Una sola acción con el estilo secundario: "Terminar ruta" mientras quedan visitas sin marcar (8.6.9).
  final bool conEstiloSecundario;
  final Accion? secundaria;
  final List<Widget> arriba;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      child: DecoratedBox(
        decoration: const BoxDecoration(
          border: Border(top: BorderSide(color: Color(0xFFE2E8F0))),
        ),
        child: SafeArea(
          top: false,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                for (final widget in arriba) ...[widget, const SizedBox(height: 12)],
                if (secundaria != null)
                  _DosAcciones(principal, secundaria!)
                else if (conEstiloSecundario)
                  _Secundaria(principal)
                else
                  _Principal(principal),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// La secundaria a la izquierda, en un tercio, y la principal a la derecha, en dos tercios. Si con la letra grande sus
/// textos no entran, se apilan, con la principal abajo.
class _DosAcciones extends StatelessWidget {
  const _DosAcciones(this.principal, this.secundaria);

  final Accion principal;
  final Accion secundaria;

  static const _separacion = 8.0;

  // El ancho que pide un botón: su texto en una línea, el relleno de cada lado, el borde y el indicador si está enviando.
  double _anchoDe(BuildContext context, Accion accion) {
    final pintor = TextPainter(
      text: TextSpan(text: accion.rotulo, style: Theme.of(context).textTheme.labelLarge),
      textScaler: MediaQuery.textScalerOf(context),
      textDirection: Directionality.of(context),
      maxLines: 1,
    )..layout();
    return pintor.width + _relleno * 2 + 2 + (accion.enviando ? 30 : 0);
  }

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, limites) {
        final tercio = (limites.maxWidth - _separacion) / 3;
        final entran = _anchoDe(context, secundaria) <= tercio && _anchoDe(context, principal) <= tercio * 2;
        if (!entran) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _Secundaria(secundaria),
              const SizedBox(height: _separacion),
              _Principal(principal),
            ],
          );
        }
        return Row(
          children: [
            Expanded(child: _Secundaria(secundaria)),
            const SizedBox(width: _separacion),
            Expanded(flex: 2, child: _Principal(principal)),
          ],
        );
      },
    );
  }
}

class _Principal extends StatelessWidget {
  const _Principal(this.accion);

  final Accion accion;

  @override
  Widget build(BuildContext context) {
    return FilledButton(
      style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(horizontal: _relleno)),
      onPressed: accion.enviando ? null : accion.alTocar,
      child: _Rotulo(accion, color: Colors.white),
    );
  }
}

class _Secundaria extends StatelessWidget {
  const _Secundaria(this.accion);

  final Accion accion;

  @override
  Widget build(BuildContext context) {
    // Lado a lado, los dos botones tienen el alto de la acción principal (8.6.5).
    return OutlinedButton(
      style: OutlinedButton.styleFrom(
        minimumSize: const Size(48, 52),
        padding: const EdgeInsets.symmetric(horizontal: _relleno),
      ),
      onPressed: accion.enviando ? null : accion.alTocar,
      child: _Rotulo(accion),
    );
  }
}

class _Rotulo extends StatelessWidget {
  const _Rotulo(this.accion, {this.color});

  final Accion accion;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        if (accion.enviando) ...[
          SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2, color: color)),
          const SizedBox(width: 12),
        ],
        Flexible(child: Text(accion.rotulo, textAlign: TextAlign.center)),
      ],
    );
  }
}
