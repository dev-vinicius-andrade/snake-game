export enum Theme {
  Dark = 'dark',
  Light = 'light',
}

export enum Direction {
  Up = 'up',
  Down = 'down',
  Left = 'left',
  Right = 'right',
  None = 'none',
}
export enum LocalStorageKey {
  playerTrackableId = 'playerTrackableId',
  theme = 'theme',
  eyeTrackerControlEnabled = 'eyeTrackerControlEnabled',
  isControlDirectionEyeCalibrated = 'isControlDirectionEyeCalibrated',
  webgazerCalibrationStep = 'webgazerCalibrationStep',
}
export enum WebGazerCalibrationStep {
  Start = 'start',
  Camera = 'camera',
  Calibration = 'calibration',
  Validation = 'validation',
  Complete = 'complete',
  CalibrationInstructions = 'CalibrationInstructions',
}
