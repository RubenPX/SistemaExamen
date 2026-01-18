# Infografía Técnica: Anatomía del Sistema "Untradex" - Una Arquitectura de 3 Fases para Ingesta Masiva

<img width="2752" height="1536" alt="image" src="https://github.com/user-attachments/assets/17839909-f7ba-485e-83b5-88ea5e443f00" />

## 1. El Desafío: Procesamiento Masivo en Tiempo Real

<img width="1376" height="768" alt="image" src="https://github.com/user-attachments/assets/aef23841-fa13-4f45-aaaf-683b6c4aba82" />

La gestión de flujos de datos de alta concurrencia es un desafío crítico en aplicaciones en tiempo real. El sistema "Untradex" fue diseñado para afrontar este reto en un entorno donde miles de usuarios interactúan simultáneamente, generando un volumen de eventos que supera la capacidad de escritura de una base de datos tradicional. La arquitectura del sistema es una respuesta directa a un conjunto de restricciones técnicas muy específicas.

El problema central del proyecto se define por dos limitaciones fundamentales:

* Carga de Usuarios: La necesidad de procesar de manera fiable las acciones de 4,000 alumnos operando de forma simultánea.
* Cuello de Botella: La elevada latencia de escritura de la base de datos Oracle, que incapacita por completo un flujo de datos directo y en tiempo real.

Una arquitectura directa, donde cada acción del alumno se escribe secuencialmente en la base de datos (Alumno -> Servidor -> Oracle), estaría destinada al fracaso. El diseño se optimizó para dominar el desafío de la eficiencia extrema de la CPU bajo la restricción impuesta de operar como si se tratara de un solo núcleo. Este enfoque forzó una arquitectura centrada en la asincronía total y el procesamiento por lotes para evitar el bloqueo de hilos y el colapso del sistema. Para superar este obstáculo, se diseñó una solución desacoplada y multifase.

## 2. La Solución: El Patrón "Buffer-Persistence"

<img width="1376" height="768" alt="image" src="https://github.com/user-attachments/assets/27e21eef-e10c-4503-a3bd-cd22ba65f299" />

La estrategia para resolver el desafío de la ingesta masiva fue la adopción de una arquitectura desacoplada. Este enfoque, denominado patrón "Buffer-Persistence", es un patrón de diseño pragmático que prioriza la disponibilidad del sistema sobre la consistencia inmediata del dato final, desacoplando la ingesta (rápida) de la persistencia (lenta). Al separar la recepción de datos de su almacenamiento final, se evita que un componente lento afecte el rendimiento global.

La solución se estructura en tres capas o fases fundamentales:

1. Capa de Absorción: Actúa como un búfer de alta velocidad para recibir todos los eventos de los alumnos sin demoras.
2. Capa de Persistencia Inmediata: Funciona como un almacén de datos intermedio, rápido y seguro, que desacopla la ingesta del almacenamiento final.
3. Capa de Almacenamiento Final: Es el repositorio de datos definitivo, optimizado para una carga masiva y diferida de información una vez que el pico de carga ha terminado.

El principio fundamental de esta arquitectura es "garantizar que ningún componente detenga al anterior". Este diseño previene de forma efectiva que un componente lento, como la base de datos Oracle, genere un efecto en cascada que colapse todo el sistema. A continuación, se detalla el funcionamiento técnico de cada una de estas fases.

## 3. Las Fases en Acción: Un Vistazo Técnico

Esta sección desglosa los componentes técnicos y los mecanismos que permiten el funcionamiento de cada una de las tres fases, detallando las tecnologías específicas utilizadas para alcanzar los objetivos de rendimiento y fiabilidad del sistema.

### 3.1. Fase 1: Absorción (El Escudo Frontal)

<img width="1376" height="768" alt="image" src="https://github.com/user-attachments/assets/286a5655-1580-4814-8da2-5dfb4fe0fb3f" />

Esta capa actúa como la primera línea del sistema, diseñada para crear una "concordancia de velocidad" (velocity match) entre las acciones instantáneas de los usuarios y las capas de persistencia más lentas. Su única responsabilidad, al ser agnóstica al disco, es aceptar los datos entrantes a la máxima velocidad posible para responder "sí" al cliente de forma inmediata, sin verse ralentizada por ninguna otra operación.

Componente	Especificación
Tecnología Principal	SignalR sobre .NET 8
Mecanismo de Búfer	System.Threading.Channels (Canal acotado de 100,000 eventos)
Optimización de Serialización	Uso de JsonSerializerContext (Source Generators) para eliminar Reflection y reducir la carga de CPU en un 40%
Protección de Memoria	Mecanismo de Backpressure para evitar desbordamiento de RAM
Modelo de Escalabilidad	Réplicas Horizontales gestionadas con .WithReplicas(n) en .NET Aspire

### 3.2. Fase 2: Persistencia Inmediata (El Búfer Seguro)

