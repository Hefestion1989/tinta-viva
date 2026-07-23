# Arquitectura

## Decisión

Private Companion es un monolito modular. No utiliza un backend web interno ni microservicios.

La UI depende de los casos de uso; la aplicación depende del dominio; infraestructura implementa los puertos de persistencia e inferencia. El composition root reside exclusivamente en `App.axaml.cs`.

## Dependencias

```text
Desktop -> Application -> Domain
Desktop -> Infrastructure -> Application + Domain
```

El modelo de transporte OpenAI no atraviesa el límite de infraestructura. `TextGenerationRequest`, `ChatEntry` y `ProviderProbe` son contratos propios.

## Persistencia

SQLite conserva entidades e historial. Los medios se guardan fuera de la base con rutas relativas, huella SHA-256 y metadatos. `IMediaLibrary` abstrae el origen: hoy importa archivos locales y luego podrá recibir resultados de ComfyUI sin acoplarlos a la UI. WAL, claves foráneas y busy timeout se activan al inicializar.

Los perfiles de personaje crean un `CharacterRevision` por guardado. Los mensajes son la fuente de verdad; memorias y diarios son datos derivados y corregibles.

`UserPersona` es una identidad local. Conversaciones, memorias, diarios y estado relacional se particionan por el par `(CharacterId, UserPersonaId)`. Esto evita que cambiar de identidad mezcle hechos o intimidad de vínculos diferentes. El esquema v7 se actualiza de forma idempotente y genera un backup SQLite consistente antes de modificar una base existente.

`CharacterMediaPresentation` referencia un retrato y un fondo sin alterar `MediaAsset`. También conserva zoom y foco normalizados por cada uso visual. La imagen original sigue siendo independiente, puede reutilizarse y nunca se recorta físicamente.

## Memoria

La recuperación actual combina fijado, importancia, coincidencia léxica y recencia. `PromptComposer.SelectMemories` es determinista y reemplazable. La futura recuperación semántica debe entrar por un puerto separado y registrar modelo y dimensión del embedding.

## Proveedores

`OpenAiCompatibleTextGateway` soporta la intersección estable de Chat Completions y normaliza SSE. Los perfiles locales quedan restringidos a `localhost` o `127.0.0.1`; los remotos requieren HTTPS y no siguen redirecciones. Los tokens remotos se guardan mediante el Administrador de credenciales de Windows, separados de SQLite. La administración del proceso local permanece separada de la composición del prompt.

## Evolución

Próximas fronteras previstas:

1. migraciones versionadas de esquema;
2. vault cifrado para base y medios;
3. embeddings locales y reindexación;
4. trabajos ComfyUI con Visual Identity Packs;
5. puertos STT/TTS;
6. backups cifrados y exportación.
