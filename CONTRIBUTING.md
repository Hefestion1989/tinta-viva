# Contribuir a Tinta Viva

Gracias por tu interés. Antes de proponer cambios:

1. Creá una rama desde `main`.
2. Mantené separadas la interfaz, la lógica de aplicación, el dominio y la infraestructura.
3. No agregues bases, conversaciones, imágenes importadas, modelos, secretos ni rutas personales.
4. Ejecutá `dotnet test .\PrivateCompanion.slnx`.
5. Explicá en el pull request qué cambia y cómo fue validado.

Los adaptadores nuevos deben conservar la persistencia canónica dentro de Tinta Viva y tratar al motor como un servicio reemplazable.
