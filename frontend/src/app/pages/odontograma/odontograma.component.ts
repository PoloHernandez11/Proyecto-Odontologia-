
import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { OdontiaApi } from '../../api/odontia-api.service';
import { ConditionDto, FindingDto, FindingStatus, OdontogramCatalogDto, OdontogramDto, PatientDto, ToothCatalogDto, ToothStateDto, ToothSurface } from '../../api/schema';
import { fechaCorta, mensajeDeError } from '../../core/api';
import { AuthService } from '../../core/auth.service';

/** Zona del dibujo de la pieza. Cada zona corresponde a una o dos caras FDI según el diente. */
type Zona = 'arriba' | 'abajo' | 'izquierda' | 'derecha' | 'centro';
const CARAS_POR_ZONA: Record<Zona, ToothSurface[]> = {
  arriba: ['Vestibular'], abajo: ['Lingual', 'Palatal'], izquierda: ['Mesial'], derecha: ['Distal'], centro: ['Occlusal', 'Incisal']
};
const NOMBRE_DE_CARA: Record<ToothSurface, string> = {
  Occlusal: 'Oclusal', Incisal: 'Incisal', Mesial: 'Mesial', Distal: 'Distal', Vestibular: 'Vestibular', Lingual: 'Lingual', Palatal: 'Palatina'
};
const PIEZA_COMPLETA = 'completa';

@Component({ selector: 'app-odontograma', standalone: true, imports: [FormsModule, RouterLink], templateUrl: './odontograma.component.html', changeDetection: ChangeDetectionStrategy.Eager,
 styleUrl: './odontograma.component.css' })
export class OdontogramaComponent {
  private readonly api = inject(OdontiaApi);
  private readonly auth = inject(AuthService);
  private readonly pacienteId = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  readonly filas = [
    { titulo: 'DENTICIÓN PERMANENTE · ARCADA SUPERIOR', piezas: [18, 17, 16, 15, 14, 13, 12, 11, 21, 22, 23, 24, 25, 26, 27, 28], temporal: false },
    { titulo: 'DENTICIÓN PERMANENTE · ARCADA INFERIOR', piezas: [48, 47, 46, 45, 44, 43, 42, 41, 31, 32, 33, 34, 35, 36, 37, 38], temporal: false },
    { titulo: 'DENTICIÓN TEMPORAL · ARCADA SUPERIOR', piezas: [55, 54, 53, 52, 51, 61, 62, 63, 64, 65], temporal: true },
    { titulo: 'DENTICIÓN TEMPORAL · ARCADA INFERIOR', piezas: [85, 84, 83, 82, 81, 71, 72, 73, 74, 75], temporal: true }
  ];
  readonly zonas: { zona: Zona; puntos: string }[] = [
    { zona: 'arriba', puntos: '2,2 38,2 27,13 13,13' }, { zona: 'abajo', puntos: '2,38 13,27 27,27 38,38' },
    { zona: 'izquierda', puntos: '2,2 13,13 13,27 2,38' }, { zona: 'derecha', puntos: '38,2 38,38 27,27 27,13' },
    { zona: 'centro', puntos: '13,13 27,13 27,27 13,27' }
  ];
  readonly nombresDeCara = NOMBRE_DE_CARA;
  readonly piezaCompleta = PIEZA_COMPLETA;
  // Registrar en el odontograma exige el permiso y además ser profesional del consultorio.
  readonly puedeEscribir = this.auth.tiene('odontogram.write') && this.auth.esProfesional();
  readonly veHistoria = this.auth.tiene('clinical.read');

  paciente: PatientDto | null = null;
  catalogo: OdontogramCatalogDto | null = null;
  private estados = new Map<number, ToothStateDto>();
  private dientes = new Map<number, ToothCatalogDto>();
  ultimaModificacion = '';
  cargando = true;
  noEncontrado = false;
  mensaje = '';
  error = '';
  guardando = false;

  piezaSeleccionada: number | null = null;
  caraElegida: string = PIEZA_COMPLETA;
  condicionElegida = '';
  estadoElegido: FindingStatus = 'Existing';
  notaDelHallazgo = '';
  notaNueva = '';

  constructor() {
    forkJoin({ paciente: this.api.paciente(this.pacienteId), catalogo: this.api.catalogoOdontograma(), odontograma: this.api.odontograma(this.pacienteId) }).subscribe({
      next: ({ paciente, catalogo, odontograma }) => {
        this.paciente = paciente;
        this.catalogo = catalogo;
        this.dientes = new Map(catalogo.teeth.map(diente => [diente.code, diente]));
        this.aplicar(odontograma);
        this.cargando = false;
      },
      error: error => { this.cargando = false; this.noEncontrado = true; this.error = mensajeDeError(error, 'No se pudo cargar el odontograma.'); }
    });
  }

  private aplicar(odontograma: OdontogramDto): void {
    this.estados = new Map(odontograma.teeth.map(pieza => [pieza.toothCode, pieza]));
    this.ultimaModificacion = odontograma.lastChangeUtc ? `${fechaCorta(odontograma.lastChangeUtc)} · ${odontograma.lastChangeBy ?? ''}` : '';
  }

