import { ComponentFixture, TestBed } from '@angular/core/testing';

import { WebGazerCalibrationInstructionsStepComponent } from './web-gazer-calibration-instructions-step.component';

describe('WebGazerCalibrationInstructionsStepComponent', () => {
  let component: WebGazerCalibrationInstructionsStepComponent;
  let fixture: ComponentFixture<WebGazerCalibrationInstructionsStepComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WebGazerCalibrationInstructionsStepComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(WebGazerCalibrationInstructionsStepComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
