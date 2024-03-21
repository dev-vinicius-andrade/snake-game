import { ComponentFixture, TestBed } from '@angular/core/testing';

import { GameCanvasComponent } from './game-canvas.component';

describe('GameCanvasComponent', () => {
  let component: GameCanvasComponent;
  let fixture: ComponentFixture<GameCanvasComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GameCanvasComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(GameCanvasComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
