import 'package:flutter/material.dart';

// El tema de docs/diseno.md, sección 7 ("App: ThemeData de Flutter"). Si cambia allá, cambia aquí.

const marca600 = Color(0xFF2A5899);
const marca700 = Color(0xFF22477D);
const marca900 = Color(0xFF152B4C);
const bordeControl = Color(0xFF7B8798);
const texto = Color(0xFF0F172A);
const textoSecundario = Color(0xFF475569);
const fondo = Color(0xFFF8FAFC);
const error = Color(0xFFB91C1C);

final tema = ThemeData(
  useMaterial3: true,
  fontFamily: 'Inter',
  colorScheme: const ColorScheme.light(
    primary: marca600,
    onPrimary: Colors.white,
    primaryContainer: Color(0xFFDCE8F5),
    onPrimaryContainer: Color(0xFF1C3A66),
    secondary: marca700,
    onSecondary: Colors.white,
    surface: Colors.white,
    onSurface: texto,
    onSurfaceVariant: textoSecundario,
    outline: bordeControl,
    outlineVariant: Color(0xFFE2E8F0),
    error: error,
    onError: Colors.white,
  ),
  scaffoldBackgroundColor: fondo,
  appBarTheme: const AppBarTheme(
    backgroundColor: marca900,
    foregroundColor: Colors.white,
  ),
  textTheme: const TextTheme(
    headlineSmall: TextStyle(
      fontSize: 22,
      height: 28 / 22,
      fontWeight: FontWeight.w600,
    ),
    titleMedium: TextStyle(
      fontSize: 18,
      height: 24 / 18,
      fontWeight: FontWeight.w600,
    ),
    bodyLarge: TextStyle(fontSize: 16, height: 24 / 16),
    // Un Text sin estilo usa bodyMedium: sin esta línea saldría en 14 sp, y el cuerpo va en 16.
    bodyMedium: TextStyle(fontSize: 16, height: 24 / 16),
    labelLarge: TextStyle(
      fontSize: 16,
      height: 24 / 16,
      fontWeight: FontWeight.w600,
    ),
    labelMedium: TextStyle(
      fontSize: 14,
      height: 20 / 14,
      fontWeight: FontWeight.w500,
    ),
  ),
  filledButtonTheme: FilledButtonThemeData(
    style: FilledButton.styleFrom(
      minimumSize: const Size.fromHeight(52),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
    ),
  ),
  outlinedButtonTheme: OutlinedButtonThemeData(
    style: OutlinedButton.styleFrom(
      minimumSize: const Size(48, 48),
      side: const BorderSide(color: bordeControl),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
    ),
  ),
  inputDecorationTheme: InputDecorationTheme(
    filled: true,
    fillColor: Colors.white,
    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
    border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
    enabledBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(8),
      borderSide: const BorderSide(color: bordeControl),
    ),
    focusedBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(8),
      borderSide: const BorderSide(color: marca600, width: 2),
    ),
    errorBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(8),
      borderSide: const BorderSide(color: Color(0xFFDC2626)),
    ),
  ),
  cardTheme: CardThemeData(
    elevation: 0,
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(12),
      side: const BorderSide(color: Color(0xFFE2E8F0)),
    ),
  ),
);

/// Colores del estado del cliente (5.2). Siempre se muestran con su texto o su ícono.
class EstadoClienteColores {
  static const activo = Color(0xFFDC2626);
  static const activoTexto = Color(0xFFB91C1C);
  static const activoFondo = Color(0xFFFEF2F2);
  static const disponible = Color(0xFF15803D);
  static const disponibleTexto = Color(0xFF166534);
  static const disponibleFondo = Color(0xFFF0FDF4);
}
