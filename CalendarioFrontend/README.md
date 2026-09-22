# CalendarioFrontend

Frontend en HTML/CSS/JavaScript vanilla (sin frameworks) que consume la Web API
de `CalendarioBackend.Api` por HTTP/JSON. No depende de la DLL de
`CalendarioBackend.Core` ni la modifica: todo pasa por `fetch`.

## Estructura

```
CalendarioFrontend/
├── index.html      Esqueleto de las 3 pantallas (login, equipos, calendario) y los paneles laterales
├── styles.css       Diseño completo (tokens, layout, responsive)
├── api.js           Capa de acceso a datos (fetch puro, sin DOM)
├── normalize.js      Adapta la forma del JSON de respuesta a un formato interno estable
├── state.js         Sesión y equipo activo, guardados solo en sessionStorage
├── calendar.js      Cálculo puro de la cuadrícula mensual (sin DOM)
├── ui.js            Helpers de DOM: toasts, drawers, confirmación, estados de carga/error
└── app.js           Controlador principal: conecta todo y maneja los eventos
```

## Cómo iniciar el frontend

Es un sitio 100% estático: no necesita `npm install` ni bundlers. Pero **no lo
abras con doble clic** (`file://`), porque `app.js` usa módulos ES
(`type="module"`) y los navegadores bloquean `import` bajo el protocolo
`file://`. Sírvelo con cualquier servidor estático simple, por ejemplo:

```bash
cd CalendarioFrontend

# Opción A: Python (ya viene instalado en la mayoría de sistemas)
python3 -m http.server 5500

# Opción B: Node (si tienes el paquete "serve" o "http-server")
npx serve -l 5500 .

# Opción C: extensión "Live Server" de VS Code
```

Luego abre `http://localhost:5500` en el navegador.

También puedes levantar frontend y backend juntos desde la raíz `calendAI` con
`docker compose up --build`; en ese caso el frontend queda en
`http://localhost:5500` y la API en `http://localhost:5080`.

### Editar el frontend con Docker

El servicio `frontend` monta `./CalendarioFrontend` como volumen de solo
lectura dentro de Nginx. Por eso, durante el desarrollo, puedes editar
directamente estos archivos en el host y refrescar el navegador:

- `index.html`: estructura de login, equipos y la vista independiente del
  calendario.
- `styles.css`: tokens de color, tipografía, layout y responsive.
- `app.js`, `calendar.js`, `state.js` y `ui.js`: comportamiento.

No es necesario ejecutar `docker compose build` después de esos cambios. Solo
hay que reconstruir si cambian el `Dockerfile` o `nginx.conf`:

```bash
docker compose up -d
docker compose build frontend
docker compose up -d frontend
```

Después de iniciar sesión, la aplicación navega a
`http://localhost:5500/calendar.html`. Esta es una página HTML independiente:
no comparte el documento ni la composición visual del login. Si se abre
directamente sin una sesión y calendario guardados, vuelve a
`http://localhost:5500/index.html`.

Durante desarrollo, Nginx sirve los HTML con `Cache-Control: no-store`, por lo
que los cambios de separación se ven al refrescar sin conservar la versión
anterior en el navegador. La URL antigua `/#/calendar` también se redirige a
`/calendar.html` cuando existe una sesión válida.

En el despliegue con Docker, `api.js` usa la ruta relativa `/api`: Nginx la
redirige internamente al contenedor backend y no hay un problema de CORS. Si
ejecutas el frontend manualmente con Python o Live Server, cambia la constante
al inicio de `api.js` a la URL pública de la API:

```js
export const API_BASE_URL = 'http://localhost:5080/api';
```

## Cómo configurar CORS en la API (ASP.NET Core)

El frontend corre en un origen distinto al de la API (por ejemplo
`http://localhost:5500` vs `http://localhost:5080`), así que el navegador
bloqueará las peticiones si la API no habilita CORS explícitamente. En
`CalendarioBackend.Api/Program.cs`, agrega (sin tocar `CalendarioBackend.Core`):

```csharp
var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "FrontendDev";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5500") // origen exacto del frontend
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ... resto de la configuración (controllers, etc.) ...

var app = builder.Build();

app.UseCors(CorsPolicy); // antes de MapControllers() / los endpoints

// ... app.MapControllers(); etc.
app.Run();
```

Ajusta el origen si sirves el frontend desde otro puerto o herramienta (por
ejemplo `http://127.0.0.1:5500` o el puerto que use tu Live Server).

