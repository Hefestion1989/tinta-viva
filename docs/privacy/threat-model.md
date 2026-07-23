# Privacidad y threat model inicial

## Garantías actuales

- Sin cuentas ni telemetría.
- Sin nube propia ni sincronización de fichas, historial, memoria o imágenes.
- Los perfiles locales sólo aceptan endpoints en loopback.
- Los perfiles remotos requieren HTTPS y no siguen redirecciones.
- Los tokens remotos se guardan en el Administrador de credenciales de Windows, no en SQLite.
- Datos bajo `%LOCALAPPDATA%\PrivateCompanion` con permisos heredados del usuario de Windows.
- Logs conversacionales propios deshabilitados.

Al usar un motor online, Tinta Viva envía al servicio elegido el contexto necesario para generar cada respuesta. Ese servicio puede aplicar sus propias políticas, conservar registros o procesar metadatos de conexión; debe evaluarse y configurarse por separado.

## No garantizado todavía

SQLite y los medios no están cifrados por la aplicación en la versión 0.7.2. BitLocker y la sesión de Windows protegen frente a pérdida física, pero otro proceso ejecutado bajo la misma cuenta puede leer los archivos.

El vault futuro debe cifrar base, imágenes, miniaturas y backups como una unidad. Cifrar solamente mensajes produciría una falsa sensación de seguridad y dificultaría la búsqueda.

## Confianza

- Se confía en el usuario local y en el modelo o servicio configurado.
- No se ejecutan tool calls emitidos por el modelo.
- El comando de inicio del servidor solo puede modificarse desde la configuración local.
- Nunca se acepta un personaje menor de 18 años.
