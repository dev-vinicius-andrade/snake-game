import { ComponentFixture, TestBed } from '@angular/core/testing';

import { WebGazerCalibrationAccuracyStepComponent } from './web-gazer-calibration-accuracy-step.component';

describe('WebGazerCalibrationAccuracyStepComponent', () => {
  let component: WebGazerCalibrationAccuracyStepComponent;
  let fixture: ComponentFixture<WebGazerCalibrationAccuracyStepComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WebGazerCalibrationAccuracyStepComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(WebGazerCalibrationAccuracyStepComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
