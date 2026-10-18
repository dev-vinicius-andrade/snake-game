import { ComponentFixture, TestBed } from '@angular/core/testing';

import { WebgazerCalibrationStepCameraComponent } from './webgazer-calibration-step-camera.component';

describe('WebgazerCalibrationStepCameraComponent', () => {
  let component: WebgazerCalibrationStepCameraComponent;
  let fixture: ComponentFixture<WebgazerCalibrationStepCameraComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WebgazerCalibrationStepCameraComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(WebgazerCalibrationStepCameraComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
