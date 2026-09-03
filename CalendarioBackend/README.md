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
- `GET /api/equipos/{id}/eventos?fecha=2026-09-03`
- `POST /api/equipos/{id}/eventos`
- `DELETE /api/equipos/{id}/eventos/{eventoId}?fecha=2026-09-03`

## Ejecutar todo con Docker Compose

Desde la carpeta raíz `calendAI`:

```bash
docker compose up --build
```

Después abre `http://localhost:5500`. La Web API queda disponible en
`http://localhost:5080` y el frontend se conecta automáticamente a ella.
Para detener los servicios:

```bash
docker compose down
```

La aplicación usa actualmente almacenamiento en memoria; los equipos y eventos
se pierden al detener o recrear el contenedor del backend.

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
