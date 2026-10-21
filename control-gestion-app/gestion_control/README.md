# Control de Gestión: la app de los promotores

App Flutter para Android de los promotores de Del Campo SRL. La versión web sirve solo para verificarla en desarrollo.

## Cómo correrla

La URL de la API se pasa al compilar, en `API_URL`: es la raíz, sin `/api`, y nunca va escrita en el código.

**En el celular,** conectado por USB y en la misma red Wi-Fi que la PC:

1. En `ControlGestion.API/`, levanta la API para toda la red: `dotnet run -- --urls http://0.0.0.0:5124`.
2. Abre el puerto 5124 en el firewall de Windows (una sola vez, en una terminal de administrador):
   `New-NetFirewallRule -DisplayName "API Control de Gestion" -Direction Inbound -Protocol TCP -LocalPort 5124 -Action Allow -Profile Private`.
3. Corre la app con la IP de la PC: `flutter run --dart-define=API_URL=http://<IP de la PC>:5124`.

**Como web,** en el puerto que la API permite en desarrollo:

    flutter run -d web-server --web-port 5175 --dart-define=API_URL=http://localhost:5124

## Comprobar

    flutter analyze
    flutter test
