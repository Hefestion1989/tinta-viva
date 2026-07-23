# Tinta Viva

Aplicación de escritorio local y de código abierto para narradores, escritores y personas que quieran experimentar libremente con universos, personajes persistentes, roleplay y memoria con continuidad. El nombre técnico interno `PrivateCompanion` se conserva para mantener la compatibilidad con las rutas y bases existentes.

> Tus personajes, historias y recuerdos permanecen en tu computadora.

Tinta Viva separa la experiencia narrativa del modelo: personajes, escenas, recuerdos, relaciones y conversaciones pertenecen a la aplicación, mientras que la generación se delega a un servidor local compatible con la API de OpenAI.

## Escritura libre y privada

Tinta Viva está pensada primero como una herramienta narrativa. Admite aventura, drama, romance y también sexualidad o contenido erótico explícito entre personajes adultos cuando formen parte del relato. La aplicación no agrega filtros de contenido ni advertencias repetitivas: la respuesta concreta depende del modelo local elegido y de su configuración.

No hay una nube propia, cuentas ni telemetría. La aplicación solo acepta conexiones con `localhost` o `127.0.0.1`, por lo que textos, fichas, recuerdos e imágenes permanecen bajo el control del usuario. Para una superficie especializada en relatos y roleplay adulto existe el proyecto independiente [Tinta Privada](https://github.com/Hefestion1989/tinta-privada), con almacenamiento separado.

## Estado actual

La versión 0.6.2 incluye:

- interfaz Avalonia/.NET 10 para Windows;
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
- diario de sesión generado por el modelo local;
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

Antes de actualizar una base anterior se crea una copia consistente en `backups`. La migración v0.6 agrega el retrato de identidad y conserva universos, personajes, conversaciones, recuerdos e imágenes existentes.

La aplicación solo acepta endpoints HTTP en `localhost` o `127.0.0.1`. No contiene telemetría, cuentas ni sincronización.

Las bases, copias de seguridad, registros, imágenes importadas y modelos no forman parte de este repositorio.

## Requisitos

- Windows 10 u 11 de 64 bits;
- .NET 10 SDK para compilar desde el código;
- un servidor local compatible, por ejemplo [LM Studio](https://lmstudio.ai/), Ollama o llama.cpp;
- al menos un modelo de texto instalado en ese servidor.

## Primer uso

1. Iniciá el servidor local y cargá un modelo.
2. Abrí **Configuración** en Tinta Viva.
3. Elegí el proveedor y completá la URL local. Para LM Studio normalmente es `http://127.0.0.1:1234/v1`.
4. Indicá el identificador del modelo o dejalo vacío para usar el primero informado por el servidor.
5. Presioná **Guardar** y después **Iniciar y comprobar**.

El iniciador `.bat` es opcional. Si querés arranque automático, guardá tu propio `run-model.bat` junto al ejecutable, dentro de `scripts`, o en `%LOCALAPPDATA%\PrivateCompanion`.

## Desarrollo

La solución se divide en:

- `PrivateCompanion.Domain`: entidades e invariantes.
- `PrivateCompanion.Application`: casos de uso, contratos y composición del contexto.
- `PrivateCompanion.Infrastructure`: SQLite, repositorios y proveedores locales.
- `PrivateCompanion.Desktop`: interfaz Avalonia/MVVM.
- `PrivateCompanion.Tests`: pruebas unitarias y de integración.

Compilar y probar desde PowerShell, dentro del repositorio:

```powershell
dotnet test .\PrivateCompanion.slnx
dotnet publish .\src\PrivateCompanion.Desktop\PrivateCompanion.Desktop.csproj -c Release -r win-x64 --self-contained true
```

## Licencia

El código se distribuye bajo la [licencia MIT](LICENSE). Los modelos, imágenes y textos importados conservan sus propias licencias y no se redistribuyen con la aplicación.
