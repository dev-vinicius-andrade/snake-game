import { Component, EventEmitter, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-web-gazer-calibration-instructions-step',
  standalone: true,
  imports: [ButtonModule],
  templateUrl: './web-gazer-calibration-instructions-step.component.html',
  styleUrl: './web-gazer-calibration-instructions-step.component.scss',
})
export class WebGazerCalibrationInstructionsStepComponent {
  @Output() onNext = new EventEmitter<void>();
  onOkClicked() {
    this.onNext.emit();
  }
}
