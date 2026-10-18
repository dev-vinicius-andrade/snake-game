import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { WebgazerComponent } from '@app/components/webgazer/webgazer.component';
import { ButtonModule } from 'primeng/button';
import { WebGazer } from 'webgazer';

@Component({
  selector: 'app-web-gazer-calibration-accuracy-step',
  standalone: true,
  imports: [CommonModule, WebgazerComponent, ButtonModule],
  templateUrl: './web-gazer-calibration-accuracy-step.component.html',
  styleUrl: './web-gazer-calibration-accuracy-step.component.scss',
})
export class WebGazerCalibrationAccuracyStepComponent {
  @Input() points: number = 9;
  @Input() clicksPerPoint: number = 3;
  @Input() pointSize: number = 50;
  protected loading: boolean;
  protected started: boolean;
  private webgazer?: WebGazer;
  protected acuracy: number;
  private currentPoint: {
    x: number;
    y: number;
    clickCount: number;
    index: number;
  };

  constructor() {
    this.acuracy = 0;
    this.loading = true;
    this.started = false;
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
  protected get acuracyPercentage(): number {
    return (this.acuracy / (this.points * this.clicksPerPoint)) * 100;
  }
  protected get leftPosition(): number {
    return this.currentPoint.x + this.pointSize;
  }
  protected get topPosition(): number {
    return this.currentPoint.y + this.pointSize;
  }
  async onWebgazerInitialized(webgazer: WebGazer) {
    this.loading = false;
    this.webgazer = webgazer;
  }
  onStartClicked() {
    this.generateNewPoint();
    this.acuracy = 0;
    this.started = true;
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
  private async checkAndIncrementAcuracy() {
    const prediction = await this.webgazer?.getCurrentPrediction();
    if (!prediction) return;
    const x = prediction.x;
    const y = prediction.y;
    const distance = Math.sqrt(
      Math.pow(x - this.currentPoint.x, 2) +
        Math.pow(y - this.currentPoint.y, 2)
    );
    if (distance < 50) {
      this.acuracy++;
    }
  }
  protected async onCurrentPointButtonClicked(e: MouseEvent) {
    if (this.finished) return;
    await this.checkAndIncrementAcuracy();
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
