declare module 'webgazer' {
  export type WebGazerScreenPosition = { x: number; y: number };
  export type WebGazerGazeData = WebGazerScreenPosition;
  export type WebGazerParamTrackEyeLeft = 'left';
  export type WebGazerParamTrackEyeRight = 'right';
  export type WebGazerParamTrackEyeBoth = 'both';
  export type WebGazerParamTrackEye =
    | WebGazerParamTrackEyeLeft
    | WebGazerParamTrackEyeRight
    | WebGazerParamTrackEyeBoth;
  export type WebGazerParamsCamConstraints = {
    video: WebGazerParamsCamConstraintsVideo;
  };
  export type WebGazerParamsCamConstraintsVideoSize = {
    min: number;
    ideal: number;
    max: number;
  };
  export type WebGazerParamsCamConstraintsVideo = {
    width: WebGazerParamsCamConstraintsVideoSize;
    height: WebGazerParamsCamConstraintsVideoSize;
  };
  export type WebGazerParamGetEventTypes = () => string[];
  export type WebGazerParams = {
    applyKalmanFilter: boolean;
    camConstraints: WebGazerParamsCamConstraints;
    dataTimestep: number;
    faceFeedbackBoxId: string;
    faceFeedbackBoxRatio: number;
    faceOverlayId: string;
    gazeDotId: string;
    getEventTypes: WebGazerParamGetEventTypes;
    mirrorVideo: boolean;
    moveTickSize: number;
    saveDataAcrossSessions: boolean;
    showFaceFeedbackBox: boolean;
    showFaceOverlay: boolean;
    showGazeDot: boolean;
    showVideo: boolean;
    showVideoPreview: boolean;
    storingPoints: number;
    trackEye: WebGazerParamTrackEye;
    videoContainerId: string;
    videoElementCanvasId: string;
    videoElementId: string;
    videoViewerHeight: number;
    videoViewerWidth: number;
  };
  export type WebGazerUtilGetEyeFeats = (eye: any) => any;
  export type WebGazerUtilResizeEye = (
    eye: any,
    resizeWidth: number,
    resizeHeight: number
  ) => ImageData;
  export type WebGazerUtilGrayscale = (
    data: Uint8ClampedArray,
    width: number,
    height: number
  ) => any;
  export type WebGazerUtilEqualizeHistogram = (
    src: any,
    step: any,
    dst: any
  ) => any;
  export type WebGazerUtilBound = (data: { x: number; y: number }) => {
    x: number;
    y: number;
  };
  export type WebGazerUtil = {
    getEyeFeats: WebGazerUtilGetEyeFeats;
    resizeEye: WebGazerUtilResizeEye;
    grayscale: WebGazerUtilGrayscale;
    equalizeHistogram: WebGazerUtilEqualizeHistogram;
    bound: WebGazerUtilBound;
  };
  export type WebGazerRegressionRidge = 'ridge';
  export type WebGazerRegressionWeightedRidge = 'weightedRidge';
  export type WebGazerRegressionThreadedRidge = 'threadedRidge';
  export declare type WebGazerRegression =
    | WebGazerRegressionRidge
    | WebGazerRegressionWeightedRidge
    | WebGazerRegressionThreadedRidge;
  export type WebGazerRegressionSetter = (type: WebGazerRegression) => WebGazer;
  export type WebGazerGazeListenerSetter = (
    callback: (data: GazeData | null, elapsedTime: number) => void
  ) => WebGazer;
  export type WebGazerShowVideoSetter = (show: boolean) => WebGazer;
  export type WebGazerShowPredictionPointsSetter = (show: boolean) => WebGazer;
  export type WebGazerShowFaceOverlaySetter = (show: boolean) => WebGazer;
  export type WebGazerVideoElementCanvasSetter = (
    canvas: HTMLCanvasElement
  ) => HTMLVideoElement;
  export type WebGazerSetters = {
    setRegression: WebGazerRegressionSetter;
    setGazeListener: WebGazerGazeListenerSetter;
    showVideo: WebGazerShowVideoSetter;
    showPredictionPoints: WebGazerShowPredictionPointsSetter;
    showFaceOverlay: WebGazerShowFaceOverlaySetter;
    setVideoElementCanvas: WebGazerVideoElementCanvasSetter;
  };
  export type WebGazer = WebGazerSetters & {
    webgazer: WebGazer;
    begin(): Promise<WebGazer>;
    end(): Promise<WebGazer>;
    pause(): WebGazer;
    resume(): WebGazer;
    resetCalibration(): WebGazer;
    isReady(): boolean;
    saveDataAcrossSessions(value: boolean): WebGazer;
    clearData(): void;
    params: WebGazerParams;
    util: WebGazerUtil;
    getVideoContainer(): HTMLDivElement;
    getVideoCanvas(): HTMLCanvasElement;
    getFaceOverlay(): HTMLCanvasElement;
    getFaceFeedbackBox(): HTMLCanvasElement;

    getCurrentPrediction(index?: number): Promise<WebGazerGazeData>;
    getVideoElement(): HTMLVideoElement;
  };

  const webgazer: WebGazer;
  export default webgazer;
  export = webgazer;
}
