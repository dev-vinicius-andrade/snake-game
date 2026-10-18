import { ComponentFixture, TestBed } from '@angular/core/testing';

import { WebGazerCalibrationStepComponent } from './web-gazer-calibration-step.component';

describe('WebGazerCalibrationStepComponent', () => {
  let component: WebGazerCalibrationStepComponent;
  let fixture: ComponentFixture<WebGazerCalibrationStepComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WebGazerCalibrationStepComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(WebGazerCalibrationStepComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
