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
CREATE INDEX IF NOT EXISTS ix_eventos_equipo_fecha ON eventos(equipo_id, fecha);
CREATE UNIQUE INDEX IF NOT EXISTS uq_colaboradores_equipo_usuario_ci
    ON colaboradores (equipo_id, lower(usuario));
CREATE UNIQUE INDEX IF NOT EXISTS uq_equipo_personal_por_usuario
    ON equipos (nombre) WHERE es_personal;
