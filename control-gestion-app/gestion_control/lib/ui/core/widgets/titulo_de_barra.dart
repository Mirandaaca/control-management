import 'package:flutter/material.dart';

/// El título de la barra superior cuando el del diseño es largo ("Nueva hoja de pedido"). La barra tiene una sola línea:
/// con la letra al 200 %, el título se achica hasta entrar, en vez de cortarse con "…". El resto de la pantalla sigue al
/// 200 %.
class TituloDeBarra extends StatelessWidget {
  const TituloDeBarra(this.texto, {super.key});

  final String texto;

  @override
  Widget build(BuildContext context) =>
      FittedBox(fit: BoxFit.scaleDown, alignment: AlignmentDirectional.centerStart, child: Text(texto));
}
