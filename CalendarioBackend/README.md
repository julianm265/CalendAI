# CalendarioBackend

Backend en C# (.NET 8) para un calendario funcional, construido siguiendo exactamente
la distribución de clases del diagrama `Calendai.dia`.

## Cómo ejecutarlo

```bash
cd CalendarioBackend
dotnet run --project src/CalendarioBackend.Console
```

Requiere el SDK de .NET 8 (`dotnet --version` debe reportar 8.x).

## Ejecutar la Web API

```bash
dotnet run --project src/CalendarioBackend.Api
```

La API expone, entre otras, estas rutas:

- `GET /api/equipos`
- `POST /api/equipos`
- `GET /api/equipos/{id}`
- `POST /api/equipos/{id}/colaboradores`
- `POST /api/autenticacion/login`
- `POST /api/autenticacion/registro`
- `GET /api/equipos/{id}/eventos?fecha=2026-09-03`
- `POST /api/equipos/{id}/eventos`
- `DELETE /api/equipos/{id}/eventos/{eventoId}?fecha=2026-09-03`
- `GET /api/lugares/buscar?query=Auditorio%20Central`
- `GET /api/equipos/{id}/lugares/preferidos?limite=8`

## Ejecutar todo con Docker Compose

Desde la carpeta raíz `calendAI`, creá un archivo `.env` a partir de `.env.example` con la
contraseña de PostgreSQL (el archivo `.env` no se versiona):

```bash
cp .env.example .env
docker compose up --build
```

Después abre `http://localhost:5500`. La Web API queda disponible en
`http://localhost:5080` y el frontend se conecta automáticamente a ella.
Para detener los servicios:

```bash
docker compose down
```

La aplicación usa PostgreSQL mediante Entity Framework Core. Docker Compose crea el servicio
`postgres` y conserva sus datos en el volumen `calendai_postgres_data`; las tablas se crean
automáticamente al iniciar la API. Las cuentas, equipos y eventos sobreviven a reinicios del
backend y el login consulta siempre el mismo repositorio persistente de colaboradores.

La cadena de conexión no se guarda en el repositorio: la API la lee de
`ConnectionStrings__Calendai`, que Docker Compose arma con las variables de `.env`. Para
ejecutar la API fuera de Docker definí esa variable de entorno o usa los secretos de usuario
(`dotnet user-secrets set ConnectionStrings:Calendai "..."`). `docker compose down` conserva la
base; no uses `docker compose down -v` si necesitas conservar los datos.

### Búsqueda de lugares

La búsqueda del lugar del evento usa Google Places desde el backend. Define una clave con **Places
API (New)** habilitada antes de ejecutar la aplicación. No es suficiente habilitar únicamente la
API legacy de Places:

```bash
dotnet user-secrets set GooglePlaces:ApiKey "TU_CLAVE_DE_GOOGLE"
```

Con Docker Compose, agrega `GOOGLE_PLACES_API_KEY=TU_CLAVE_DE_GOOGLE` al archivo `.env`. La clave
no se envía al frontend. Los lugares preferidos se calculan con los lugares de eventos guardados,
agrupando diferencias de mayúsculas, espacios y acentos.

Los usuarios que solo existían en la implementación anterior en memoria no pueden recuperarse
después de haber detenido el contenedor, porque no había un origen persistente que migrar. A
partir de esta implementación, los nuevos registros y colaboradores de equipos se guardan en
PostgreSQL y comparten una restricción única por usuario.

## Estructura del proyecto

```
CalendarioBackend/
├── CalendarioBackend.sln
└── src/
    ├── CalendarioBackend.Core/           # Librería de clases (el "backend" propiamente dicho)
    │   ├── Models/                       # Clases del diagrama UML
    │   │   ├── Evento.cs
    │   │   ├── Dia.cs
    │   │   ├── Semana.cs
    │   │   ├── Mes.cs
    │   │   ├── Anio.cs
    │   │   ├── Calendario.cs
    │   │   ├── Colaborador.cs
    │   │   └── Equipo.cs
    │   ├── Repositories/                 # Persistencia (interfaz + implementación en memoria)
    │   │   ├── IEquipoRepository.cs
    │   │   └── InMemoryEquipoRepository.cs
    │   └── Services/                     # Lógica de negocio / casos de uso
    │       ├── CalendarioService.cs
    │       ├── EquipoService.cs
    │       └── AutenticacionService.cs
    └── CalendarioBackend.Console/        # Programa de consola que demuestra el flujo completo
        └── Program.cs
    └── CalendarioBackend.Api/            # ASP.NET Core Web API para clientes frontend
      ├── Controllers/
      └── Program.cs
```

## Mapeo diagrama → código

