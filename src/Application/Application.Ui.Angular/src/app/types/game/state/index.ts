import { Color } from '@app/types/color';
import { Food } from '@app/types/food';
import { Player } from '@app/types/player';
import { Position } from '@app/types/position';
import { Room } from '@app/types/room';

export interface GameState {
  room: Room;
  players: { [key: string]: Player };
  colors: Color[];
  foods: { [key: string]: Food };
  obstacles: {
    [key: string]: {
      currentPosition: Position;
      size: number;
    };
  };
}
