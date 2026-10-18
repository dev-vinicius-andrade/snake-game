import { Nullable } from '@app/types/nullable';

export function isStringNullishOrEmpty(value?: Nullable<string>): boolean {
  return value === null || value === undefined || value === '';
}
