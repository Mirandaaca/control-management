import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/sesion.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/core/router.dart';
import 'package:gestion_control/ui/sesion/arranque_screen.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';

import '../ayudantes.dart';

void main() {
  group('al abrir la app', () {
    Future<EstadoDeSesion> abrir(SesionRepositoryFalso repositorio) async {
      final contenedor = ProviderContainer.test(overrides: [sesionRepositoryProvider.overrideWithValue(repositorio)]);
      contenedor.read(sesionProvider);
      await contenedor.read(sesionProvider.notifier).restaurar();
      return contenedor.read(sesionProvider);
    }

    test('sin token guardado va al inicio de sesión, sin aviso', () async {
      final estado = await abrir(SesionRepositoryFalso());

      expect(estado, isA<SinSesion>().having((e) => e.aviso, 'aviso', isNull));
    });

    test('con token guardado trae la sesión de la API', () async {
      final estado = await abrir(SesionRepositoryFalso(token: 'abc'));

      expect(estado, isA<ConSesion>().having((e) => e.sesion.nombreCompleto, 'nombre', 'Pablo Promotor'));
    });

    test('si la API responde 401, borra el token y avisa que la sesión terminó', () async {
      final repositorio = SesionRepositoryFalso(
        token: 'abc',
        alPedirYo: () => throw const ErrorDeApi(mensaje: 'Tu sesión terminó: vuelve a entrar.', estado: 401),
      );

      final estado = await abrir(repositorio);

      expect(estado, isA<SinSesion>().having((e) => e.aviso, 'aviso', avisoSesionTerminada));
      expect(repositorio.token, isNull);
    });

    test('sin conexión no usa permisos guardados: muestra el error', () async {
      final estado = await abrir(SesionRepositoryFalso(token: 'abc', alPedirYo: () => throw sinConexion));

      expect(estado, isA<SesionSinConexion>().having((e) => e.mensaje, 'mensaje', sinConexion.mensaje));
    });
  });

  test('un 401 en cualquier pantalla termina la sesión con el aviso', () {
    final repositorio = SesionRepositoryFalso(token: 'abc');
    final contenedor = ProviderContainer.test(
      overrides: [
        sesionRepositoryProvider.overrideWithValue(repositorio),
        sesionProvider.overrideWith(() => SesionFija(const ConSesion(sesionDePrueba))),
      ],
    );

    contenedor.read(sesionProvider.notifier).terminada();

    expect(contenedor.read(sesionProvider), isA<SinSesion>().having((e) => e.aviso, 'aviso', avisoSesionTerminada));
  });

  group('redirigir', () {
    test('mientras se abre la sesión, o si la API no responde, se queda en el arranque', () {
      expect(redirigir(const SesionCargando(), Rutas.perfil), Rutas.arranque);
      expect(redirigir(const SesionSinConexion('x'), Rutas.menu), Rutas.arranque);
      expect(redirigir(const SesionCargando(), Rutas.arranque), isNull);
    });

    test('sin sesión, va al inicio de sesión', () {
      expect(redirigir(const SinSesion(), Rutas.menu), Rutas.entrar);
      expect(redirigir(const SinSesion(aviso: avisoSesionTerminada), Rutas.perfil), Rutas.entrar);
      expect(redirigir(const SinSesion(), Rutas.entrar), isNull);
    });

    test('con sesión, el inicio de sesión y el arranque llevan al menú, y las demás pantallas se quedan', () {
      expect(redirigir(const ConSesion(sesionDePrueba), Rutas.entrar), Rutas.menu);
      expect(redirigir(const ConSesion(sesionDePrueba), Rutas.arranque), Rutas.menu);
      expect(redirigir(const ConSesion(sesionDePrueba), Rutas.perfil), isNull);
    });
  });

  group('una pantalla con permisos', () {
    Widget pantalla(Set<String> permisos) => conLaApp(
      ConPermiso(permisos: [Permisos.ventas.registrarHp], child: const Text('Nueva HP')),
      overrides: [
        sesionProvider.overrideWith(
          () => SesionFija(
            ConSesion(
              Sesion(id: 'u1', email: 'a@test.local', nombreCompleto: 'Prueba', rol: 'Promotor', permisos: permisos),
            ),
          ),
        ),
      ],
    );

    testWidgets('sin ninguno de ellos muestra "Sin acceso"', (tester) async {
      await tester.pumpWidget(pantalla({Permisos.pedidos.verBandeja}));

      expect(find.text('No tienes acceso a esta pantalla'), findsOneWidget);
      expect(find.text('Nueva HP'), findsNothing);
    });

    testWidgets('con uno de ellos muestra la pantalla', (tester) async {
      await tester.pumpWidget(pantalla({Permisos.ventas.registrarHp}));

      expect(find.text('Nueva HP'), findsOneWidget);
    });
  });

  testWidgets('el arranque sin conexión muestra el error y reintenta', (tester) async {
    final repositorio = SesionRepositoryFalso(token: 'abc', alPedirYo: () => throw sinConexion);
    await tester.pumpWidget(
      conLaApp(const ArranqueScreen(), overrides: [sesionRepositoryProvider.overrideWithValue(repositorio)]),
    );
    await tester.pumpAndSettle();

    expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);
    repositorio.alPedirYo = () async => sesionDePrueba;
    await tester.tap(find.text('Reintentar'));
    await tester.pump();

    expect(repositorio.pedidosDeYo, 2);
  });
}