  // ── Dibujo ─────────────────────────────────────────────────────────────────
  get condiciones(): ConditionDto[] { return this.catalogo?.conditions.filter(condicion => condicion.isActive) ?? []; }
  estado(pieza: number): ToothStateDto | undefined { return this.estados.get(pieza); }
  ausente(pieza: number): boolean { return this.estados.get(pieza)?.isAbsent ?? false; }
  private hallazgoDeZona(pieza: number, zona: Zona): FindingDto | undefined {
    return this.estados.get(pieza)?.findings.find(hallazgo => hallazgo.surface && CARAS_POR_ZONA[zona].includes(hallazgo.surface));
  }
  hallazgoDePieza(pieza: number): FindingDto | undefined { return this.estados.get(pieza)?.findings.find(hallazgo => !hallazgo.surface); }
  colorDeZona(pieza: number, zona: Zona): string {
    if (this.ausente(pieza)) return '#d7e1e5';
    const hallazgo = this.hallazgoDeZona(pieza, zona);
    return hallazgo ? this.color(hallazgo) : '#f8fbfc';
  }
  color(hallazgo: FindingDto): string { return hallazgo.color || '#f4a477'; }
  descripcion(pieza: number): string {
    const estado = this.estados.get(pieza);
    const hallazgos = estado?.findings.map(h => `${h.conditionName}${h.surface ? ' (' + NOMBRE_DE_CARA[h.surface] + ')' : ''}`) ?? [];
    return `Pieza ${pieza}: ${hallazgos.length ? hallazgos.join(', ') : 'sin hallazgos'}`;
  }

  // ── Selección ──────────────────────────────────────────────────────────────
  seleccionarPieza(pieza: number, zona?: Zona): void {
    this.piezaSeleccionada = pieza;
    this.mensaje = '';
    this.error = '';
    this.notaDelHallazgo = '';
    const cara = zona ? this.carasDeLaPieza.find(c => CARAS_POR_ZONA[zona].includes(c)) : undefined;
    this.caraElegida = cara ?? PIEZA_COMPLETA;
    this.ajustarCondicion();
  }
  get carasDeLaPieza(): ToothSurface[] { return this.piezaSeleccionada ? this.dientes.get(this.piezaSeleccionada)?.surfaces ?? [] : []; }
  get nombreDeLaPieza(): string { return this.piezaSeleccionada ? this.dientes.get(this.piezaSeleccionada)?.name ?? '' : ''; }
  get hallazgosDeLaPieza(): FindingDto[] { return this.piezaSeleccionada ? this.estados.get(this.piezaSeleccionada)?.findings ?? [] : []; }
  get notasDeLaPieza() { return this.piezaSeleccionada ? this.estados.get(this.piezaSeleccionada)?.notes ?? [] : []; }
  /** Condiciones aplicables según se haya elegido la pieza completa o una cara. */
  get condicionesAplicables(): ConditionDto[] {
    const alcance = this.caraElegida === PIEZA_COMPLETA ? 'Tooth' : 'Surface';
    return this.condiciones.filter(condicion => condicion.scope === 'Both' || condicion.scope === alcance);
  }
  ajustarCondicion(): void {
    if (!this.condicionesAplicables.some(condicion => condicion.id === this.condicionElegida)) this.condicionElegida = this.condicionesAplicables[0]?.id ?? '';
  }

  // ── Cambios ────────────────────────────────────────────────────────────────
  agregarHallazgo(): void {
    if (!this.piezaSeleccionada || !this.condicionElegida) return;
    this.guardando = true;
    this.error = '';
    this.api.aplicarHallazgos(this.pacienteId, [{
      toothCode: this.piezaSeleccionada,
      // La API acepta null para registrar la condición sobre la pieza completa.
      surface: (this.caraElegida === PIEZA_COMPLETA ? null : this.caraElegida) as ToothSurface,
      conditionId: this.condicionElegida, status: this.estadoElegido, notes: this.notaDelHallazgo.trim() || null
    }]).subscribe({
      next: odontograma => { this.guardando = false; this.aplicar(odontograma); this.notaDelHallazgo = ''; this.mensaje = `Se actualizó la pieza ${this.piezaSeleccionada}.`; },
      error: error => { this.guardando = false; this.error = mensajeDeError(error, 'No se pudo registrar el hallazgo.'); }
    });
  }

  quitarHallazgo(hallazgo: FindingDto): void {
    this.guardando = true;
    this.error = '';
    this.api.quitarHallazgo(this.pacienteId, hallazgo.id, 'Corrección del registro').subscribe({
      next: odontograma => { this.guardando = false; this.aplicar(odontograma); this.mensaje = `Se quitó "${hallazgo.conditionName}" de la pieza ${hallazgo.toothCode}.`; },
      error: error => { this.guardando = false; this.error = mensajeDeError(error, 'No se pudo quitar el hallazgo.'); }
    });
  }

  agregarNota(): void {
    const texto = this.notaNueva.trim();
    if (!this.piezaSeleccionada || !texto) return;
    this.guardando = true;
    this.api.notaDePieza(this.pacienteId, this.piezaSeleccionada, texto).subscribe({
      next: () => {
        this.notaNueva = '';
        this.api.odontograma(this.pacienteId).subscribe({ next: odontograma => { this.guardando = false; this.aplicar(odontograma); this.mensaje = 'Se agregó la observación.'; } });
      },
      error: error => { this.guardando = false; this.error = mensajeDeError(error, 'No se pudo guardar la observación.'); }
    });
  }

  fecha(valor: string): string { return fechaCorta(valor); }
}
