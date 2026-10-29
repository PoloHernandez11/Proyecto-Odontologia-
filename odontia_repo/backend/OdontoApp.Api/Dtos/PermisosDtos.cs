using System.ComponentModel.DataAnnotations;
using OdontoApp.Api.Models;

namespace OdontoApp.Api.Dtos;

/// <summary>Una celda de la matriz: módulo, acción y nivel.</summary>
public record PermisoItemDto(ModuloPermiso Modulo, AccionPermiso Accion, NivelPermiso Nivel);

/// <summary>Matriz completa que guarda el odontólogo para un rol.</summary>
public record MatrizPermisosDto([Required] List<PermisoItemDto> Items);

/// <summary>Una acción de la secretaria en el historial.</summary>
public record ActividadPermisoDto(
    int Id,
    string Usuario,
    string Email,
    ModuloPermiso Modulo,
    AccionPermiso Accion,
    NivelPermiso Nivel,
    DateTime CreadoEn);
