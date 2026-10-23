import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/sesion.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/sesion/inicio_de_sesion_screen.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';

import '../ayudantes.dart';

void main() {
  late SesionRepositoryFalso repositorio;

  Future<void> abrir(WidgetTester tester, {String? aviso, double letra = 1}) async {
    tamanoDeCelular(tester, letra: letra);
    await tester.pumpWidget(
      conLaApp(
        const InicioDeSesionScreen(),
        overrides: [
          sesionRepositoryProvider.overrideWithValue(repositorio),
          sesionProvider.overrideWith(() => SesionFija(SinSesion(aviso: aviso))),
        ],
      ),
    );
  }

  Future<void> escribir(
    WidgetTester tester, {
    String correo = 'promotor@test.local',
    String contrasena = 'Clave2026',
  }) async {
    await tester.enterText(campo('Correo electrónico'), correo);
    await tester.enterText(campo('Contraseña'), contrasena);
  }

  Finder botonIngresar() => find.byType(FilledButton);

  Future<void> tocarIngresar(WidgetTester tester) async {
    await tester.tap(botonIngresar());
    await tester.pump();
  }

  setUp(() => repositorio = SesionRepositoryFalso());

  testWidgets('al abrirse, despierta la API una vez', (tester) async {
    await abrir(tester);
    await tester.pump();

    expect(repositorio.despertares, 1);
  });

  testWidgets('con los datos vacíos, los errores van en sus campos y no se envía nada', (tester) async {
    await abrir(tester);

    await tocarIngresar(tester);

    expect(find.text('Escribe tu correo.'), findsOneWidget);
    expect(find.text('Escribe tu contraseña.'), findsOneWidget);
    expect(repositorio.inicios, isEmpty);
  });

  testWidgets('envía el correo sin espacios y la contraseña tal cual', (tester) async {
    await abrir(tester);
    await escribir(tester, correo: '  promotor@test.local ', contrasena: ' Clave 2026');

    await tocarIngresar(tester);

    expect(repositorio.inicios.single, ('promotor@test.local', ' Clave 2026'));
  });

  for (final (codigo, texto) in [
    ('credenciales_incorrectas', 'Credenciales incorrectas.'),
    ('usuario_inactivo', 'Usuario inactivo.'),
  ]) {
    testWidgets('muestra "$texto" de la API', (tester) async {
      repositorio.alIniciar = (_, _) => throw ErrorDeApi(mensaje: texto, codigo: codigo, estado: 401);
      await abrir(tester);
      await escribir(tester);

      await tester.tap(botonIngresar());
      await tester.pump();

      expect(find.text(texto), findsOneWidget);
    });
  }

  testWidgets('el botón se deshabilita mientras se envía', (tester) async {
    final respuesta = Completer<Sesion>();
    repositorio.alIniciar = (_, _) => respuesta.future;
    await abrir(tester);
    await escribir(tester);

    await tocarIngresar(tester);

    expect(tester.widget<FilledButton>(botonIngresar()).onPressed, isNull);
    expect(find.text('Ingresando…'), findsOneWidget);
    await tester.tap(botonIngresar());
    expect(repositorio.inicios, hasLength(1));

    respuesta.completeError(sinConexion);
    await tester.pump();
    expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);
    expect(tester.widget<FilledButton>(botonIngresar()).onPressed, isNotNull);
  });

  testWidgets('avisa que la sesión terminó, y el aviso se va al volver a intentar', (tester) async {
    repositorio.alIniciar = (_, _) => throw const ErrorDeApi(mensaje: 'Credenciales incorrectas.', estado: 401);
    await abrir(tester, aviso: avisoSesionTerminada);
    expect(find.text(avisoSesionTerminada), findsOneWidget);

    await escribir(tester);
    await tocarIngresar(tester);

    expect(find.text(avisoSesionTerminada), findsNothing);
  });

  testWidgets('el título es un encabezado propio, y cada campo se lee con su etiqueta', (tester) async {
    final semantica = tester.ensureSemantics();
    await abrir(tester);

    expect(tester.getSemantics(find.text('Inicia sesión')), matchesSemantics(label: 'Inicia sesión', isHeader: true));
    final correo = tester.getSemantics(campo('Correo electrónico'));
    expect(correo, isSemantics(isTextField: true));
    // Después de la etiqueta, el lector lee el ejemplo (nombre@empresa.com).
    expect(correo.label, startsWith('Correo electrónico'));
    semantica.dispose();
  });

  testWidgets('se puede mostrar la contraseña', (tester) async {
    await abrir(tester);
    TextField contrasena() => tester.widget<TextField>(campo('Contraseña'));
    expect(contrasena().obscureText, isTrue);

    await tester.tap(find.text('Mostrar contraseña'));
    await tester.pump();

    expect(contrasena().obscureText, isFalse);
  });

  testWidgets('con la letra al 200 % nada se corta, y cumple las guías de accesibilidad', (tester) async {
    final semantica = tester.ensureSemantics();
    await abrir(tester, aviso: avisoSesionTerminada, letra: 2);
    // Con la letra grande el botón queda más abajo: se envía con la tecla "Listo" del teclado.
    await tester.showKeyboard(campo('Contraseña'));
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pump();

    expect(tester.takeException(), isNull);
    await tester.scrollUntilVisible(botonIngresar(), 200, scrollable: find.byType(Scrollable).first);
    expect(find.text('Escribe tu contraseña.'), findsOneWidget);
    await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
    await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));
    await expectLater(tester, meetsGuideline(textContrastGuideline));
    semantica.dispose();
  });
}