| Clase del diagrama | Atributos del diagrama                        | Clase C#       | Notas |
|---------------------|------------------------------------------------|----------------|-------|
| Dia                 | Fecha, L_eventos                                | `Dia`          | `Fecha` es `DateOnly`. `LEventos` es de solo lectura hacia afuera; se modifica con `AgregarEvento`/`EliminarEvento`. |
| Evento              | Nombre_evento, Hora_Evento, Lugar_Evento        | `Evento`       | Se añadieron `Descripcion` y `ColaboradorOrganizadorId` (opcionales) por ser prácticos en un backend real, sin romper el modelo original. |
| Semana              | Fecha_inicio, Fecha_final, L_dias               | `Semana`       | Se genera automáticamente con semanas reales (lunes a domingo, recortadas en los bordes del mes). |
| Mes                 | Num_Mes, L_Semana, Treinta_y_uno                | `Mes`          | `TreintaYUno` se calcula con `DateTime.DaysInMonth`, no se almacena "a mano". |
| Año                 | Num_año, Es_bisiesto, l_meses                   | `Anio`         | Al crearse genera sus 12 meses automáticamente. `EsBisiesto` usa `DateTime.IsLeapYear`. |
| Calendario          | l_años                                          | `Calendario`   | Los años se crean bajo demanda (`ObtenerOCrearAño`) para no generar estructuras completas sin necesidad. |
| Colaborador         | Usuario, Contraseña                             | `Colaborador`  | La contraseña **nunca** se guarda en texto plano: se almacena su hash SHA-256. |
| Equipo              | Nombre_equipo, l_colaboradores                  | `Equipo`       | La asociación Equipo–Calendario del diagrama (sin multiplicidad) se interpretó como 1 a 1: cada equipo tiene su propio calendario, creado automáticamente. |

### Jerarquía de composición

```
Calendario → Año[] → Mes[] → Semana[] → Dia[] → Evento[]
```

Esto respeta las asociaciones "Almacena" (Dia–Evento) y las multiplicidades del diagrama
(`1..n`, `1..12`, etc.), pero se implementó con **generación automática real de fechas**
en lugar de listas vacías que el usuario tendría que llenar a mano: al pedir el año 2026,
el backend arma sus 12 meses, cada mes sus semanas reales y cada semana sus días reales,
usando el calendario gregoriano de .NET.

### Decisiones de diseño (como lo haría un senior)

1. **Separación en capas**: `Models` (entidades del diagrama) → `Repositories` (persistencia,
   hoy en memoria, fácilmente sustituible por EF Core/SQL/Mongo) → `Services` (casos de uso:
   crear equipo, registrar colaborador, autenticar, agregar evento, consultar agenda).
2. **Encapsulamiento**: las listas (`L_eventos`, `L_dias`, `L_Semana`, `l_meses`,
   `l_colaboradores`, `l_años`) se exponen como `IReadOnlyList<T>` y solo se modifican
   mediante métodos con validaciones (evitar eventos duplicados a la misma hora, evitar
   colaboradores duplicados, etc.).
3. **Seguridad mínima viable**: contraseñas hasheadas (SHA-256) y nunca expuestas como
   propiedad pública.
4. **Extensible**: `IEquipoRepository` permite conectar una base de datos real sin tocar
   `Services` ni `Models`. Se puede añadir un `CalendarioBackend.Api` (ASP.NET Core Web API)
   como una tercera capa que simplemente llame a estos servicios, sin cambiar nada de lo ya escrito.
5. **Sin dependencias externas**: solo usa el BCL de .NET (`System.Security.Cryptography`,
   `System.Globalization`, etc.), para que el proyecto compile sin paquetes NuGet adicionales.

## Próximos pasos sugeridos

- Agregar un proyecto `CalendarioBackend.Api` (ASP.NET Core Web API) que exponga
  `CalendarioService`, `EquipoService` y `AutenticacionService` vía controladores/minimal APIs.
- Agregar un proyecto de pruebas (`CalendarioBackend.Tests` con xUnit) para las reglas de
  negocio (año bisiesto, mes de 31 días, colisión de eventos a la misma hora, login inválido, etc.).
- Sustituir `InMemoryEquipoRepository` por una implementación con Entity Framework Core
  y una base de datos relacional.


## Importación de eventos desde documentos

`POST /api/equipos/{id}/documentos/fechas?formatoFecha=auto|dmy|mdy` (PDF o .docx) devuelve, por cada evento detectado: fecha (y fecha fin si es un rango), nombre, lugar, hora, texto original, confianza y, si la fecha es ambigua, la lectura alternativa.

- Formatos de fecha: `25/03/2026`, `25-03-26`, `2026-03-25`, `25 de marzo de 2026`, `25-mar-26`, `March 25th, 2026`, `del 5 al 7 de octubre`, etc.
- El orden día/mes se deduce del documento (una fecha como 25/03 lo delata); si no hay evidencia se asume día/mes y se marca la fecha como ambigua. El usuario puede forzar el formato.
- Un día de la semana junto a la fecha (`lunes 06/04/2026`) la desambigua; las fechas imposibles (31/04) se descartan.
- La lógica vive en `CalendarioBackend.Core/Documents` y se prueba en `DocumentEventAnalyzerTests`.
