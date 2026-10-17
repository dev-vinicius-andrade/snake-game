import { IVariation } from "@locales/types";
export const messages = {
  variations: {
    nickname: {
      text: "nickname | nicknames",
      Text: "Nickname | Nicknames",
      PascalCased: "Nickname | Nicknames",
      textCammelCased: "nickname | nicknames",
      count: "no nickname | nickname | nicknames({count})",
      Count: "No nickname | Nickname | Nicknames({count})",
      CountPascalCased: "No Nickname | Nickname | Nicknames({count})",
    } as IVariation,
  },
  buttons: {
    cancel: "Cancel",
    save: "Save",
    delete: "Delete",
    edit: "Edit",
    add: "Add",
    close: "Close",
    yes: "Yes",
    no: "No",
    ok: "Ok",
    confirm: "Confirm",
    join: "Join",
    joinRandomRoom: "Join Random Room",
  },
  game: {
    score: "Score",
    leaderboard: "Top players",
    joining: "Finding a game...",
    joinFailed: "Could not join a game",
    reconnecting: "Reconnecting...",
    disconnected: "Disconnected",
    died: "You died",
    finalScore: "Final score: {score}",
    playAgain: "Play again",
    retry: "Try again",
    back: "Back",
    controls: {
      move: "Move: arrows or WASD",
      shoot: "Shoot: Space",
      boost: "Boost: hold Shift",
    },
  },
};
export default messages;
