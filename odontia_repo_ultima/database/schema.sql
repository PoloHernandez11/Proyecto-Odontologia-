-- Base completa de Odontia: crea la base y sus tablas desde cero.
-- Ejecutar en una base vacía (si ya existen las tablas, el script falla).
CREATE DATABASE IF NOT EXISTS `odontoapp` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `odontoapp`;

ALTER DATABASE CHARACTER SET utf8mb4;


CREATE TABLE `paciente` (
    `id` int NOT NULL AUTO_INCREMENT,
    `nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `apellido` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `dni` varchar(15) CHARACTER SET utf8mb4 NOT NULL,
    `telefono` varchar(30) CHARACTER SET utf8mb4 NULL,
    `email` varchar(150) CHARACTER SET utf8mb4 NULL,
    `fecha_nacimiento` date NULL,
    `sexo` varchar(20) CHARACTER SET utf8mb4 NULL,
    `ocupacion` varchar(100) CHARACTER SET utf8mb4 NULL,
    `domicilio` varchar(200) CHARACTER SET utf8mb4 NULL,
    `contacto_emergencia` varchar(150) CHARACTER SET utf8mb4 NULL,
    `obra_social` varchar(100) CHARACTER SET utf8mb4 NULL,
    `plan` varchar(50) CHARACTER SET utf8mb4 NULL,
    `nro_afiliado` varchar(50) CHARACTER SET utf8mb4 NULL,
    `grupo_sanguineo` varchar(5) CHARACTER SET utf8mb4 NULL,
    `diabetes` tinyint(1) NOT NULL DEFAULT 0,
    `hipertension` tinyint(1) NOT NULL DEFAULT 0,
    `embarazo` tinyint(1) NOT NULL DEFAULT 0,
    `asma` tinyint(1) NOT NULL DEFAULT 0,
    `cardiopatia` tinyint(1) NOT NULL DEFAULT 0,
    `anticoagulado` tinyint(1) NOT NULL DEFAULT 0,
    `hepatitis` tinyint(1) NOT NULL DEFAULT 0,
    `fumador` tinyint(1) NOT NULL DEFAULT 0,
    `alergias` varchar(255) CHARACTER SET utf8mb4 NULL,
    `medicacion_actual` varchar(255) CHARACTER SET utf8mb4 NULL,
    `otros_antecedentes` text CHARACTER SET utf8mb4 NULL,
    `consentimiento_firmado` tinyint(1) NOT NULL DEFAULT 0,
    `creado_en` datetime(6) NOT NULL,
    CONSTRAINT `PK_paciente` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;


-- Consultorio: la "empresa". La crea el primer odontólogo al registrarse.
CREATE TABLE `consultorio` (
    `id` int NOT NULL AUTO_INCREMENT,
    `nombre` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
    `creado_en` datetime(6) NOT NULL,
    CONSTRAINT `PK_consultorio` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

-- Personas autorizadas a ingresar. Sin contraseñas: la autenticación es con Google.
CREATE TABLE `usuario` (
    `id` int NOT NULL AUTO_INCREMENT,
    `consultorio_id` int NOT NULL,
    `nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `email` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `google_sub` varchar(255) CHARACTER SET ascii NULL,
    `rol` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `activo` tinyint(1) NOT NULL DEFAULT 1,
    `creado_en` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_usuario` PRIMARY KEY (`id`),
    CONSTRAINT `UQ_usuario_google_sub` UNIQUE (`google_sub`),
    CONSTRAINT `UQ_usuario_consultorio_email` UNIQUE (`consultorio_id`, `email`),
    CONSTRAINT `FK_usuario_consultorio` FOREIGN KEY (`consultorio_id`)
        REFERENCES `consultorio` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

-- Invitación para sumar a la secretaria. Se guarda solo el hash SHA-256 del token.
CREATE TABLE `invitacion` (
    `id` int NOT NULL AUTO_INCREMENT,
    `consultorio_id` int NOT NULL,
    `email` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `rol` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `token_hash` char(64) CHARACTER SET ascii NOT NULL,
    `creada_por_id` int NOT NULL,
    `creada_en` datetime(6) NOT NULL,
    `expira_en` datetime(6) NOT NULL,
    `usada_en` datetime(6) NULL,
    `usuario_creado_id` int NULL,
    `revocada_en` datetime(6) NULL,
    CONSTRAINT `PK_invitacion` PRIMARY KEY (`id`),
    CONSTRAINT `UQ_invitacion_token_hash` UNIQUE (`token_hash`),
    CONSTRAINT `FK_invitacion_consultorio` FOREIGN KEY (`consultorio_id`)
        REFERENCES `consultorio` (`id`) ON DELETE CASCADE,
    CONSTRAINT `FK_invitacion_creada_por` FOREIGN KEY (`creada_por_id`)
        REFERENCES `usuario` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_invitacion_consultorio_email` ON `invitacion` (`consultorio_id`, `email`);


CREATE TABLE `odontograma` (
    `id` int NOT NULL AUTO_INCREMENT,
    `cliente_id` int NOT NULL,
    `usuario_id` int NOT NULL,
    `fecha_registro` datetime(6) NOT NULL,
    `observaciones` text CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_odontograma` PRIMARY KEY (`id`),
    CONSTRAINT `FK_odontograma_paciente_cliente_id` FOREIGN KEY (`cliente_id`) REFERENCES `paciente` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_odontograma_usuario_usuario_id` FOREIGN KEY (`usuario_id`) REFERENCES `usuario` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `pago` (
    `id` int NOT NULL AUTO_INCREMENT,
    `paciente_id` int NOT NULL,
    `usuario_id` int NOT NULL,
    `fecha` datetime(6) NOT NULL,
    `monto` decimal(12,2) NOT NULL,
    `medio_pago` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `concepto` varchar(255) CHARACTER SET utf8mb4 NULL,
    `anulado` tinyint(1) NOT NULL,
    CONSTRAINT `PK_pago` PRIMARY KEY (`id`),
    CONSTRAINT `FK_pago_paciente_paciente_id` FOREIGN KEY (`paciente_id`) REFERENCES `paciente` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_pago_usuario_usuario_id` FOREIGN KEY (`usuario_id`) REFERENCES `usuario` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `trabajo_laboratorio` (
    `id` int NOT NULL AUTO_INCREMENT,
    `paciente_id` int NOT NULL,
    `usuario_id` int NOT NULL,
    `laboratorio` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `tipo_trabajo` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `piezas` varchar(100) CHARACTER SET utf8mb4 NULL,
    `descripcion` text CHARACTER SET utf8mb4 NULL,
    `fecha_envio` date NOT NULL,
    `fecha_entrega_estimada` date NULL,
    `fecha_recepcion` date NULL,
    `estado` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `costo` decimal(12,2) NULL,
    `pagado` tinyint(1) NOT NULL,
    `creado_en` datetime(6) NOT NULL,
    CONSTRAINT `PK_trabajo_laboratorio` PRIMARY KEY (`id`),
    CONSTRAINT `FK_trabajo_laboratorio_paciente_paciente_id` FOREIGN KEY (`paciente_id`) REFERENCES `paciente` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_trabajo_laboratorio_usuario_usuario_id` FOREIGN KEY (`usuario_id`) REFERENCES `usuario` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `turno` (
    `id` int NOT NULL AUTO_INCREMENT,
    `paciente_id` int NOT NULL,
    `usuario_id` int NOT NULL,
    `fecha` date NOT NULL,
    `hora` time(6) NOT NULL,
    `duracion_minutos` int NOT NULL DEFAULT 30,
    `estado` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `motivo` varchar(255) CHARACTER SET utf8mb4 NULL,
    `notas` varchar(255) CHARACTER SET utf8mb4 NULL,
    `urgencia` tinyint(1) NOT NULL DEFAULT 0,
    `hora_llegada` time(6) NULL,
    `creado_en` datetime(6) NOT NULL,
    CONSTRAINT `PK_turno` PRIMARY KEY (`id`),
    CONSTRAINT `FK_turno_paciente_paciente_id` FOREIGN KEY (`paciente_id`) REFERENCES `paciente` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_turno_usuario_usuario_id` FOREIGN KEY (`usuario_id`) REFERENCES `usuario` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `odontograma_diente` (
    `id` int NOT NULL AUTO_INCREMENT,
    `odontograma_id` int NOT NULL,
    `numero_diente` tinyint unsigned NOT NULL,
    `estado_general` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `oclusal` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `mesial` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `distal` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `vestibular` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `lingual` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `nota` varchar(255) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_odontograma_diente` PRIMARY KEY (`id`),
    CONSTRAINT `FK_odontograma_diente_odontograma_odontograma_id` FOREIGN KEY (`odontograma_id`) REFERENCES `odontograma` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `permiso_rol` (
    `id` int NOT NULL AUTO_INCREMENT,
    `consultorio_id` int NOT NULL,
    `rol` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `modulo` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
    `accion` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `nivel` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_permiso_rol` PRIMARY KEY (`id`),
    CONSTRAINT `UQ_permiso_rol` UNIQUE (`consultorio_id`, `rol`, `modulo`, `accion`),
    CONSTRAINT `FK_permiso_rol_consultorio` FOREIGN KEY (`consultorio_id`) REFERENCES `consultorio` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `actividad_permiso` (
    `id` int NOT NULL AUTO_INCREMENT,
    `consultorio_id` int NOT NULL,
    `usuario_id` int NOT NULL,
    `modulo` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
    `accion` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `nivel` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `creado_en` datetime(6) NOT NULL,
    CONSTRAINT `PK_actividad_permiso` PRIMARY KEY (`id`),
    CONSTRAINT `FK_actividad_permiso_consultorio` FOREIGN KEY (`consultorio_id`) REFERENCES `consultorio` (`id`) ON DELETE CASCADE,
    CONSTRAINT `FK_actividad_permiso_usuario` FOREIGN KEY (`usuario_id`) REFERENCES `usuario` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `movimiento_caja` (
    `id` int NOT NULL AUTO_INCREMENT,
    `tipo` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `categoria` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `fecha` date NOT NULL,
    `descripcion` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `monto` decimal(12,2) NOT NULL,
    `medio_pago` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `usuario_id` int NOT NULL,
    `pago_id` int NULL,
    `trabajo_laboratorio_id` int NULL,
    `creado_en` datetime(6) NOT NULL,
    CONSTRAINT `PK_movimiento_caja` PRIMARY KEY (`id`),
    CONSTRAINT `FK_movimiento_caja_pago_pago_id` FOREIGN KEY (`pago_id`) REFERENCES `pago` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_movimiento_caja_trabajo_laboratorio_trabajo_laboratorio_id` FOREIGN KEY (`trabajo_laboratorio_id`) REFERENCES `trabajo_laboratorio` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_movimiento_caja_usuario_usuario_id` FOREIGN KEY (`usuario_id`) REFERENCES `usuario` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `historial_clinico` (
    `id` int NOT NULL AUTO_INCREMENT,
    `paciente_id` int NOT NULL,
    `usuario_id` int NOT NULL,
    `turno_id` int NULL,
    `fecha` datetime(6) NOT NULL,
    `tipo_registro` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Consulta',
    `tipo_consulta` varchar(20) CHARACTER SET utf8mb4 NULL,
    `ta_max` int NULL,
    `ta_min` int NULL,
    `peso` decimal(5,1) NULL,
    `talla` decimal(5,1) NULL,
    `temperatura` decimal(4,1) NULL,
    `frecuencia_cardiaca` int NULL,
    `frecuencia_respiratoria` int NULL,
    `saturacion_o2` int NULL,
    `motivo_consulta` varchar(255) CHARACTER SET utf8mb4 NULL,
    `antecedentes_examen` text CHARACTER SET utf8mb4 NULL,
    `diagnostico` text CHARACTER SET utf8mb4 NULL,
    `tratamiento` text CHARACTER SET utf8mb4 NULL,
    `prescripcion` text CHARACTER SET utf8mb4 NULL,
    `informe` text CHARACTER SET utf8mb4 NULL,
    `piezas` varchar(100) CHARACTER SET utf8mb4 NULL,
    `descripcion` text CHARACTER SET utf8mb4 NOT NULL,
    `importe` decimal(12,2) NULL,
    CONSTRAINT `PK_historial_clinico` PRIMARY KEY (`id`),
    CONSTRAINT `FK_historial_clinico_paciente_paciente_id` FOREIGN KEY (`paciente_id`) REFERENCES `paciente` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_historial_clinico_turno_turno_id` FOREIGN KEY (`turno_id`) REFERENCES `turno` (`id`) ON DELETE SET NULL,
    CONSTRAINT `FK_historial_clinico_usuario_usuario_id` FOREIGN KEY (`usuario_id`) REFERENCES `usuario` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `diagnostico_atencion` (
    `id` int NOT NULL AUTO_INCREMENT,
    `historial_id` int NOT NULL,
    `orden` tinyint unsigned NOT NULL,
    `codigo` varchar(10) CHARACTER SET utf8mb4 NULL,
    `descripcion` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `caracter` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_diagnostico_atencion` PRIMARY KEY (`id`),
    CONSTRAINT `FK_diagnostico_atencion_historial_clinico_historial_id` FOREIGN KEY (`historial_id`) REFERENCES `historial_clinico` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `practica_atencion` (
    `id` int NOT NULL AUTO_INCREMENT,
    `historial_id` int NOT NULL,
    `codigo` varchar(10) CHARACTER SET utf8mb4 NULL,
    `descripcion` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `piezas` varchar(100) CHARACTER SET utf8mb4 NULL,
    `observaciones` varchar(500) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_practica_atencion` PRIMARY KEY (`id`),
    CONSTRAINT `FK_practica_atencion_historial_clinico_historial_id` FOREIGN KEY (`historial_id`) REFERENCES `historial_clinico` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `tarea` (
    `id` int NOT NULL AUTO_INCREMENT,
    `texto` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `prioridad` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `categoria` varchar(30) CHARACTER SET utf8mb4 NULL,
    `vence` date NULL,
    `completada` tinyint(1) NOT NULL,
    `completada_en` datetime(6) NULL,
    `usuario_id` int NOT NULL,
    `creado_en` datetime(6) NOT NULL,
    CONSTRAINT `PK_tarea` PRIMARY KEY (`id`),
    CONSTRAINT `FK_tarea_usuario_usuario_id` FOREIGN KEY (`usuario_id`) REFERENCES `usuario` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE INDEX `IX_diagnostico_atencion_historial_id` ON `diagnostico_atencion` (`historial_id`);


CREATE INDEX `IX_practica_atencion_historial_id` ON `practica_atencion` (`historial_id`);


CREATE INDEX `IX_tarea_completada_vence` ON `tarea` (`completada`, `vence`);


CREATE INDEX `IX_tarea_usuario_id` ON `tarea` (`usuario_id`);


CREATE INDEX `IX_historial_clinico_paciente_id_fecha` ON `historial_clinico` (`paciente_id`, `fecha`);


CREATE INDEX `IX_historial_clinico_turno_id` ON `historial_clinico` (`turno_id`);


CREATE INDEX `IX_historial_clinico_usuario_id` ON `historial_clinico` (`usuario_id`);


CREATE INDEX `IX_movimiento_caja_fecha` ON `movimiento_caja` (`fecha`);


CREATE UNIQUE INDEX `IX_movimiento_caja_pago_id` ON `movimiento_caja` (`pago_id`);


CREATE UNIQUE INDEX `IX_movimiento_caja_trabajo_laboratorio_id` ON `movimiento_caja` (`trabajo_laboratorio_id`);


CREATE INDEX `IX_movimiento_caja_usuario_id` ON `movimiento_caja` (`usuario_id`);


CREATE INDEX `IX_odontograma_cliente_id_fecha_registro` ON `odontograma` (`cliente_id`, `fecha_registro`);


CREATE INDEX `IX_odontograma_usuario_id` ON `odontograma` (`usuario_id`);


CREATE UNIQUE INDEX `IX_odontograma_diente_odontograma_id_numero_diente` ON `odontograma_diente` (`odontograma_id`, `numero_diente`);


CREATE UNIQUE INDEX `IX_paciente_dni` ON `paciente` (`dni`);


CREATE INDEX `IX_pago_paciente_id_fecha` ON `pago` (`paciente_id`, `fecha`);


CREATE INDEX `IX_pago_usuario_id` ON `pago` (`usuario_id`);


CREATE INDEX `IX_trabajo_laboratorio_estado_fecha_envio` ON `trabajo_laboratorio` (`estado`, `fecha_envio`);


CREATE INDEX `IX_trabajo_laboratorio_paciente_id` ON `trabajo_laboratorio` (`paciente_id`);


CREATE INDEX `IX_trabajo_laboratorio_usuario_id` ON `trabajo_laboratorio` (`usuario_id`);


CREATE INDEX `IX_turno_fecha_hora` ON `turno` (`fecha`, `hora`);


CREATE INDEX `IX_turno_paciente_id` ON `turno` (`paciente_id`);


CREATE INDEX `IX_turno_usuario_id` ON `turno` (`usuario_id`);






