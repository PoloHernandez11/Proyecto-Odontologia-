
import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
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

/**
 * Únicas condiciones que se ofrecen, en el orden de la ficha en papel del consultorio.
 * Los nombres se fijan acá aunque el catálogo del backend use otros.
 */
const CODIGOS = ['EXTRACTION', 'CARIES', 'FILLING', 'IMPLANT', 'MISSING', 'CROWN'];
const NOMBRE_DE_CODIGO: Record<string, string> = {
  EXTRACTION: 'Extraer', CARIES: 'Caries', FILLING: 'Arreglo', IMPLANT: 'Implante', MISSING: 'Ausente', CROWN: 'Corona'
};
/** Caries y arreglos se marcan con una rayita del color correspondiente sobre la cara. */
const COLOR_DE_CODIGO: Record<string, string> = { CARIES: '#d62828', FILLING: '#1f5fcc' };
const COLOR_POR_DEFECTO = '#f4a477';
/** Trazo corto en el centro de cada zona, a lo largo de la zona. */
const RAYITA: Record<Zona, { x1: number; y1: number; x2: number; y2: number; vertical: boolean }> = {
  arriba: { x1: 15, y1: 7.5, x2: 25, y2: 7.5, vertical: false }, abajo: { x1: 15, y1: 32.5, x2: 25, y2: 32.5, vertical: false },
  izquierda: { x1: 7.5, y1: 15, x2: 7.5, y2: 25, vertical: true }, derecha: { x1: 32.5, y1: 15, x2: 32.5, y2: 25, vertical: true },
  centro: { x1: 15, y1: 20, x2: 25, y2: 20, vertical: false }
};
/** Símbolos de pieza completa, dibujados sobre el diente (viewBox 0 0 40 40). */
export interface Simbolo { trazos: string; circulo?: boolean; letra?: string; }
const SIMBOLOS: Record<string, Simbolo> = {
  EXTRACTION: { trazos: 'M5 15H35M5 25H35' },
  IMPLANT: { trazos: 'M20 5V35M13 5H27M13 35H27' },
  MISSING: { trazos: 'M5 5L35 35M35 5L5 35' },
  CROWN: { trazos: '', circulo: true, letra: 'C' }
};

interface Rayita { x1: number; y1: number; x2: number; y2: number; color: string; planificado: boolean; }
interface SimboloDePieza extends Simbolo { planificado: boolean; }
/** Lo que se dibuja de cada pieza, calculado una vez por cada carga del odontograma. */
interface Dibujo { rayitas: Rayita[]; simbolos: SimboloDePieza[]; ausente: boolean; otro?: FindingDto; }

const codigo = (valor: string | null | undefined): string => (valor ?? '').toUpperCase();

