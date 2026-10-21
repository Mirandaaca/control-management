import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

/// Lo que ve quien abre una pantalla sin tener ninguno de sus permisos (docs/diseno.md, "Estados de una pantalla").
class SinAccesoScreen extends StatelessWidget {
  const SinAccesoScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Sin acceso')),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.lock_outline, size: 32, color: Color(0xFF64748B)),
              const SizedBox(height: 12),
              Semantics(
                header: true,
                child: Text(
                  'No tienes acceso a esta pantalla',
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              ),
              const SizedBox(height: 8),
              const Text('Si la necesitas, pídele el permiso al administrador.', textAlign: TextAlign.center),
              const SizedBox(height: 16),
              OutlinedButton(onPressed: () => context.go('/'), child: const Text('Ir al inicio')),
            ],
          ),
        ),
      ),
    );
  }
}
