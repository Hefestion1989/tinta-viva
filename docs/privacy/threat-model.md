# Privacidad y threat model inicial

## Garantías actuales

- Sin cuentas ni telemetría.
- Sin sincronización.
- Endpoint del modelo limitado a loopback.
- Datos bajo `%LOCALAPPDATA%\PrivateCompanion` con permisos heredados del usuario de Windows.
- Logs conversacionales deshabilitados: el servidor externo puede mantener sus propios logs y debe configurarse por separado.

## No garantizado todavía

SQLite y los medios no están cifrados por la aplicación en la versión 0.3. BitLocker y la sesión de Windows protegen frente a pérdida física, pero otro proceso ejecutado bajo la misma cuenta puede leer los archivos.

El vault futuro debe cifrar base, imágenes, miniaturas y backups como una unidad. Cifrar solamente mensajes produciría una falsa sensación de seguridad y dificultaría la búsqueda.

## Confianza

- Se confía en el usuario local y en el modelo configurado.
- No se ejecutan tool calls emitidos por el modelo.
- El comando de inicio del servidor solo puede modificarse desde la configuración local.
- Nunca se acepta un personaje menor de 18 años.
