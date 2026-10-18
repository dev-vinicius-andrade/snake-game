import {
  AfterContentInit,
  AfterViewChecked,
  AfterViewInit,
  Component,
  ElementRef,
  EventEmitter,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { WebgazerComponent } from '@app/components/webgazer/webgazer.component';
import { useWebgazer } from '@app/composables/webgazer';
import { WebGazer, WebGazerParams } from 'webgazer';
import { ButtonModule } from 'primeng/button';
import { WebGazerCalibrationInstructionsStepComponent } from '@app/components/webgazer/calibration/step/web-gazer-calibration-instructions-step/web-gazer-calibration-instructions-step.component';
@Component({
  selector: 'app-webgazer-calibration-step-camera',
  standalone: true,
  imports: [
    WebgazerComponent,
    ButtonModule,
    WebGazerCalibrationInstructionsStepComponent,
  ],
  templateUrl: './webgazer-calibration-step-camera.component.html',
  styleUrl: './webgazer-calibration-step-camera.component.scss',
})
export class WebgazerCalibrationStepCameraComponent
  implements OnInit, AfterViewChecked
{
  protected get listClass(): string {
    if (this.isFaceDetected)
      return 'list-image-[url(assets/images/check.svg)] fill-green-600 space-y-1';
    return 'list-image-[url(assets/images/uncheck.svg)] fill-red-600 space-y-1';
  }
  ngAfterViewChecked(): void {
    this.onCameraReady.emit(this.loading);
  }
  ngAfterContentInit(): void {
    this.onCameraReady.emit(this.loading);
  }
  webgazer?: WebGazer;
  loading = true;
  isFaceDetected = true;
  protected begin = new EventEmitter<void>();
  async ngOnInit(): Promise<void> {}
  @Output() onNext = new EventEmitter<void>();
  @Output() onCameraReady = new EventEmitter<boolean>();
  protected onOkClicked() {
    this.onNext.emit();
  }
  async onWebgazerInitialized(webgazer: WebGazer) {
    this.loading = false;
    this.onCameraReady.emit(this.loading);
  }
  onFaceDetectionChange($event: boolean) {
    this.isFaceDetected = $event;
  }
  setupStyles(webgazer: WebGazer) {}
}
