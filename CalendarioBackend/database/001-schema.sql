CREATE TABLE IF NOT EXISTS equipos (
    id uuid PRIMARY KEY,
    nombre varchar(120) NOT NULL UNIQUE,
    es_personal boolean NOT NULL DEFAULT false,
    creado_en timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS calendarios (
    id uuid PRIMARY KEY,
    equipo_id uuid NOT NULL UNIQUE REFERENCES equipos(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS colaboradores (
    id uuid PRIMARY KEY,
    equipo_id uuid NULL REFERENCES equipos(id) ON DELETE CASCADE,
    usuario varchar(100) NOT NULL,
    contrasena_hash varchar(255) NOT NULL
);

CREATE TABLE IF NOT EXISTS equipo_colaboradores (
    equipo_id uuid NOT NULL REFERENCES equipos(id) ON DELETE CASCADE,
    colaborador_id uuid NOT NULL REFERENCES colaboradores(id) ON DELETE CASCADE,
    rol varchar(20) NOT NULL DEFAULT 'Miembro',
    PRIMARY KEY (equipo_id, colaborador_id)
);

ALTER TABLE equipo_colaboradores
    ADD COLUMN IF NOT EXISTS rol varchar(20) NOT NULL DEFAULT 'Miembro';

CREATE TABLE IF NOT EXISTS eventos (
    id uuid PRIMARY KEY,
    equipo_id uuid NOT NULL REFERENCES equipos(id) ON DELETE CASCADE,
    fecha date NOT NULL,
    hora time NOT NULL,
    nombre varchar(160) NOT NULL,
    lugar varchar(200) NULL,
    descripcion varchar(500) NULL,
    colaborador_organizador_id uuid NULL REFERENCES colaboradores(id) ON DELETE SET NULL,
    CONSTRAINT uq_evento_equipo_fecha_hora UNIQUE (equipo_id, fecha, hora),
    CONSTRAINT fk_eventos_organizador FOREIGN KEY (colaborador_organizador_id)
        REFERENCES colaboradores(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_colaboradores_equipo_id ON colaboradores(equipo_id);
CREATE INDEX IF NOT EXISTS ix_equipo_colaboradores_colaborador_id ON equipo_colaboradores(colaborador_id);
CREATE INDEX IF NOT EXISTS ix_eventos_equipo_fecha ON eventos(equipo_id, fecha);
CREATE UNIQUE INDEX IF NOT EXISTS uq_colaboradores_equipo_usuario_ci
    ON colaboradores (equipo_id, lower(usuario));
CREATE UNIQUE INDEX IF NOT EXISTS uq_equipo_personal_por_usuario
    ON equipos (nombre) WHERE es_personal;

INSERT INTO equipo_colaboradores (equipo_id, colaborador_id)
SELECT equipo_id, id FROM colaboradores WHERE equipo_id IS NOT NULL
ON CONFLICT DO NOTHING;

UPDATE colaboradores SET equipo_id = NULL WHERE equipo_id IS NOT NULL;

WITH primer_miembro AS (
    SELECT DISTINCT ON (relacion.equipo_id)
        relacion.equipo_id,
        relacion.colaborador_id
    FROM equipo_colaboradores AS relacion
    JOIN equipos AS equipo ON equipo.id = relacion.equipo_id
    WHERE equipo.es_personal = false
    ORDER BY relacion.equipo_id, relacion.colaborador_id
)
UPDATE equipo_colaboradores AS relacion
SET rol = 'Líder'
FROM primer_miembro
WHERE relacion.equipo_id = primer_miembro.equipo_id
  AND relacion.colaborador_id = primer_miembro.colaborador_id
  AND NOT EXISTS (
      SELECT 1 FROM equipo_colaboradores AS lider
      WHERE lider.equipo_id = relacion.equipo_id AND lider.rol = 'Líder'
  );