## Ejecutar la API y el frontend simultáneamente

En dos terminales:

```bash
# Terminal 1: la API
cd CalendarioBackend
dotnet run --project src/CalendarioBackend.Api
# queda escuchando en, por ejemplo, http://localhost:5080

# Terminal 2: el frontend
cd CalendarioFrontend
python3 -m http.server 5500
# ábrelo en http://localhost:5500
```

Si el frontend no puede conectarse, la interfaz lo dice explícitamente
("No se pudo conectar con la API en http://localhost:5080/api. Verifica que
esté corriendo y que CORS esté habilitado.") en vez de fallar en silencio o
mostrar datos simulados.

## Flujo de la aplicación

1. **Inicio de sesión y registro** (`POST /api/autenticacion/login` y
  `POST /api/autenticacion/registro`). Cada cuenta recibe automáticamente un
  calendario personal al iniciar sesión por primera vez, aunque todavía no
  pertenezca a un equipo colaborativo. El calendario personal se abre por
  defecto en `#/calendar`.
2. **Equipos**: listar (`GET /api/equipos`), crear (`POST /api/equipos`) y
   elegir el equipo activo. Estos calendarios son colaborativos; el personal y
   los colaborativos usan la misma vista y las mismas operaciones de eventos.
   El encabezado indica si el calendario actual es personal o colaborativo.
3. **Calendario mensual**: navegación entre meses, cuadrícula con indicador de
   cuántos eventos tiene cada día (usando `GET /eventos/mes`), panel lateral
   con los eventos del día seleccionado (usando `GET /eventos?fecha=`),
   creación (`POST /eventos`) y eliminación (`DELETE /eventos/{id}`) de
   eventos con confirmación previa.
4. **Colaboradores**: panel lateral accesible desde la barra superior para ver
   y agregar colaboradores del equipo activo (`POST /colaboradores`).

La sesión (`colaborador`) y el equipo activo se guardan en
`sessionStorage` para sobrevivir recargas de la página, **pero no son un
mecanismo de seguridad real**: el backend actual no protege ninguna ruta con
JWT ni cookies, así que cualquier persona con acceso a la URL de la API puede
leer o modificar los datos directamente. La interfaz lo aclara en la pantalla
de inicio de sesión.

## Supuestos sobre la forma de las respuestas JSON

La consigna especificó los endpoints y el cuerpo de las peticiones `POST`,
pero no la forma exacta del JSON que devuelve cada `GET`/`POST`. Para no
romper la app si el nombre de un campo no coincide exactamente,
`normalize.js` acepta variantes camelCase/PascalCase y documenta ahí mismo
qué se asumió:

- `GET /api/equipos` y `GET /api/equipos/{id}` → un equipo con
  `{ id, nombreEquipo, colaboradores: [{ id, usuario }] }`.
- `POST /api/autenticacion/login` → el colaborador autenticado, ya sea como
  `{ id, usuario }` directamente o envuelto en `{ colaborador: { ... } }`.
- `GET /api/equipos/{id}/eventos?fecha=` → un arreglo de eventos
  `{ id, nombreEvento, hora, lugar, descripcion, colaboradorOrganizadorId }`.
- `GET /api/equipos/{id}/eventos/mes?año=&mes=` → un arreglo donde cada
  elemento trae la fecha y el evento, aceptando tanto
  `{ fecha, evento: {...} }` (anidado) como `{ fecha, nombreEvento, ... }`
  (aplanado) — así sea que el backend serialice la tupla `(Fecha, Evento)` de
  una forma u otra.

Si tu API usa nombres de campo distintos a los de arriba, el único archivo que
hay que tocar es `normalize.js`.

## Accesibilidad y estados cubiertos

- Labels asociados a cada input, navegación por teclado (incluye `Escape`
  para cerrar drawers/diálogos y devolución de foco al elemento que los abrió),
  contraste AA en texto y botones, y foco visible (`:focus-visible`).
- Estados de carga (`Cargando…`), error (mensajes según código HTTP
  400/401/404/409 o "sin conexión"), vacío ("No hay eventos este día…") y
  validación de formularios (mensajes inline por campo, sin usar `alert()`).
- Confirmación explícita antes de eliminar un evento.
- La contraseña nunca se muestra de vuelta en la interfaz: los formularios se
  limpian (`form.reset()`) inmediatamente después de enviarse.
