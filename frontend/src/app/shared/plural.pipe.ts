import { Pipe, PipeTransform } from '@angular/core';
import { cantidad } from '../core/api';

/** {{ total | plural:'paciente' }} → "1 paciente" / "3 pacientes". El plural irregular va como segundo argumento. */
@Pipe({ name: 'plural' })
export class PluralPipe implements PipeTransform {
  transform(numero: number | null | undefined, singular: string, plural?: string): string {
    return cantidad(numero ?? 0, singular, plural);
  }
}
