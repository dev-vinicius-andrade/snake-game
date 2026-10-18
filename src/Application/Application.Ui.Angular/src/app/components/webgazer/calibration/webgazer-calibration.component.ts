import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { WebGazerCalibrationAccuracyStepComponent } from '@app/components/webgazer/calibration/step/web-gazer-calibration-accuracy-step/web-gazer-calibration-accuracy-step.component';
import { WebGazerCalibrationInstructionsStepComponent } from '@app/components/webgazer/calibration/step/web-gazer-calibration-instructions-step/web-gazer-calibration-instructions-step.component';
import { WebGazerCalibrationStepStartComponent } from '@app/components/webgazer/calibration/step/web-gazer-calibration-step-start/web-gazer-calibration-step-start.component';
import { WebGazerCalibrationStepComponent } from '@app/components/webgazer/calibration/step/web-gazer-calibration-step/web-gazer-calibration-step.component';
import { WebgazerCalibrationStepCameraComponent } from '@app/components/webgazer/calibration/step/webgazer-calibration-step-camera/webgazer-calibration-step-camera.component';
import { LocalStorageService } from '@app/services/localStorage/local-storage.service';
import { LocalStorageKey, WebGazerCalibrationStep } from '@app/types/enums';
import { DialogModule } from 'primeng/dialog';
@Component({
  selector: 'app-webgazer-calibration',
  standalone: true,
  imports: [
    WebGazerCalibrationStepStartComponent,
    WebgazerCalibrationStepCameraComponent,
    WebGazerCalibrationInstructionsStepComponent,
    WebGazerCalibrationAccuracyStepComponent,
    WebGazerCalibrationStepComponent,
    DialogModule,
  ],
  templateUrl: './webgazer-calibration.component.html',
  styleUrl: './webgazer-calibration.component.scss',
})
export class WebgazerCalibrationComponent {
  private router: Router;
  protected WebGazerCalibrationStep = WebGazerCalibrationStep;

  get currentStep(): WebGazerCalibrationStep {
    const persistedStep = this.localStorageService.get(
      LocalStorageKey.webgazerCalibrationStep
    );
    if (!persistedStep) {
      this.currentStep = WebGazerCalibrationStep.Start;
      return WebGazerCalibrationStep.Start;
    }
    return persistedStep as WebGazerCalibrationStep;
  }
  set currentStep(step: WebGazerCalibrationStep) {
    this.localStorageService.set(
      LocalStorageKey.webgazerCalibrationStep,
      step.toString()
    );
  }
  localStorageService: LocalStorageService;
  constructor(router: Router, localStorageService: LocalStorageService) {
    this.router = router;
    this.localStorageService = localStorageService;
  }
  setStep(step: WebGazerCalibrationStep) {
    this.currentStep = step;
  }
  goToHomePage() {
    this.router.navigate(['/']);
  }
  isStep(step: WebGazerCalibrationStep): boolean {
    return this.currentStep === step;
  }
}
