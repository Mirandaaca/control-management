import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/services/error_de_api.dart';
import '../tema.dart';
import 'aviso.dart';
import 'cargando.dart';
import 'error_con_reintento.dart';

/// Una foto a pantalla completa, con zoom: el comprobante de un pago o la foto de un evento. [foto] la trae de la API;
/// [pie] va debajo, como quién la subió y cuándo (7.3.14, paso 3).
Future<void> verFoto(
  BuildContext context, {
  required String titulo,
  required String etiqueta,
  required FutureProvider<Uint8List> foto,
  String? pie,
}) => showDialog<void>(
  context: context,
  builder: (contexto) => Dialog.fullscreen(
    child: Scaffold(
      appBar: AppBar(
        title: Text(titulo),
        leading: IconButton(
          tooltip: 'Cerrar',
          icon: const Icon(Icons.close),
          onPressed: () => Navigator.of(contexto).pop(),
        ),
      ),
      body: Consumer(
        builder: (context, ref, _) => switch (ref.watch(foto)) {
          // Ocupa la pantalla: una foto chica, como una captura subida desde el panel, también se ve.
          AsyncData(:final value) => Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Expanded(
                child: InteractiveViewer(
                  child: SizedBox.expand(
                    child: Semantics(
                      image: true,
                      label: etiqueta,
                      child: Image.memory(
                        value,
                        fit: BoxFit.contain,
                        errorBuilder: (_, _, _) => const Padding(
                          padding: EdgeInsets.all(16),
                          child: Align(
                            alignment: Alignment.topCenter,
                            child: Aviso(
                              'El archivo está dañado o no es una imagen.',
                              titulo: 'No se pudo mostrar la foto.',
                            ),
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              ),
              if (pie != null)
                SafeArea(
                  top: false,
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Text(pie, style: const TextStyle(color: textoSecundario)),
                  ),
                ),
            ],
          ),
          AsyncError(:final error) => ErrorConReintento.de(
            ErrorDeApi.desde(error),
            alReintentar: () => ref.invalidate(foto),
          ),
          _ => const Cargando('Cargando la foto…'),
        },
      ),
    ),
  ),
);
