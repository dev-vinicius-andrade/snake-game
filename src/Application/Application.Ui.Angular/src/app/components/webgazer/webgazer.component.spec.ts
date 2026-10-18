import { ComponentFixture, TestBed } from '@angular/core/testing';

import { WebgazerComponent } from './webgazer.component';

describe('WebgazerComponent', () => {
  let component: WebgazerComponent;
  let fixture: ComponentFixture<WebgazerComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WebgazerComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(WebgazerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
