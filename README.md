# Untradex - Concepto EDA por fases
[Documentación provisional con IA (Notebook LLM)](./Readme%20with%20IA.md)

El proyecto "Untradex" es un sistema diseñado para gestionar grandes volúmenes de solicitudes y acciones en tiempo real en entornos de elevada concurrencia, específicamente en un sistema de evaluación para alumnos.

El proyecto está optimizado para resolver problemas derivados de la latencia elevada en bases de datos tradicionales y las limitaciones derivadas del procesamiento masivo en tiempo real.

## Desafío principal

El sistema ha sido concebido para procesar de manera confiable las acciones de hasta 4,000 alumnos interactuando simultáneamente. El desafío principal identificado en el diseño es la limitación de la escritura en bases de datos Oracle. Estas bases de datos tienen una latencia elevada que provoca un cuello de botella al manejar grandes flujos de eventos en tiempo real. Para abordar estas limitaciones:

1. **Objetivo principal:** Procesar de manera eficiente eventos masivos generados por alumnos, evitando sobrecargar los recursos del sistema.
2. **Restricciones técnicas:** La arquitectura está diseñada para cumplir con niveles muy específicos de alta concurrencia y minimizar el impacto del uso intensivo de bases de datos.

## Arquitectura: 3 Fases para Ingesta Masiva de Datos

El proyecto basa su arquitectura en una estructura dividida en 3 fases independientes, cada una diseñada para gestionar un paso específico en el flujo de datos.

### Fase 1: Absorción (El Escudo Frontal)
- **Tecnología principal:** .NET 10 y SignalR para la comunicación en tiempo real con el cliente (alumnos).
- **Gestión de eventos:** Usa `System.Threading.Channels` con un canal acotado a 100,000 eventos.
- **Optimización del rendimiento:**
  - Uso de `JsonSerializerContext` con *Source Generators* para reducir la carga de CPU en un 40%.
  - Funcionalidad de *backpressure* para proteger la memoria contra el desbordamiento.
- **Escalabilidad:** Implementación de réplicas horizontales con `.WithReplicas(n)` en .NET Aspire.

### Fase 2: Persistencia Temporal en Redis
Una vez que los eventos son gestionados por la Fase 1:
1. **Ingestión en Redis a través de un worker en segundo plano (RedisIngestionWorker).**
2. Los datos se encolan y se escriben en un archivo de solo adición (AOF) para garantizar la persistencia en disco.

> [!note]
> En caso de que haya un apagon, solo se ha perdido el ultimo segundo antes del apagon

### Fase 3: Volcado Final a la Base de Datos Oracle
- **Servicio Exporter:** Un administrador activa el proceso de transferencia final llamando al endpoint `/api/finalizarexamen`. Este paso involucra:
  - La transferencia de datos desde Redis hacia Oracle en un formato de carga de alto rendimiento.
  - Un servicio denominado `OracleExporterService`, que inicializa la conexión con Oracle y gestiona las tablas necesarias para almacenar respuestas de los alumnos.

## Flujo General del Dato

1. **Acción del alumno:** Los alumnos envían respuestas usando SignalR.
2. **Inserción en memoria:** Un servidor de primeras interacciones recibe los eventos y los almacena en un canal residente en RAM.
3. **Persistencia en Redis:** Un worker transfiere los eventos al sistema de cola en Redis y persiste los datos en el archivo AOF.
4. **Exportación masiva a Oracle:** Las respuestas definitivas se vuelcan en una base de datos Oracle para su almacenamiento seguro y más permanente.

Este sistema divide las tareas en pasos bien definidos, garantizando que cada componente cumpla con su función y previniendo cuellos de botella.

## Principales Componentes de Código

1. **Monitor de Canales:**
   - Archivo: [`Examenes.Server/BackgroundServices/ChannelMonitor.cs`](./Examenes.Server/Monitoring/ChannelMonitorWorker.cs)
   - Propósito: Monitorear el uso del canal de eventos en memoria y prevenir sobrecargas utilizando Backpressure.

2. **Servicio OracleExporter:**
   - Archivo: [`OracleExporterService.cs`](./Examenes.Server/Exporters/OracleExporterService.cs)
   - Función:
     - Conectar a la base de datos Oracle con múltiples intentos automáticos.
     - Crear la tabla requerida en Oracle si no está configurada.
     - Transmitir masivamente datos usando protocolos de alta eficiencia.

3. **RedisIngestion Worker:**
   - Archivo: [`RedisIngestionWorker.cs`](./Examenes.Server/BackgroundServices/RedisIngestionWorker.cs)
   - Función: Mover los datos del canal en memoria a Redis para su persistencia temporal.

4. **Endpoint de Exportación:**
   - Archivo: [`Program.cs`](./Examenes.Server/Program.cs)
   - Detalle: Un endpoint REST permite iniciar el volcado de datos desde Redis a Oracle.

## Tecnologías Usadas

1. **SignalR:** Gestiona la comunicación en tiempo real entre los alumnos y el sistema.
2. **.NET 8:** El framework base del sistema, junto con .NET Aspire para capacidades adicionales como escalabilidad, resiliencia y mínimo consumo de recursos.
3. **Redis:** Base de datos NoSQL para ingesta rápida y con persistencia en AOF.
4. **Oracle:** Base de datos transaccional para almacenamiento final y consultas.
5. **OpenTelemetry:** Monitorización centralizada, métricas y diágnosticos para la arquitectura.

En general, "SistemaExamen" representa un esfuerzo de ingeniería para abordar desafíos específicos en la manejo masivo de datos, con una combinación de tecnologías modernas y patrones arquitectónicos probados. Su diseño modular basado en las tres fases lo hace eficiente y escalable, cumpliendo con las estrictas demandas de baja latencia, monitoreo y gestión de datos en entornos educativos.

> [!warning]
> Debido a limitaciones que desconozco, solo he podido realizar pruebas con un maximo de 4000 conexiones a la vez


