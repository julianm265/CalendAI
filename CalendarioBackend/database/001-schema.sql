-- Esquema completo de CalendAI. Es idempotente: la API aplica exactamente este mismo SQL al iniciar.

CREATE TABLE IF NOT EXISTS equipos (
    id uuid PRIMARY KEY,
    nombre varchar(120) NOT NULL,
    es_personal boolean NOT NULL DEFAULT false,
    creado_por uuid NULL,
    creado_en timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS calendarios (
    id uuid PRIMARY KEY,
    equipo_id uuid NOT NULL UNIQUE REFERENCES equipos(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS colaboradores (
    id uuid PRIMARY KEY,
    usuario varchar(100) NOT NULL,
    contrasena_hash varchar(255) NOT NULL
);

CREATE TABLE IF NOT EXISTS membresias (
    equipo_id uuid NOT NULL REFERENCES equipos(id) ON DELETE CASCADE,
    colaborador_id uuid NOT NULL REFERENCES colaboradores(id) ON DELETE CASCADE,
    rol varchar(20) NOT NULL DEFAULT 'miembro' CHECK (rol IN ('lider', 'miembro')),
    unido_en timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (equipo_id, colaborador_id)
);

CREATE TABLE IF NOT EXISTS invitaciones (
    id uuid PRIMARY KEY,
    equipo_id uuid NOT NULL REFERENCES equipos(id) ON DELETE CASCADE,
    invitado_id uuid NOT NULL REFERENCES colaboradores(id) ON DELETE CASCADE,
    invitado_por_id uuid NOT NULL REFERENCES colaboradores(id) ON DELETE CASCADE,
    estado varchar(20) NOT NULL DEFAULT 'pendiente'
        CHECK (estado IN ('pendiente', 'aceptada', 'rechazada', 'cancelada')),
    creada_en timestamptz NOT NULL DEFAULT now(),
    respondida_en timestamptz NULL
);

CREATE TABLE IF NOT EXISTS eventos (
    id uuid PRIMARY KEY,
    equipo_id uuid NOT NULL REFERENCES equipos(id) ON DELETE CASCADE,
    fecha date NOT NULL,
    hora time NOT NULL,
    nombre varchar(160) NOT NULL,
    lugar varchar(200) NULL,
    descripcion varchar(500) NULL,
    colaborador_organizador_id uuid NULL REFERENCES colaboradores(id) ON DELETE SET NULL,
    CONSTRAINT uq_evento_equipo_fecha_hora UNIQUE (equipo_id, fecha, hora)
);

ALTER TABLE equipos ADD COLUMN IF NOT EXISTS es_personal boolean NOT NULL DEFAULT false;

ALTER TABLE equipos ADD COLUMN IF NOT EXISTS creado_por uuid NULL;

ALTER TABLE equipos ADD COLUMN IF NOT EXISTS creado_en timestamptz NOT NULL DEFAULT now();

DO $$
BEGIN
    -- Versión anterior: cada colaborador pertenecía a un solo equipo (colaboradores.equipo_id).
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = current_schema() AND table_name = 'colaboradores' AND column_name = 'equipo_id') THEN
        INSERT INTO membresias (equipo_id, colaborador_id, rol)
        SELECT c.equipo_id, c.id, 'miembro' FROM colaboradores c WHERE c.equipo_id IS NOT NULL
        ON CONFLICT DO NOTHING;

        -- Los equipos antiguos no tenían líder: se asigna a un miembro (el calendario personal, a su único dueño).
        UPDATE membresias m SET rol = 'lider'
        WHERE m.colaborador_id = (SELECT m2.colaborador_id FROM membresias m2
                                  WHERE m2.equipo_id = m.equipo_id ORDER BY m2.colaborador_id LIMIT 1)
          AND NOT EXISTS (SELECT 1 FROM membresias m3 WHERE m3.equipo_id = m.equipo_id AND m3.rol = 'lider');

        ALTER TABLE colaboradores DROP COLUMN equipo_id;
    END IF;
END $$;

UPDATE equipos e SET creado_por = (SELECT m.colaborador_id FROM membresias m
                                   WHERE m.equipo_id = e.id AND m.rol = 'lider' LIMIT 1)
WHERE e.creado_por IS NULL;

ALTER TABLE equipos DROP CONSTRAINT IF EXISTS equipos_nombre_key;

DROP INDEX IF EXISTS "IX_equipos_nombre";

DROP INDEX IF EXISTS uq_equipo_personal_por_usuario;

CREATE INDEX IF NOT EXISTS ix_membresias_colaborador ON membresias(colaborador_id);

CREATE INDEX IF NOT EXISTS ix_eventos_equipo_fecha ON eventos(equipo_id, fecha);

CREATE INDEX IF NOT EXISTS ix_eventos_organizador ON eventos(colaborador_organizador_id);

CREATE INDEX IF NOT EXISTS ix_invitaciones_invitado_estado ON invitaciones(invitado_id, estado);

CREATE UNIQUE INDEX IF NOT EXISTS uq_invitacion_pendiente
    ON invitaciones(equipo_id, invitado_id) WHERE estado = 'pendiente';

CREATE UNIQUE INDEX IF NOT EXISTS uq_membresias_un_lider ON membresias(equipo_id) WHERE rol = 'lider';

DO $$
BEGIN
    CREATE UNIQUE INDEX IF NOT EXISTS uq_colaboradores_usuario_ci ON colaboradores (lower(usuario));
EXCEPTION WHEN unique_violation THEN
    RAISE NOTICE 'Hay usuarios duplicados (sin distinguir mayúsculas): no se creó uq_colaboradores_usuario_ci.';
END $$;

DO $$
BEGIN
    CREATE UNIQUE INDEX IF NOT EXISTS uq_equipo_personal_por_creador ON equipos (creado_por) WHERE es_personal;
EXCEPTION WHEN unique_violation THEN
    RAISE NOTICE 'Hay usuarios con más de un calendario personal: no se creó uq_equipo_personal_por_creador.';
END $$;
