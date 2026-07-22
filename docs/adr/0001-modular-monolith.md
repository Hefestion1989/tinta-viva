# ADR-0001: Monolito modular

Estado: aceptado.

## Contexto

La aplicación es local, de un solo usuario y debe poder cambiar proveedores sin multiplicar procesos o despliegues.

## Decisión

Usar un proceso de escritorio con proyectos separados para dominio, casos de uso, infraestructura y UI. Las dependencias externas se conectan mediante interfaces propias.

## Consecuencias

Se obtienen reemplazabilidad y pruebas aisladas sin el costo operativo de microservicios. Un cargador dinámico de plugins solo se agregará cuando exista una necesidad comprobada.
