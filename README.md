# Tinta Viva

Aplicación de escritorio privada y de código abierto para Windows, orientada a universos narrativos, personajes persistentes, conversación local y memoria con continuidad. El nombre técnico interno `PrivateCompanion` se conserva para mantener la compatibilidad con las rutas y bases existentes.

Tinta Viva separa la experiencia narrativa del modelo: personajes, escenas, recuerdos, relaciones y conversaciones pertenecen a la aplicación, mientras que la generación se delega a un servidor local compatible con la API de OpenAI.

## Estado actual

La versión 0.5.2 incluye:

- interfaz Avalonia/.NET 10 para Windows;
- personajes editables y perfiles versionados;
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
- encuadre no destructivo con zoom y foco independientes para retrato y fondo;
- botón de cierre visible en la esquina superior derecha;
- diseño más compacto que respeta el área útil de Windows, recorta cada panel y reserva el espacio del botón de cierre;
- múltiples identidades de usuario con conversaciones, recuerdos, diario y relación aislados;
- español rioplatense natural como valor predeterminado, sin forzar lunfardo ni muletillas;
- actualización versionada del esquema con backup automático previo;
- validación de edad mínima.

La importación manual ya usa el puerto común de biblioteca visual. ComfyUI y voz tienen límites de módulo definidos, pero la generación visual con identidad y el audio pertenecen a fases posteriores. No se simula consistencia de identidad con un prompt suelto.

## Datos locales

Los datos de uso se almacenan en:

`%LOCALAPPDATA%\PrivateCompanion`

Las imágenes importadas se copian en `media\originals`; nunca se mueven ni se modifican los archivos elegidos por el usuario.

Antes de actualizar una base anterior se crea una copia consistente en `backups`. La migración v0.5 vincula los personajes existentes a un universo principal sin borrar conversaciones ni recuerdos.

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

## Alcance narrativo

Tinta Viva está orientada a universos, personajes, vínculos, psicología y continuidad general. También admite romance, sexualidad y contenido erótico explícito adulto cuando resulte coherente con los personajes y la escena, sin advertencias repetitivas ni moralizaciones. Tinta Privada continúa siendo la superficie especializada para escribir relatos y roleplay erótico. La edad mínima continúa validándose al guardar los perfiles.

## Licencia

El código se distribuye bajo la [licencia MIT](LICENSE). Los modelos, imágenes y textos importados conservan sus propias licencias y no se redistribuyen con la aplicación.
