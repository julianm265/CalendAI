CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE equipos (
    id uuid PRIMARY KEY,
    nombre varchar(120) NOT NULL,
    creado_en timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE calendarios (
    id uuid PRIMARY KEY,
    equipo_id uuid NOT NULL,
    CONSTRAINT fk_calendarios_equipo
        FOREIGN KEY (equipo_id) REFERENCES equipos(id) ON DELETE CASCADE,
    CONSTRAINT uq_calendarios_equipo UNIQUE (equipo_id)
);

CREATE TABLE colaboradores (
    id uuid PRIMARY KEY,
    equipo_id uuid NOT NULL,
    usuario varchar(100) NOT NULL,
    contrasena_hash varchar(255) NOT NULL,
    CONSTRAINT fk_colaboradores_equipo
        FOREIGN KEY (equipo_id) REFERENCES equipos(id) ON DELETE CASCADE,
    CONSTRAINT uq_colaboradores_id UNIQUE (id)
);

CREATE TABLE eventos (
    id uuid PRIMARY KEY,
    equipo_id uuid NOT NULL,
    colaborador_organizador_id uuid NULL,
    fecha date NOT NULL,
    nombre varchar(160) NOT NULL,
    hora time NOT NULL,
    lugar varchar(200) NULL,
    descripcion text NULL,
    CONSTRAINT fk_eventos_equipo
        FOREIGN KEY (equipo_id) REFERENCES equipos(id) ON DELETE CASCADE,
    CONSTRAINT fk_eventos_organizador
        FOREIGN KEY (colaborador_organizador_id) REFERENCES colaboradores(id) ON DELETE SET NULL,
    CONSTRAINT uq_eventos_equipo_fecha_hora UNIQUE (equipo_id, fecha, hora)
);

CREATE INDEX ix_colaboradores_usuario ON colaboradores(usuario);
CREATE UNIQUE INDEX uq_equipos_nombre_ci ON equipos(lower(nombre));
CREATE UNIQUE INDEX uq_colaboradores_equipo_usuario_ci
    ON colaboradores(equipo_id, lower(usuario));
CREATE INDEX ix_eventos_equipo_fecha ON eventos(equipo_id, fecha);
