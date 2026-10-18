import { Component } from '@angular/core';
import { WebgazerCalibrationComponent } from '@app/components/webgazer/calibration/webgazer-calibration.component';
import { LocalStorageService } from '@app/services/localStorage/local-storage.service';
import { LocalStorageKey } from '@app/types/enums';

@Component({
  selector: 'app-control-direction-eye-calibration',
  standalone: true,
  imports: [WebgazerCalibrationComponent],
  templateUrl: './control-direction-eye-calibration.component.html',
  styleUrl: './control-direction-eye-calibration.component.scss',
})
export class ControlDirectionEyeCalibrationComponent {
  localStorageService: LocalStorageService;

  constructor(localStorageService: LocalStorageService) {
    this.localStorageService = localStorageService;
  }
  protected get isCalibrated(): boolean {
    return (
      this.localStorageService.get(
        LocalStorageKey.isControlDirectionEyeCalibrated
      ) === 'true'
    );
  }
}