<img width="1376" height="768" alt="image" src="https://github.com/user-attachments/assets/d7c58387-0409-4d1f-b582-86d90c768580" />


El propósito de esta capa es actuar como un commit log intermedio y duradero para los datos que provienen de la capa de absorción. Persiste la información de inmediato en una base de datos de alta velocidad, garantizando que los datos sobrevivan a posibles fallos del sistema antes de llegar a su destino final.

Componente	Especificación
Tecnología Principal	Redis
Estructura de Datos	Redis List para la cola de eventos y Hash para el estado del alumno
Optimización Clave	Ingesta masiva con LPUSH (lotes de 100 eventos) para minimizar viajes de red
Garantía de Datos	Configuración de AOF (Append Only File) con fsync everysec

### 3.3. Fase 3: Almacenamiento Final (La Bóveda de Datos)

<img width="1376" height="768" alt="image" src="https://github.com/user-attachments/assets/407e2c0c-1ff4-46d6-8424-ae4a74ba39f0" />

Esta capa opera bajo el concepto de "Persistencia Diferida". Su principal desafío no es recibir datos en tiempo real, sino procesar de manera eficiente un volcado masivo de información desde Redis una vez que el evento de alta concurrencia ha concluido.

Para lograr un rendimiento óptimo durante la transferencia de datos de Redis a Oracle, se aplica una secuencia de optimizaciones clave:

1. Desactivación de Logs: Se ejecuta ALTER TABLE NOLOGGING para eliminar el overhead de escritura que genera el Redo Log de Oracle.
2. Eliminación de Índices: Se borran todos los índices de la tabla de destino con DROP INDEX antes de iniciar la carga de datos.
3. Carga Masiva: Se utiliza la utilidad OracleBulkCopy con un tamaño de lote (BatchSize) de 5,000 registros y la opción de Carga Directa (Direct Path Load) para maximizar la velocidad de inserción.
4. Reconstrucción Final: Una vez finalizada la exportación de todos los datos, se vuelven a crear los índices en la tabla.

Con la arquitectura de las tres fases definida, podemos ahora trazar el recorrido exacto de un único dato a través de este sistema, desde el clic del alumno hasta su almacenamiento final en la bóveda de datos.

## 4. El Viaje del Dato: Un Flujo Paso a Paso

<img width="1376" height="768" alt="image" src="https://github.com/user-attachments/assets/f0329792-e9fd-43be-a3cb-f4dfe30aa3d9" />

Para comprender mejor cómo interactúan las tres fases, esta sección describe el ciclo de vida completo de un dato, desde que un alumno realiza una acción hasta que esta queda registrada de forma segura en la base de datos final.

El flujo de trabajo sigue una secuencia clara y ordenada de eventos:

1. Envío del Alumno: El alumno envía una respuesta a través de SignalR.
2. Ingesta en Memoria: El servidor de absorción recibe el evento y lo coloca de inmediato en un Channel interno residente en RAM.
3. Persistencia en Redis: Un proceso worker en segundo plano extrae el evento del canal y lo envía a la cola correspondiente en Redis mediante el comando LPUSH.
4. Seguridad en Disco: Redis persiste el evento recibido en su archivo AOF (Append Only File), garantizando su durabilidad en el disco del servidor.
5. Inicio de la Exportación: Un administrador activa el proceso de volcado final de datos llamando al endpoint /api/finalizarexamen.
6. Volcado a Oracle: El servicio Exporter ejecuta el proceso de volcado masivo, transfiriendo los datos desde Redis hacia Oracle siguiendo el protocolo de carga de alto rendimiento.

Este flujo paso a paso demuestra cómo la arquitectura desacoplada permite que cada componente se enfoque en su tarea específica sin generar cuellos de botella.

## 5. Fortalezas Clave de la Arquitectura

<img width="1376" height="768" alt="image" src="https://github.com/user-attachments/assets/42d814a9-b8a4-44c6-85b4-ddb34d818729" />

La arquitectura resultante va más allá de la simple resolución de un cuello de botella; establece un nuevo estándar de operatividad para la plataforma, fundamentado en tres pilares estratégicos: resiliencia, eficiencia y escalabilidad.

Las principales fortalezas de esta arquitectura son:

* Resiliencia: El sistema es tolerante a fallos; puede continuar operando y recibiendo datos de los alumnos incluso si la conexión con la base de datos Oracle se pierde temporalmente. La persistencia intermedia en Redis asegura que no se pierda ninguna información.
* Eficiencia: El diseño está optimizado para un uso extremo de la CPU, logrando un alto rendimiento a través de asincronía total y procesamiento por lotes (batching). Cada recurso se utiliza de la manera más eficaz posible.
* Escalabilidad: La arquitectura está preparada para crecer. Se pueden añadir más servidores de absorción de forma horizontal sin necesidad de modificar la lógica de negocio, gracias al uso de Redis como un backplane de comunicación y datos compartido.


