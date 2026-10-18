import { ComponentFixture, TestBed } from '@angular/core/testing';

import { WebgazerCalibrationComponent } from './webgazer-calibration.component';

describe('WebgazerCalibrationComponent', () => {
  let component: WebgazerCalibrationComponent;
  let fixture: ComponentFixture<WebgazerCalibrationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WebgazerCalibrationComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(WebgazerCalibrationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
