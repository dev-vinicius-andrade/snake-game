import { Nullable } from '@app/types/nullable';
import { Injectable } from '@angular/core';
import { Direction } from '@app/types/enums';
import { Position } from '@app/types/position';

@Injectable({
  providedIn: 'root',
})
export class KeyboardEventHandlerService {
  constructor() {}
  buildDiretion(event: Nullable<KeyboardEvent>): Nullable<Direction> {
    if (!event) return null;

    switch (event.key) {
      case 'ArrowUp':
      case 'w':
        return Direction.Up;
      case 'ArrowDown':
      case 's':
        return Direction.Down;
      case 'ArrowLeft':
      case 'a':
        return Direction.Left;
      case 'ArrowRight':
      case 'd':
        return Direction.Right;
      default:
        return null;
    }
  }
}
