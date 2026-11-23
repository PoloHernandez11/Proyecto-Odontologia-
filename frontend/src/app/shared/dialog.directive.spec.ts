import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DialogDirective } from './dialog.directive';

@Component({
  imports: [DialogDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button id="abrir" type="button" (click)="abierto.set(true)">Abrir</button>
    <a id="fondo" href="#">Enlace del fondo</a>
    @if (abierto()) {
      <section role="dialog" aria-modal="true" appDialog (cerrar)="cerrado = cerrado + 1; abierto.set(false)">
        <button id="cerrar" type="button">×</button>
        <input id="primero" />
        <select id="medio"><option>A</option></select>
        <button id="ultimo" type="button">Guardar</button>
      </section>
    }
  `
})
class AnfitrionComponent {
  readonly abierto = signal(false);
  cerrado = 0;
}

function tecla(elemento: Element, key: string, shiftKey = false): KeyboardEvent {
  const evento = new KeyboardEvent('keydown', { key, shiftKey, bubbles: true, cancelable: true });
  elemento.dispatchEvent(evento);
  return evento;
}

describe('DialogDirective', () => {
  let fixture: ComponentFixture<AnfitrionComponent>;
  let raiz: HTMLElement;
  const $ = (selector: string) => raiz.querySelector<HTMLElement>(selector)!;

  beforeEach(async () => {
    fixture = TestBed.createComponent(AnfitrionComponent);
    fixture.autoDetectChanges();
    raiz = fixture.nativeElement;
    document.body.appendChild(raiz);
    await fixture.whenStable();
    $('#abrir').focus();
    $('#abrir').click();
    await fixture.whenStable();
  });

  afterEach(() => raiz.remove());

  it('al abrir enfoca el primer campo del formulario', () => {
    expect(document.activeElement).toBe($('#primero'));
  });

  it('Tab desde el último elemento vuelve al primero y Shift+Tab desde el primero va al último', () => {
    $('#ultimo').focus();
    const adelante = tecla($('#ultimo'), 'Tab');
    expect(adelante.defaultPrevented).toBeTrue();
    expect(document.activeElement).toBe($('#cerrar'));

    const atras = tecla($('#cerrar'), 'Tab', true);
    expect(atras.defaultPrevented).toBeTrue();
    expect(document.activeElement).toBe($('#ultimo'));
  });

  it('Tab en medio del diálogo sigue el orden normal', () => {
    $('#primero').focus();
    expect(tecla($('#primero'), 'Tab').defaultPrevented).toBeFalse();
  });

  it('si el foco se escapa al fondo, vuelve al diálogo', () => {
    $('#fondo').focus();
    expect(raiz.querySelector('[role=dialog]')!.contains(document.activeElement)).toBeTrue();
  });

  it('Escape emite (cerrar) y al cerrarse el foco vuelve al botón que lo abrió', async () => {
    tecla($('#medio'), 'Escape');
    await fixture.whenStable();
    expect(fixture.componentInstance.cerrado).toBe(1);
    expect(raiz.querySelector('[role=dialog]')).toBeNull();
    expect(document.activeElement).toBe($('#abrir'));
  });
});
