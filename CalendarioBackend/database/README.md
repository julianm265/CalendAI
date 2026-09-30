# Base de datos PostgreSQL

`001-schema.sql` se ejecuta automaticamente por la imagen oficial de PostgreSQL cuando el volumen se crea por primera vez.

## Levantar la base de datos

Desde la raiz `calendAI`:

```powershell
docker compose up -d postgres
```

## Entrar a PostgreSQL

```powershell
docker exec -it calendai-postgres psql -U calendai_user -d calendai
```

Comandos utiles dentro de `psql`:

```sql
\dt
\d equipos
SELECT * FROM equipos;
\q
```

## Relaciones principales

- `equipos` es la tabla principal.
- `calendarios.equipo_id` es unico: un equipo tiene un calendario.
- `colaboradores.equipo_id` permite varios colaboradores por equipo.
- `eventos.equipo_id` permite varios eventos por equipo.
- `eventos.colaborador_organizador_id` puede ser nulo.
- `UNIQUE (equipo_id, fecha, hora)` impide dos eventos simultaneos del mismo equipo.

El volumen `calendai_postgres_data` conserva los datos. Para borrar todo y volver a ejecutar el SQL inicial:

```powershell
docker compose down -v
docker compose up -d postgres
```

Usa `down -v` solo cuando quieras eliminar los datos locales.
