import {
  AfterViewInit,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnDestroy,
  OnInit,
  Output,
  viewChild,
  ViewChild,
} from '@angular/core';
import { isStringNullishOrEmpty } from '@app/composables/utils';
import { useWebgazer } from '@app/composables/webgazer';
import { Nullable } from '@app/types/nullable';
import { WebGazer, WebGazerGazeData } from 'webgazer';

@Component({
  selector: 'app-webgazer',
  standalone: true,
  imports: [],
  templateUrl: './webgazer.component.html',
  styleUrl: './webgazer.component.scss',
})
export class WebgazerComponent implements AfterViewInit, OnDestroy {
  @ViewChild('videoContainerWrapper', { static: true })
  videoContainerWrapper?: ElementRef<HTMLDivElement>;
  @ViewChild('loadingCameraContainer', { static: true })
  loadingCameraContainer?: ElementRef<HTMLSpanElement>;
  @Input() showGazeDot = false;
  @Input() showVideo = false;
  @Input() showFaceOverlay = false;
  @Input() showFaceFeedbackBox = false;
  @Input() runPredictionInterval = false;
  @Input() class?: Nullable<string> = null;
  @Input() videoElementClass?: Nullable<string> = null;
  @Input() videoContainerClass?: Nullable<string> = null;
  @Input() faceOverlayClass?: Nullable<string> = null;
  @Input() faceFeedbackBoxClass?: Nullable<string> = null;
  @Input() faceFeedbackBoxBorderNotDetectedColor: string = 'red';
  @Input() faceFeedbackBoxBorderDetectedColor: string = 'green';

  @Input() predictionInterval = 100;
  @Output() onPrediction = new EventEmitter<WebGazerGazeData>();
  @Output() onInitialized: EventEmitter<WebGazer> =
    new EventEmitter<WebGazer>();

  private predictionIntervalTimeout: Nullable<NodeJS.Timeout>;
  private webgazerReadyInterval: Nullable<NodeJS.Timeout>;
  @Output() onFaceDetectionChange = new EventEmitter<boolean>();

  async ngOnInit(): Promise<void> {}
  async ngAfterViewInit(): Promise<void> {
    const webgazer = await useWebgazer({
      showVideo: false,
      showFaceOverlay: this.showFaceOverlay,
      showFaceFeedbackBox: this.showFaceFeedbackBox,
      showGazeDot: this.showGazeDot,
    });
    await webgazer.begin();
    //this.hideVideoContainerWrapper();
    this.setupReadyInterval(webgazer);
    this.setupCameraPreview(webgazer);
    this.setUpPredictionInterval(webgazer);
  }
  private hideVideoContainerWrapper() {
    if (!this.showVideo) return;

    this.videoContainerWrapper?.nativeElement.classList.add('!hidden');
  }
  private showVideoContainerWrapper() {
    if (!this.showVideo) return;
    this.videoContainerWrapper?.nativeElement.classList.remove('!hidden');
  }
  protected isVideoPreviewEnabled(): boolean {
    return this.showVideo;
  }
  setupStyles(webgazer: WebGazer) {
    const videoElement = webgazer.getVideoElement();
    const videoContainer = webgazer.getVideoContainer();
    const faceOverlay = webgazer.getFaceOverlay();
    const faceFeedbackBox = webgazer.getFaceFeedbackBox();
    const videoCanvas = webgazer.getVideoCanvas();
    const cssResetClassList = ['!m-0', '!p-0'];
    this.setVideoContainerStyles(videoContainer, cssResetClassList);
    this.setVideoElementStyles(videoElement, cssResetClassList);
    this.setFaceOverlayStyles(faceOverlay, cssResetClassList);
    this.setFaceFeedboxStyles(faceFeedbackBox, cssResetClassList);
    this.setupFaceFeedboxObserver(webgazer, faceFeedbackBox);
    this.setVideoCanvasStyles(videoCanvas, cssResetClassList);
  }
  private setVideoCanvasStyles(
    videoCanvas: HTMLCanvasElement,
    cssResetClassList: string[]
  ) {
    videoCanvas?.classList.add(...cssResetClassList);
  }

