import { Direction } from '@app/types/enums';
import { WebGazer, WebGazerParams } from 'webgazer';
export async function useWebgazer(
  params: Partial<WebGazerParams> = {}
): Promise<WebGazer> {
  const webgazer = (await import('webgazer')).default;
  const resizeWidth = 10;
  const resizeHeight = 6;
  webgazer.util.getEyeFeats = (eyes: any) => {
    const process = (eye: any) => {
      const resized = webgazer.util.resizeEye(eye, resizeWidth, resizeHeight);
      if (!resized) return;
      const gray = webgazer.util.grayscale(
        resized.data,
        resized.width,
        resized.height
      );
      let hist: any = [];
      webgazer.util.equalizeHistogram(gray, 5, hist);
      return hist;
    };

    if (webgazer.params.trackEye == 'left') {
      return process(eyes.left);
    } else if (webgazer.params.trackEye == 'right') {
      return process(eyes.right);
    } else {
      return [].concat(process(eyes.left), process(eyes.right));
    }
  };
  webgazer.params = {
    ...webgazer.params,
    ...params,
  };
  webgazer.getVideoContainer = () => {
    return document.getElementById(
      webgazer.params.videoContainerId
    ) as HTMLDivElement;
  };
  webgazer.getVideoElement = () => {
    return document.getElementById(
      webgazer.params.videoElementId
    ) as HTMLVideoElement;
  };
  webgazer.getVideoContainer = () => {
    return document.getElementById(
      webgazer.params.videoContainerId
    ) as HTMLDivElement;
  };
  webgazer.getVideoCanvas = () => {
    return document.getElementById(
      webgazer.params.videoElementCanvasId
    ) as HTMLCanvasElement;
  };
  webgazer.getFaceOverlay = () => {
    return document.getElementById(
      webgazer.params.faceOverlayId
    ) as HTMLCanvasElement;
  };
  webgazer.getFaceFeedbackBox = () => {
    return document.getElementById(
      webgazer.params.faceFeedbackBoxId
    ) as HTMLCanvasElement;
  };
  return webgazer;
}
interface ScreenLookPosition {
  x: number;
  y: number;
}
type ScreenLookPositionResult = {
  calculateEyePosition: (currentPosition: ScreenLookPosition) => Direction;
};
export function useScreenLookDirection(
  x: number,
  y: number,
  movementThresholdPixels: number = 10
): ScreenLookPositionResult {
  // Adjusting weights and dead zone handling
  const horizontalWeight = 1;
  const verticalWeight = 1.5; // Reduced to lessen the bias towards vertical movements
  const deadZoneThresholdX = movementThresholdPixels * 1.5; // Increase the dead zone slightly
  const deadZoneThresholdY = movementThresholdPixels * 1.5; // Adjust based on vertical weight

  let lastPosition: Partial<ScreenLookPosition> = {
    x: undefined,
    y: undefined,
  };

  const isNullOrUndefined = (value?: any) =>
    value === null || value === undefined;

  const calculateEyePosition = (currentPosition: ScreenLookPosition) => {
    if (
      isNullOrUndefined(lastPosition.x) ||
      isNullOrUndefined(lastPosition.y)
    ) {
      lastPosition = { ...currentPosition };
      return Direction.None; // Initial state handling
    }

    const diffX = (currentPosition.x - lastPosition.x!) * horizontalWeight;
    const diffY = (currentPosition.y - lastPosition.y!) * verticalWeight;
    if (
      Math.abs(diffX) <= deadZoneThresholdX &&
      Math.abs(diffY) <= deadZoneThresholdY
    ) {
      return Direction.None;
    }

    let direction: Direction = Direction.None;

    if (
      Math.abs(diffX) > deadZoneThresholdX ||
      Math.abs(diffY) > deadZoneThresholdY
    ) {
      if (Math.abs(diffX) > Math.abs(diffY)) {
        direction = diffX > 0 ? Direction.Right : Direction.Left;
      } else {
        direction = diffY > 0 ? Direction.Down : Direction.Up;
      }
    }

    lastPosition = { ...currentPosition };

    return direction;
  };

  return { calculateEyePosition };
}
