-- ============================================
-- Datos de prueba - Sistema de Ficha Dental
-- Ejecutar DESPUES de ficha_dental.sql
-- Solo para desarrollo: NO usar en produccion
-- ============================================

USE ficha_dental;

INSERT INTO usuario (nombre, usuario, password, rol) VALUES
    ('Dr Test',  'dentista1',   'hash_falso', 'dentista'),
    ('Sec Test', 'secretaria1', 'hash_falso', 'secretaria');

INSERT INTO paciente (nombre, apellido, dni, telefono, obra_social, nro_afiliado) VALUES
    ('Juan', 'Perez', '12345678', '2235551234', 'OSDE', '0001');

INSERT INTO turno (paciente_id, usuario_id, fecha, hora, motivo) VALUES
    (1, 2, '2026-10-15', '10:00:00', 'Control');

INSERT INTO odontograma (paciente_id, usuario_id) VALUES (1, 1);

INSERT INTO odontograma_diente (odontograma_id, numero_diente) VALUES
    (1, 11), (1, 36);

UPDATE odontograma_diente
SET oclusal = 'caries'
WHERE odontograma_id = 1 AND numero_diente = 36;

INSERT INTO historial_clinico (paciente_id, usuario_id, turno_id, descripcion) VALUES
    (1, 1, 1, 'Caries en el 36');
