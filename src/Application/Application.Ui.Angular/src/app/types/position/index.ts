import { Color } from '@app/types/color';

export declare type Position = {
  x: number;
  y: number;
};
export declare type PositionWithColor = Position & {
  color: Color;
};