@Component({ selector: 'app-odontograma', standalone: true, imports: [FormsModule, NgTemplateOutlet, RouterLink], templateUrl: './odontograma.component.html', changeDetection: ChangeDetectionStrategy.Eager,
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
    this.dibujos = new Map(odontograma.teeth.map(pieza => [pieza.toothCode, this.dibujar(pieza)]));
    this.ultimaModificacion = odontograma.lastChangeUtc ? `${fechaCorta(odontograma.lastChangeUtc)} · ${odontograma.lastChangeBy ?? ''}` : '';
  }

  // ── Dibujo ─────────────────────────────────────────────────────────────────
  private dibujos = new Map<number, Dibujo>();
  private static readonly SIN_DIBUJO: Dibujo = { rayitas: [], simbolos: [], ausente: false };

  /** Traduce los hallazgos de una pieza a rayitas por cara y símbolos de pieza completa. */
  private dibujar(estado: ToothStateDto): Dibujo {
    const porZona = new Map<Zona, { color: string; planificado: boolean }[]>();
    const simbolos: SimboloDePieza[] = [];
    let otro: FindingDto | undefined;
    for (const hallazgo of estado.findings) {
      const cod = codigo(hallazgo.conditionCode);
      const planificado = hallazgo.status === 'Planned';
      const simbolo = SIMBOLOS[cod];
      if (simbolo) { simbolos.push({ ...simbolo, planificado }); continue; }
      // Caries, arreglos (y condiciones anteriores por cara): rayita en la cara; sin cara, en el centro.
      const zona = hallazgo.surface ? this.zonaDeCara(hallazgo.surface) : COLOR_DE_CODIGO[cod] ? 'centro' : undefined;
      if (!zona) { otro ??= hallazgo; continue; }
      porZona.set(zona, [...(porZona.get(zona) ?? []), { color: this.color(hallazgo), planificado }]);
    }
    const rayitas: Rayita[] = [];
    for (const [zona, marcas] of porZona) {
      const base = RAYITA[zona];
      // Si hay más de una marca en la misma cara, se dibujan en paralelo para que se vean todas.
      marcas.forEach((marca, indice) => {
        const desplazamiento = (indice - (marcas.length - 1) / 2) * 4;
        const dx = base.vertical ? desplazamiento : 0, dy = base.vertical ? 0 : desplazamiento;
        rayitas.push({ x1: base.x1 + dx, y1: base.y1 + dy, x2: base.x2 + dx, y2: base.y2 + dy, ...marca });
      });
    }
    return { rayitas, simbolos, otro, ausente: estado.findings.some(h => codigo(h.conditionCode) === 'MISSING') };
  }
  private zonaDeCara(cara: ToothSurface): Zona | undefined {
    return (Object.keys(CARAS_POR_ZONA) as Zona[]).find(zona => CARAS_POR_ZONA[zona].includes(cara));
  }

  get condiciones(): ConditionDto[] {
    const activas = this.catalogo?.conditions.filter(condicion => condicion.isActive && CODIGOS.includes(codigo(condicion.code))) ?? [];
    return activas.sort((a, b) => CODIGOS.indexOf(codigo(a.code)) - CODIGOS.indexOf(codigo(b.code)));
  }
  dibujo(pieza: number): Dibujo { return this.dibujos.get(pieza) ?? OdontogramaComponent.SIN_DIBUJO; }
  /** Sólo "Ausente" deja la pieza tachada; "Extraer" indica que la pieza todavía está. */
  ausente(pieza: number): boolean { return this.dibujo(pieza).ausente; }
  nombre(condicion: ConditionDto): string { return NOMBRE_DE_CODIGO[codigo(condicion.code)] ?? condicion.name; }
  nombreDeHallazgo(hallazgo: FindingDto): string { return NOMBRE_DE_CODIGO[codigo(hallazgo.conditionCode)] ?? hallazgo.conditionName; }
  simboloDe(valor: string): Simbolo | undefined { return SIMBOLOS[codigo(valor)]; }
  color(hallazgo: FindingDto): string { return COLOR_DE_CODIGO[codigo(hallazgo.conditionCode)] ?? (hallazgo.color || COLOR_POR_DEFECTO); }
  colorDeCondicion(condicion: ConditionDto): string { return COLOR_DE_CODIGO[codigo(condicion.code)] ?? (condicion.color || COLOR_POR_DEFECTO); }
  descripcion(pieza: number): string {
    const estado = this.estados.get(pieza);
    const hallazgos = estado?.findings.map(h => `${this.nombreDeHallazgo(h)}${h.surface ? ' (' + NOMBRE_DE_CARA[h.surface] + ')' : ''}${h.status === 'Planned' ? ' [planificado]' : ''}`) ?? [];
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
  /**
   * Se ofrecen siempre todas las condiciones activas, ordenadas según la ubicación elegida
   * (primero las que aplican a la cara o a la pieza completa). Si se elige una condición que no
   * corresponde a la ubicación actual, la ubicación se ajusta sola (ver alCambiarCondicion).
   */
  get condicionesAplicables(): ConditionDto[] {
    return [...this.condiciones.filter(c => this.aplicaALaUbicacion(c)), ...this.condiciones.filter(c => !this.aplicaALaUbicacion(c))];
  }
  /** Indica, en la lista, dónde se registra una condición que no corresponde a la ubicación actual. */
  etiquetaDeCondicion(condicion: ConditionDto): string {
    if (condicion.scope === 'Tooth' && this.caraElegida !== PIEZA_COMPLETA) return `${this.nombre(condicion)} (pieza completa)`;
    if (condicion.scope === 'Surface' && this.caraElegida === PIEZA_COMPLETA) return `${this.nombre(condicion)} (por cara)`;
    return this.nombre(condicion);
  }
  private aplicaALaUbicacion(condicion: ConditionDto): boolean {
    return condicion.scope === 'Both' || condicion.scope === (this.caraElegida === PIEZA_COMPLETA ? 'Tooth' : 'Surface');
  }
  /** Al cambiar la ubicación, si la condición elegida no corresponde, se propone la primera que sí. */
  ajustarCondicion(): void {
    const actual = this.condiciones.find(condicion => condicion.id === this.condicionElegida);
    if (!actual || !this.aplicaALaUbicacion(actual)) this.condicionElegida = this.condicionesAplicables[0]?.id ?? '';
  }
  /** Lleva la ubicación a una compatible con la condición: pieza completa o, si exige cara, la primera cara de la pieza. */
  alCambiarCondicion(): void {
    const condicion = this.condiciones.find(c => c.id === this.condicionElegida);
    if (!condicion) return;
    if (condicion.scope === 'Tooth' && this.caraElegida !== PIEZA_COMPLETA) this.caraElegida = PIEZA_COMPLETA;
    else if (condicion.scope === 'Surface' && this.caraElegida === PIEZA_COMPLETA) this.caraElegida = this.carasDeLaPieza[0] ?? PIEZA_COMPLETA;
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
      next: odontograma => { this.guardando = false; this.aplicar(odontograma); this.mensaje = `Se quitó "${this.nombreDeHallazgo(hallazgo)}" de la pieza ${hallazgo.toothCode}.`; },
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
