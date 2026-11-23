import { DOCUMENT } from '@angular/common';
import { Directive, ElementRef, OnDestroy, afterNextRender, inject, output } from '@angular/core';

const ENFOCABLES = [
  'a[href]', 'area[href]', 'button:not([disabled])', 'input:not([disabled]):not([type=hidden])', 'select:not([disabled])',
  'textarea:not([disabled])', 'iframe', '[contenteditable=""]', '[contenteditable="true"]', '[tabindex]:not([tabindex="-1"])'
].join(',');
const CAMPOS = 'input:not([disabled]):not([type=hidden]), select:not([disabled]), textarea:not([disabled])';

/**
 * Comportamiento accesible de un diálogo modal (role="dialog" aria-modal="true"):
 * al abrir enfoca el primer campo, Tab y Shift+Tab quedan dentro del diálogo, Escape emite (cerrar)
 * y al cerrarse (cuando el @if lo saca del DOM) devuelve el foco al elemento que lo abrió.
 */
@Directive({
  selector: '[appDialog]',
  host: { '(keydown)': 'alPresionarTecla($event)' }
})
export class DialogDirective implements OnDestroy {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly documento = inject(DOCUMENT);
  private readonly origen = this.documento.activeElement as HTMLElement | null;
  private readonly alEnfocarFuera = (evento: FocusEvent) => {
    const destino = evento.target as Node | null;
    if (destino && !this.host.contains(destino)) this.enfocarInicio();
  };

  /** Se emite con Escape. El componente decide cómo cerrar (normalmente, el mismo código que el botón ×). */
  readonly cerrar = output<void>();

  constructor() {
    afterNextRender(() => {
      this.enfocarInicio();
      this.documento.addEventListener('focusin', this.alEnfocarFuera);
    });
  }

  ngOnDestroy(): void {
    this.documento.removeEventListener('focusin', this.alEnfocarFuera);
    // El opener puede haber desaparecido (p. ej. la fila se recargó): en ese caso no se fuerza el foco.
    if (this.origen?.isConnected && typeof this.origen.focus === 'function') this.origen.focus();
  }

  alPresionarTecla(evento: KeyboardEvent): void {
    if (evento.key === 'Escape') {
      evento.preventDefault();
      evento.stopPropagation();
      this.cerrar.emit();
      return;
    }
    if (evento.key !== 'Tab') return;
    const enfocables = this.enfocables();
    if (!enfocables.length) { evento.preventDefault(); this.host.focus(); return; }
    const primero = enfocables[0], ultimo = enfocables[enfocables.length - 1];
    const activo = this.documento.activeElement;
    if (evento.shiftKey && (activo === primero || activo === this.host)) { evento.preventDefault(); ultimo.focus(); }
    else if (!evento.shiftKey && activo === ultimo) { evento.preventDefault(); primero.focus(); }
  }

  private enfocables(): HTMLElement[] {
    return Array.from(this.host.querySelectorAll<HTMLElement>(ENFOCABLES))
      .filter(elemento => elemento.tabIndex >= 0 && !elemento.closest('[inert]') && elemento.getClientRects().length > 0);
  }

  /** Primer campo del formulario; si no hay, el primer elemento enfocable; si tampoco, el propio diálogo. */
  private enfocarInicio(): void {
    const campo = Array.from(this.host.querySelectorAll<HTMLElement>(CAMPOS)).find(elemento => elemento.getClientRects().length > 0);
    const destino = campo ?? this.enfocables()[0];
    if (destino) { destino.focus(); return; }
    if (!this.host.hasAttribute('tabindex')) this.host.setAttribute('tabindex', '-1');
    this.host.focus();
  }
}
