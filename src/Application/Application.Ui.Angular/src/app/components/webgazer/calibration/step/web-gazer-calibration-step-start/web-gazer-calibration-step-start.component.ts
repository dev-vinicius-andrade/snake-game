import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
@Component({
  selector: 'app-web-gazer-calibration-step-start',
  standalone: true,
  imports: [ButtonModule],
  templateUrl: './web-gazer-calibration-step-start.component.html',
  styleUrl: './web-gazer-calibration-step-start.component.scss',
})
export class WebGazerCalibrationStepStartComponent {
  @Output() onNext = new EventEmitter<void>();
  protected onOkClicked() {
    this.onNext.emit();
  }
}