  private setFaceFeedboxStyles(
    faceFeedbackBox: HTMLCanvasElement,
    cssResetClassList: string[]
  ) {
    faceFeedbackBox?.classList.add(
      ...cssResetClassList,
      '!absolute',
      '!top-1/4',
      '!left-1/3',
      '!w-1/3',
      '!h-2/4'
    );
    if (!isStringNullishOrEmpty(this.faceFeedbackBoxClass))
      faceFeedbackBox.className += ` ${this.faceFeedbackBoxClass}`;
  }

  private setFaceOverlayStyles(
    faceOverlay: HTMLCanvasElement,
    cssResetClassList: string[]
  ) {
    faceOverlay?.classList.add(
      ...cssResetClassList,
      '!absolute',
      '!top-0',
      '!left-0'
    );
    if (!isStringNullishOrEmpty(this.faceOverlayClass))
      faceOverlay.className += ` ${this.faceOverlayClass}`;
  }

  private setVideoElementStyles(
    videoElement: HTMLVideoElement,
    cssResetClassList: string[]
  ) {
    videoElement?.classList.add(
      ...cssResetClassList,
      '!relative',
      '!w-full',
      '!h-full'
    );
    if (!isStringNullishOrEmpty(this.videoElementClass))
      videoElement.className += ` ${this.videoElementClass}`;
  }

  private setVideoContainerStyles(
    videoContainer: HTMLDivElement,
    cssResetClassList: string[]
  ) {
    videoContainer?.classList.add(
      ...cssResetClassList,
      '!relative',
      '!w-full',
      '!h-full'
    );
    if (!isStringNullishOrEmpty(this.videoContainerClass))
      videoContainer.className += ` ${this.videoContainerClass}`;
  }

  setupFaceFeedboxObserver(webgazer: WebGazer, faceFeedbox: HTMLCanvasElement) {
    const observer = new MutationObserver((mutationList: MutationRecord[]) => {
      for (const mutation of mutationList) {
        if (mutation.type !== 'attributes') continue;
        if (mutation.attributeName !== 'style') continue;
        if (!faceFeedbox.style.border) continue;

        this.onFaceDetectionChange.emit(this.isFaceDetected(faceFeedbox));
      }
      if (this.showFaceOverlay) {
        faceFeedbox.classList.remove('!hidden');
      } else {
        faceFeedbox.classList.add('!hidden');
      }
    });
    observer.observe(faceFeedbox, {
      attributes: true,
      attributeFilter: ['style'],
    });
  }
  private isFaceDetected(faceFeedBox: HTMLCanvasElement): boolean | undefined {
    return (
      faceFeedBox.style.borderColor === this.faceFeedbackBoxBorderDetectedColor
    );
  }

  setupCameraPreview(webgazer: WebGazer) {
    if (this.showVideo) return;
    this.videoContainerWrapper?.nativeElement.classList.add('!hidden');
  }
  setupReadyInterval(webgazer: WebGazer) {
    this.webgazerReadyInterval = setInterval(() => {
      if (webgazer.isReady() && this.webgazerReadyInterval) {
        this.videoContainerWrapper?.nativeElement.appendChild(
          webgazer.getVideoContainer()!
        );
        this.loadingCameraContainer?.nativeElement.classList.add('!hidden');
        webgazer.showVideo(true);
        this.setupStyles(webgazer);
        this.showVideoContainerWrapper();
        clearInterval(this.webgazerReadyInterval);
        this.onInitialized?.emit(webgazer);
      }
    }, 1000);
  }
  setUpPredictionInterval(webgazer: WebGazer) {
    if (!this.runPredictionInterval) return;
    this.predictionIntervalTimeout = setInterval(async () => {
      const prediction = await webgazer.getCurrentPrediction();
      this.onPrediction.emit(prediction);
    }, this.predictionInterval);
  }
  ngOnDestroy(): void {
    if (this.predictionIntervalTimeout) {
      clearInterval(this.predictionIntervalTimeout);
    }
  }
  protected get webgazerDotClass(): string {
    //this.webgazerFaceFeedbackBox?.nativeElement.t;
    return [].join(' ');
  }
  protected get webgazerFaceFeedbackBoxClass(): string {
    return [].join(' ');
  }
  protected get webgazerFaceOverlayClass(): string {
    return [].join(' ');
  }
  protected get webgazerVideoClass(): string {
    return ['!shadow', '!rounded-lg', '!w-48', '!h-25'].join(' ');
  }
  protected get webgazerVideoContainerClass(): string {
    return ['!relative'].join(' ');
  }
}
