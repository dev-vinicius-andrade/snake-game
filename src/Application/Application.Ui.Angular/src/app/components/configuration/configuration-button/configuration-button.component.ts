import { Component } from '@angular/core';
import { ThemePalette } from '@angular/material/core';
import { ContextMenuModule } from 'primeng/contextmenu';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatIcon } from '@angular/material/icon';
import { ButtonModule } from 'primeng/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { Router } from '@angular/router';
import { LocalStorageService } from '@app/services/localStorage/local-storage.service';
import { LocalStorageKey } from '@app/types/enums';
@Component({
  selector: 'app-configuration-button',
  standalone: true,
  imports: [
    ContextMenuModule,
    MatSlideToggleModule,
    MatIcon,
    ButtonModule,
    MatMenuModule,
    MatButtonModule,
  ],
  templateUrl: './configuration-button.component.html',
  styleUrl: './configuration-button.component.scss',
})
export class ConfigurationButtonComponent {
  color: ThemePalette;
  eyeTrackerControlEnabled = false;
  private _eyeTrackerControlLabel: string;
  router: Router;
  private localStorageService: LocalStorageService;
  constructor(router: Router, localStorageService: LocalStorageService) {
    this.router = router;
    this._eyeTrackerControlLabel = this.getEyeTrackerControlLabel();
    this.localStorageService = localStorageService;
    this.eyeTrackerControlEnabled =
      this.localStorageService.get(LocalStorageKey.eyeTrackerControlEnabled) ===
      'true';
  }
  protected get eyeTrackerControlLabel(): string {
    return this._eyeTrackerControlLabel;
  }
  toogleEyeTrackerControl() {
    this.eyeTrackerControlEnabled = !this.eyeTrackerControlEnabled;
    this._eyeTrackerControlLabel = this.getEyeTrackerControlLabel();
    this.localStorageService.set(
      LocalStorageKey.eyeTrackerControlEnabled,
      this.eyeTrackerControlEnabled.toString()
    );
  }
  private getEyeTrackerControlLabel(): string {
    return this.eyeTrackerControlEnabled
      ? 'Disable Eye Tracker'
      : 'Enable Eye Tracker';
  }
  navigateToEyeControlCalibrationPage(event: MouseEvent) {
    this.router.navigate(['/control/direction/eye/calibration']);
  }
}
