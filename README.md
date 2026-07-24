# Tinta Viva

Aplicación de escritorio local y de código abierto para narradores, escritores y personas que quieran experimentar libremente con universos, personajes persistentes, roleplay y memoria con continuidad. El nombre técnico interno `PrivateCompanion` se conserva para mantener la compatibilidad con las rutas y bases existentes.

> Tus personajes, historias y recuerdos permanecen en tu computadora.

Tinta Viva separa la experiencia narrativa del modelo: personajes, escenas, recuerdos, relaciones y conversaciones pertenecen a la aplicación, mientras que la generación se delega a un servidor local o, de forma opcional, a un servidor remoto OpenAI-compatible.

## Escritura libre y privada

Tinta Viva está pensada primero como una herramienta narrativa. Admite aventura, drama, romance y también sexualidad o contenido erótico explícito entre personajes adultos cuando formen parte del relato. La aplicación no agrega filtros de contenido ni advertencias repetitivas: la respuesta concreta depende del modelo y del servicio elegidos, además de su configuración.

No hay una nube propia, cuentas ni telemetría. El modo local sólo acepta conexiones con `localhost` o `127.0.0.1`. El modo online es opcional y exige HTTPS: conserva fichas, recuerdos, imágenes e historial en el equipo, aunque envía al servidor elegido el contexto necesario para generar cada respuesta. Para una superficie especializada en relatos y roleplay adulto existe el proyecto independiente [Tinta Privada](https://github.com/Hefestion1989/tinta-privada), con almacenamiento separado.

## Estado actual

La versión 0.8.2 incluye:

[Ver las notas completas de Tinta Viva 0.8.2](docs/releases/0.8.2.md).

- interfaz Avalonia/.NET 10 para Windows;
- recuperación automática de Motor Tinta después de reiniciar la aplicación o de una caída del modelo;
- generación estable sin streaming para modelos LM Studio que se cierran al usar SSE;
- recuperación adaptativa por CPU cuando el backend Vulkan informa que la GPU dejó de responder;
- errores del motor visibles y mensajes fallidos vacíos ocultos en la conversación;
- **Motor Tinta** como recorrido local recomendado: detecta LM Studio y los modelos ya instalados sin pedir URLs ni alias;
- activación guiada de un único modelo local para evitar competencia silenciosa por la RAM;
- recomendación automática de un modelo rápido entre los disponibles;
- presets **Conversación ágil**, **Roleplay equilibrado** y **Narración extensa**, con diferencias reales de longitud, creatividad y composición del contexto;
- configuración manual y conexiones online conservadas como alternativas;
- perfiles de motor separados para ejecución local u online;
- configuración online guiada con presets para servidor propio, Venice AI y OpenRouter;
- asistente online de tres pasos con los campos técnicos relegados a una sección avanzada;
- pantalla de motores sin el panel narrativo lateral, para mantener el foco en la conexión;
- motor activo siempre visible mientras se prepara un perfil nuevo;
- detección y selección de modelos informados por el servidor, sin copiar identificadores a mano;
- comprobación segura de un perfil nuevo antes de activarlo, para no desplazar el motor anterior si la conexión falla;
- conexión remota genérica mediante HTTPS y protocolo OpenAI-compatible, sin dependencia obligatoria de OpenAI ni de otro proveedor comercial;
- tokens remotos opcionales protegidos por el Administrador de credenciales de Windows y nunca guardados en SQLite;
- historial, memoria, personajes, universos e imágenes conservados localmente aun cuando la generación use un servidor remoto;
- portada neutra para elegir identidad, personaje y conversación antes de entrar al chat;
- selección explícita del personaje, con número de revisión y fecha de última edición;
- separación reforzada de roles para que el modelo no adopte la identidad, acciones o pertenencias de la persona usuaria;
- álbum con imagen completa por defecto, zoom y centro de vista independientes del retrato y del fondo;
- acción visible para usar la imagen elegida como retrato o como fondo del chat;
- personajes editables y perfiles versionados;
- acceso directo para editar al personaje activo y agregarle imágenes desde la barra lateral;
- diez rasgos de personalidad configurables;
- conversaciones e historial persistente;
- escenas canónicas y sandbox;
- universos compartidos con resumen, canon, reglas, tono y procedencia;
- escenas estructuradas con lugar, momento, atmósfera, participantes, intención, conocimiento desigual y límites;
- memoria personal, episódica, emocional e identitaria;
- diario de sesión generado por el modelo configurado;
- estado relacional persistente;
- adaptador OpenAI-compatible para LM Studio, Ollama y llama.cpp;
- streaming, cancelación y arranque automático de un servidor local;
- SQLite local mediante la biblioteca SQLite de Windows;
- galería por personaje con importación múltiple de PNG, JPEG, WebP, BMP y GIF;
- copia privada de originales, deduplicación SHA-256 y miniaturas de memoria acotada;
- retrato por personaje, fondo visual de conversación y álbum lateral navegable;
- asignación visible de la imagen seleccionada como retrato desde la propia galería;
- encuadre no destructivo con zoom y foco independientes para retrato y fondo;
- botón de cierre visible en la esquina superior derecha;
- diseño más compacto que respeta el área útil de Windows, recorta cada panel y reserva el espacio del botón de cierre;
- múltiples identidades de usuario con conversaciones, recuerdos, diario y relación aislados;
- retrato privado e independiente para cada identidad de usuario;
- confirmación visible dentro del formulario al guardar o preparar una imagen;
- español rioplatense natural como valor predeterminado, sin forzar lunfardo ni muletillas;
- actualización versionada del esquema con backup automático previo;
- validación de edad mínima.

La importación manual ya usa el puerto común de biblioteca visual. ComfyUI y voz tienen límites de módulo definidos, pero la generación visual con identidad y el audio pertenecen a fases posteriores. No se simula consistencia de identidad con un prompt suelto.

## Datos locales

Los datos de uso se almacenan en:

`%LOCALAPPDATA%\PrivateCompanion`

Las imágenes importadas se copian en `media\originals`; nunca se mueven ni se modifican los archivos elegidos por el usuario.

Antes de actualizar una base anterior se crea una copia consistente en `backups`. El esquema actual es v8 y las migraciones conservan universos, personajes, identidades, conversaciones, recuerdos, imágenes y perfiles de motor existentes.

La aplicación sólo acepta endpoints HTTP en `localhost` o `127.0.0.1`; para conexiones remotas exige HTTPS. No contiene telemetría, cuentas ni sincronización.

Las bases, copias de seguridad, registros, imágenes importadas y modelos no forman parte de este repositorio.

## Requisitos

- Windows 10 u 11 de 64 bits;
- .NET 10 SDK para compilar desde el código;
- para usar el modo local, un servidor compatible —por ejemplo [LM Studio](https://lmstudio.ai/), Ollama o llama.cpp— y al menos un modelo de texto instalado;
- para usar el modo online opcional, una cuenta y una clave del servicio elegido o un servidor propio compatible por HTTPS.

## Primer uso

1. Instalá LM Studio y descargá al menos un modelo de texto.
2. Abrí **Configuración** en Tinta Viva.
3. En **Motor Tinta**, elegí el modelo y la forma de escribir.
4. Presioná **Activar Motor Tinta**. La aplicación inicia el servidor local, carga el modelo y configura la conexión.

Los perfiles manuales para LM Studio, Ollama, llama.cpp y servidores compatibles siguen disponibles debajo del recorrido recomendado.

El iniciador `.bat` es opcional. Si querés arranque automático, guardá tu propio `run-model.bat` junto al ejecutable, dentro de `scripts`, o en `%LOCALAPPDATA%\PrivateCompanion`.

## Motor online opcional

1. Abrí **Configuración**, presioná **＋ Nuevo online** y elegí un preset.
2. Usá el botón de ayuda para obtener tu propia clave en el servicio elegido, o escribí la URL HTTPS de un servidor propio.
3. Pegá la clave y presioná **Buscar modelos disponibles**.
4. Elegí el modelo y presioná **Probar conexión y usar este motor**.

La comprobación no reemplaza el motor que ya estaba activo si la nueva conexión falla. Los servicios incluidos son accesos directos de configuración, no afiliaciones ni recomendaciones permanentes; sus precios, políticas y disponibilidad dependen de cada operador.

## Desarrollo

La solución se divide en:

- `PrivateCompanion.Domain`: entidades e invariantes.
- `PrivateCompanion.Application`: casos de uso, contratos y composición del contexto.
- `PrivateCompanion.Infrastructure`: SQLite, repositorios, credenciales protegidas y proveedores locales o remotos.
- `PrivateCompanion.Desktop`: interfaz Avalonia/MVVM.
- `PrivateCompanion.Tests`: pruebas unitarias y de integración.

Compilar y probar desde PowerShell, dentro del repositorio:

```powershell
dotnet test .\PrivateCompanion.slnx
dotnet publish .\src\PrivateCompanion.Desktop\PrivateCompanion.Desktop.csproj -c Release -r win-x64 --self-contained true
```

## Licencia

El código se distribuye bajo la [licencia MIT](LICENSE). Los modelos, imágenes y textos importados conservan sus propias licencias y no se redistribuyen con la aplicación.
