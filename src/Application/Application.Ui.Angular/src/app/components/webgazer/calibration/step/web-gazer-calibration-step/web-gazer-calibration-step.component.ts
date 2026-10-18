import { CommonModule } from '@angular/common';
import {
  Component,
  ElementRef,
  EventEmitter,
  Input,
  Output,
  ViewChild,
} from '@angular/core';
import { WebgazerComponent } from '@app/components/webgazer/webgazer.component';
import { ButtonModule } from 'primeng/button';
import { WebGazer } from 'webgazer';

@Component({
  selector: 'app-web-gazer-calibration-step',
  standalone: true,
  imports: [CommonModule, WebgazerComponent, ButtonModule],
  templateUrl: './web-gazer-calibration-step.component.html',
  styleUrl: './web-gazer-calibration-step.component.scss',
})
export class WebGazerCalibrationStepComponent {
  // @ViewChild('plottingPointsContainer') plottingPointsContainer: Nullable<
  //   ElementRef<HTMLDivElement>
  // >;
  @Input() points: number = 9;
  @Input() clicksPerPoint: number = 3;
  @Input() pointSize: number = 50;
  @Output() onNext = new EventEmitter<void>();
  protected loading: boolean;
  private currentPoint: {
    x: number;
    y: number;
    clickCount: number;
    index: number;
  };
  constructor() {
    this.loading = true;
    this.currentPoint = {
      x: 0,
      y: 0,
      clickCount: 0,
      index: 0,
    };
  }
  private buttonClassList: string[] = [];
  protected get buttonClass(): string {
    return this.buttonClassList.join(' ');
  }
  protected get finished(): boolean {
    return this.currentPoint.index >= this.points;
  }
  protected get leftPosition(): number {
    return this.currentPoint.x + this.pointSize;
  }
  protected get topPosition(): number {
    return this.currentPoint.y + this.pointSize;
  }
  onOkClicked() {
    this.onNext.emit();
  }
  async onWebgazerInitialized(webgazer: WebGazer) {
    this.loading = false;
  }
  private generateNewPoint() {
    // Ensure the button stays within the viewport's bounds
    const maxWidth = window.innerWidth - this.pointSize * 2;
    const maxHeight = window.innerHeight - this.pointSize * 2;

    // Generate a random position within the constraints
    const leftPosition = Math.random() * maxWidth;
    const topPosition = Math.random() * maxHeight;
    this.currentPoint = {
      x: leftPosition,
      y: topPosition,
      clickCount: 0,
      index: this.currentPoint.index + 1,
    };
  }
  protected onCurrentPointButtonClicked(e: MouseEvent) {
    if (this.finished) return;
    this.currentPoint.clickCount++;
    const button = e.target as HTMLButtonElement;
    const progress = this.currentPoint.clickCount / this.clicksPerPoint;
    this.changeButtonColor(button, progress);
    if (this.currentPoint.clickCount < this.clicksPerPoint) return;

    button.style.animationDuration = '0.1s'; // Trigger the explosion animation

    setTimeout(() => {
      this.resetAnimation(button);
      this.generateNewPoint();
    }, 100);
  }
  private resetAnimation(button: HTMLButtonElement) {
    button.style.animation = 'none';
    requestAnimationFrame(() => {
      // Force reflow in between setting none and adding the animation again
      button.offsetHeight; // Trigger reflow to make the browser recognize the change
      button.style.animation = '';
      button.style.backgroundColor = ''; // Reset the color
    });
  }
  private changeButtonColor(button: HTMLButtonElement, progress: number) {
    // Example color transition: blue (0%) -> yellow (50%) -> red (100%)
    if (progress < 0.5) {
      // Transition from blue to yellow
      const intermediateColor = `rgb(${255 * progress * 2}, ${255}, 0)`; // R, G, B
      button.style.backgroundColor = intermediateColor;
    } else {
      // Transition from yellow to red
      const intermediateColor = `rgb(255, ${255 * (1 - progress) * 2}, 0)`;
      button.style.backgroundColor = intermediateColor;
    }
  }
}
