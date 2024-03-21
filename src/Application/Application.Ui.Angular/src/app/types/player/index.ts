import { Axis } from '@app/types/axis';
import { Color } from '@app/types/color';
import { Direction } from '@app/types/direction';

export interface Player {
  name: string;
  currentPosition: Axis;
  currentDirection: any;
  direction: Direction;
  backgroundColor: Color;
  borderColor: any;
  path: Axis[];
  isAlive: boolean;
  guid: string;
  id: string;
}
