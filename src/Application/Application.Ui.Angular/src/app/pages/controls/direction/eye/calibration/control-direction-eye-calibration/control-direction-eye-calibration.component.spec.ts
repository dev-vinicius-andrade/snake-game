import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ControlDirectionEyeCalibrationComponent } from './control-direction-eye-calibration.component';

describe('ControlDirectionEyeCalibrationComponent', () => {
  let component: ControlDirectionEyeCalibrationComponent;
  let fixture: ComponentFixture<ControlDirectionEyeCalibrationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ControlDirectionEyeCalibrationComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(ControlDirectionEyeCalibrationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
