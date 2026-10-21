import 'package:flutter/material.dart';

/// El estado de carga de una pantalla: un indicador giratorio y su texto ("Cargando tu perfil…").
class Cargando extends StatelessWidget {
  const Cargando(this.texto, {super.key});

  final String texto;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Semantics(
          container: true,
          liveRegion: true,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const CircularProgressIndicator(),
              const SizedBox(height: 16),
              Text(
                texto,
                textAlign: TextAlign.center,
                style: const TextStyle(color: Color(0xFF64748B)),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
