-- ============================================
-- Sistema de Ficha Dental - Script completo
-- 6 tablas | Paciente <-> Odontograma: 1 a 1
-- ============================================

CREATE DATABASE IF NOT EXISTS ficha_dental
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE ficha_dental;

CREATE TABLE usuario (
    id          INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nombre      VARCHAR(50) NOT NULL,
    usuario     VARCHAR(30) NOT NULL UNIQUE,
    password    VARCHAR(255) NOT NULL,  -- hash, nunca texto plano
    rol         ENUM('secretaria','dentista') NOT NULL
);

CREATE TABLE paciente (
    id                  INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nombre              VARCHAR(50) NOT NULL,
    apellido            VARCHAR(50) NOT NULL,
    dni                 VARCHAR(20) NOT NULL UNIQUE,
    telefono            VARCHAR(20) NOT NULL,
    email               VARCHAR(100),
    fecha_nacimiento    DATE,
    obra_social         VARCHAR(50),
    nro_afiliado        VARCHAR(30),
    creado_en           DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE turno (
    id          INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    paciente_id INT UNSIGNED NOT NULL,
    usuario_id  INT UNSIGNED NOT NULL,  -- quien lo agendo
    fecha       DATE NOT NULL,
    hora        TIME NOT NULL,
    estado      ENUM('pendiente','confirmado','cancelado','completado') NOT NULL DEFAULT 'pendiente',
    motivo      VARCHAR(100),
    creado_en   DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (paciente_id) REFERENCES paciente(id),
    FOREIGN KEY (usuario_id) REFERENCES usuario(id)
);

CREATE TABLE odontograma (
    id              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    paciente_id     INT UNSIGNED NOT NULL UNIQUE,
    usuario_id      INT UNSIGNED NOT NULL,  -- quien lo modifico por ultima vez
    fecha_registro  DATETIME DEFAULT CURRENT_TIMESTAMP,
    observaciones   TEXT,
    FOREIGN KEY (paciente_id) REFERENCES paciente(id),
    FOREIGN KEY (usuario_id) REFERENCES usuario(id)
);

CREATE TABLE odontograma_diente (
    id              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    odontograma_id  INT UNSIGNED NOT NULL,
    numero_diente   TINYINT UNSIGNED NOT NULL,  -- 11 a 48 (numeracion FDI)
    estado_general  ENUM('presente','ausente','extraido','implante') NOT NULL DEFAULT 'presente',
    oclusal         ENUM('sano','caries','obturado','corona','sellante') NOT NULL DEFAULT 'sano',
    mesial          ENUM('sano','caries','obturado','corona','sellante') NOT NULL DEFAULT 'sano',
    distal          ENUM('sano','caries','obturado','corona','sellante') NOT NULL DEFAULT 'sano',
    vestibular      ENUM('sano','caries','obturado','corona','sellante') NOT NULL DEFAULT 'sano',
    lingual         ENUM('sano','caries','obturado','corona','sellante') NOT NULL DEFAULT 'sano',
    FOREIGN KEY (odontograma_id) REFERENCES odontograma(id)
);

CREATE TABLE historial_clinico (
    id          INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    paciente_id INT UNSIGNED NOT NULL,
    usuario_id  INT UNSIGNED NOT NULL,  -- el dentista que la escribio
    turno_id    INT UNSIGNED,            -- opcional
    fecha       DATETIME DEFAULT CURRENT_TIMESTAMP,
    descripcion TEXT NOT NULL,
    FOREIGN KEY (paciente_id) REFERENCES paciente(id),
    FOREIGN KEY (usuario_id) REFERENCES usuario(id),
    FOREIGN KEY (turno_id) REFERENCES turno(id)
);
