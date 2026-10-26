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

- `equipos`: calendarios. `es_personal = true` es el calendario personal de un usuario; `creado_por` es quien lo creó.
- `colaboradores`: usuarios (`contrasena_hash` con PBKDF2 y sal).
- `membresias`: quién pertenece a qué equipo y con qué rol (`lider` o `miembro`). Un usuario puede estar en muchos equipos; hay exactamente un líder por equipo (índice único parcial).
- `invitaciones`: invitaciones del líder a otros usuarios (`pendiente`, `aceptada`, `rechazada`, `cancelada`).
- `calendarios` y `eventos`: cada equipo tiene un calendario; no puede haber dos eventos a la misma fecha y hora en el mismo equipo.

Al eliminar un equipo se eliminan en cascada sus membresías, invitaciones y eventos (nunca los usuarios).
