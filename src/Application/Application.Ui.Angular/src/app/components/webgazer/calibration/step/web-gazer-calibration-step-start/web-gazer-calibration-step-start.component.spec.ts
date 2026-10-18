import { ComponentFixture, TestBed } from '@angular/core/testing';

import { WebGazerCalibrationStepStartComponent } from './web-gazer-calibration-step-start.component';

describe('WebGazerCalibrationStepStartComponent', () => {
  let component: WebGazerCalibrationStepStartComponent;
  let fixture: ComponentFixture<WebGazerCalibrationStepStartComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WebGazerCalibrationStepStartComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(WebGazerCalibrationStepStartComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
