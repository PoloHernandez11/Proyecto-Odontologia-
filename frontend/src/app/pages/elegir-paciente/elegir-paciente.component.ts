import { Component, OnDestroy, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, Subscription, debounceTime, startWith, switchMap } from 'rxjs';
import { OdontiaApi } from '../../api/odontia-api.service';
import { PatientListItemDto } from '../../api/schema';
import { fechaCorta, iniciales, mensajeDeError } from '../../core/api';

/** Paso previo de "Historia clínica" y "Odontograma": elegir de qué paciente se quiere ver la información. */
@Component({
  selector: 'app-elegir-paciente',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <main class="picker">
      <header><h1>{{ titulo }}</h1><p>Buscá el paciente cuya {{ titulo.toLowerCase() === 'odontograma' ? 'ficha dental' : 'historia clínica' }} querés abrir.</p></header>
      <label class="search"><span aria-hidden="true">⌕</span><input type="search" autofocus aria-label="Buscar paciente" placeholder="Buscar por nombre, DNI o teléfono..." [(ngModel)]="busqueda" (ngModelChange)="buscar()"></label>
      @if (error) { <p class="error" role="alert">{{ error }}</p> }
      <section class="results">
        @for (paciente of pacientes; track paciente.id) {
          <a class="result" [routerLink]="[destino, paciente.id]">
            <span class="avatar">{{ iniciales(paciente) }}</span>
            <span class="who"><strong>{{ paciente.firstName }} {{ paciente.lastName }}</strong><small>DNI {{ paciente.documentNumber }} · {{ paciente.healthInsurance || 'Particular' }}</small></span>
            <span class="visit">{{ paciente.lastVisitUtc ? 'Última visita: ' + fecha(paciente.lastVisitUtc) : 'Sin visitas' }}</span>
            <span class="go" aria-hidden="true">›</span>
          </a>
        } @empty {
          <p class="empty">{{ cargando ? 'Buscando…' : busqueda ? 'No se encontraron pacientes con esa búsqueda.' : 'Todavía no hay pacientes registrados.' }}</p>
        }
      </section>
      @if (total > pacientes.length) { <p class="more">Mostrando {{ pacientes.length }} de {{ total }} pacientes. Escribí para acotar la búsqueda.</p> }
    </main>
  `,
  styles: [`
    :host { display: block; color: #243746; }
    .picker { max-width: 860px; margin: 0 auto; padding: 36px 24px; }
    h1 { margin: 0 0 6px; font-size: 26px; }
    header p { margin: 0 0 22px; color: #6f8188; font-size: 14px; }
    .search { display: flex; align-items: center; gap: 10px; height: 48px; padding: 0 14px; border: 1px solid #cbd7da; border-radius: 10px; background: #fff; color: #6f8188; }
    .search:focus-within { border-color: #105c6c; box-shadow: 0 0 0 3px #d6eaee; }
    .search input { flex: 1; border: 0; outline: 0; font: inherit; background: transparent; min-width: 0; }
    .results { margin-top: 18px; background: #fff; border: 1px solid #dce5e7; border-radius: 12px; overflow: hidden; }
    .result { display: flex; align-items: center; gap: 13px; padding: 13px 16px; border-bottom: 1px solid #edf1f2; color: inherit; text-decoration: none; }
    .result:last-child { border-bottom: 0; }
    .result:hover, .result:focus-visible { background: #f2f8f9; outline: 0; }
    .avatar { width: 38px; height: 38px; flex: 0 0 38px; display: grid; place-items: center; border-radius: 50%; background: #d6eaee; color: #105c6c; font-size: 12px; font-weight: 700; }
    .who { flex: 1; min-width: 0; }
    .who strong, .who small { display: block; }
    .who small, .visit { color: #6f8188; font-size: 12px; margin-top: 3px; }
    .go { color: #105c6c; font-size: 22px; }
    .empty, .more { margin: 0; padding: 22px 16px; color: #6f8188; font-size: 13px; text-align: center; }
    .error { background: #fdecec; color: #a12626; border-radius: 8px; padding: 10px 13px; font-size: 13px; }
    @media (max-width: 600px) { .visit { display: none; } .picker { padding: 22px 14px; } }
  `]
})
export class ElegirPacienteComponent implements OnDestroy {
  private readonly api = inject(OdontiaApi);
  private readonly datos = inject(ActivatedRoute).snapshot.data;
  private readonly busquedas = new Subject<void>();
  private readonly suscripcion: Subscription;

  readonly titulo: string = this.datos['titulo'];
  readonly destino: string = this.datos['destino'];
  busqueda = '';
  pacientes: PatientListItemDto[] = [];
  total = 0;
  cargando = true;
  error = '';

  constructor() {
    this.suscripcion = this.busquedas.pipe(
      startWith(undefined),
      debounceTime(200),
      switchMap(() => { this.cargando = true; return this.api.pacientes({ Search: this.busqueda.trim(), pageSize: 20 }); })
    ).subscribe({
      next: pagina => { this.cargando = false; this.error = ''; this.pacientes = pagina.items; this.total = pagina.totalCount; },
      error: error => { this.cargando = false; this.error = mensajeDeError(error, 'No se pudo buscar pacientes.'); }
    });
  }

  ngOnDestroy(): void { this.suscripcion.unsubscribe(); }
  buscar(): void { this.busquedas.next(); }
  iniciales(paciente: PatientListItemDto): string { return iniciales(`${paciente.firstName} ${paciente.lastName}`); }
  fecha(valor: string): string { return fechaCorta(valor); }
}
