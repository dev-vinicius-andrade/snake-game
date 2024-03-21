import { Nullable } from '@app/types/nullable';
import {
  AfterViewInit,
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { Position, PositionWithColor } from '@app/types/position';

@Component({
  selector: 'app-game-canvas',
  standalone: true,
  imports: [],
  templateUrl: './game-canvas.component.html',
  styleUrl: './game-canvas.component.scss',
})
export class GameCanvasComponent implements OnInit, AfterViewInit {
  setDimensions(width: number, height: number) {
    this.roomWidth = width;
    this.roomHeight = height;
  }
  canvasContext: Nullable<CanvasRenderingContext2D>;
  private roomWidth?: number;
  private roomHeight?: number;
  @Output() keyboardEvent = new EventEmitter<Nullable<KeyboardEvent>>();
  @ViewChild('gamecanvas', { static: true }) gamecanvas: Nullable<
    ElementRef<HTMLCanvasElement>
  >;
  @HostListener('window:resize', ['$event'])
  onResize(event: any) {
    this.updateCanvasSize();
  }
  @HostListener('window:keydown', ['$event'])
  handleKeyboardEvent(event: KeyboardEvent): void {
    this.keyboardEvent.emit(event);
  }
  ngOnInit(): void {
    this.canvasContext = this.gamecanvas?.nativeElement.getContext('2d');
    this.updateCanvasSize();
    // this.initializeKeyboardEvents();
  }
  ngAfterViewInit(): void {}
  initializeKeyboardEvents() {
    if (!this.gamecanvas) return;

    this.gamecanvas.nativeElement.onkeyup = (event) => {
      event.preventDefault();
      event.stopPropagation();
      this.keyboardEvent.emit(event);
    };
  }

  writeText(text: string, position: Position, color: string, font: string) {
    if (!this.canvasContext) return;
    this.canvasContext.beginPath();
    this.canvasContext.font = font;
    this.canvasContext.fillStyle = color;
    this.canvasContext.fillText(text, position.x, position.y);
    this.canvasContext.closePath();
  }
  drawPath(path: PositionWithColor[], size: number) {
    if (!this.canvasContext) return;
    for (let position of path)
      this.drawRectangle(
        position.color.fillColor,
        position,
        size,
        position.color.border
      );
  }
  drawRectangle(
    color: string,
    position: Position,
    size: number,
    borderColor?: string
  ) {
    if (!this.canvasContext) return;
    this.canvasContext.beginPath();
    this.canvasContext.fillStyle = color;
    const canvasPosition = this.translateToCanvasPosition(position, size);
    if (!canvasPosition) return;
    this.canvasContext.rect(canvasPosition.x, canvasPosition.y, size, size);
    this.canvasContext.fill();
    this.canvasContext.closePath();
    if (borderColor) {
      this.canvasContext.strokeStyle = borderColor;
      this.canvasContext.lineWidth = 1;
      this.canvasContext.stroke();
    }
  }
  clearPosition(position: Position, size: number) {
    if (!this.canvasContext) return;
    this.canvasContext.clearRect(position.x, position.y, size, size);
  }
  clear() {
    if (!this.canvasContext) return;
    if (!this.gamecanvas) return;
    const canvasRect = this.gamecanvas.nativeElement.getBoundingClientRect();
    const canvasWidth = canvasRect.width;
    const canvasHeight = canvasRect.height;
    this.canvasContext.clearRect(0, 0, canvasWidth, canvasHeight);
  }
  translateToCanvasPosition(position: Position, size: number = 0) {
    if (!this.canvasContext) return null;
    if (!this.gamecanvas) return null;
    const roomDimensions = { width: 1920, height: 1080 };

    const canvasRect = this.gamecanvas.nativeElement.getBoundingClientRect();
    const canvasWidth = canvasRect.width;
    const canvasHeight = canvasRect.height;
    const x = (position.x / roomDimensions.width) * canvasWidth - size;
    const y = (position.y / roomDimensions.height) * canvasHeight - size;
    return { x, y };
  }
  updateCanvasSize(): void {
    if (this.gamecanvas && this.gamecanvas.nativeElement) {
      const canvas = this.gamecanvas.nativeElement;
      const parent = canvas.parentElement;
      if (!parent) return;
      //const rect = canvas.getBoundingClientRect();

      const width = parent.clientWidth;
      const height = parent.clientHeight;
      const dpr = window.devicePixelRatio || 1;

      canvas.width = width * dpr;
      canvas.height = height * dpr;

      canvas.style.width = `${width}px`;
      canvas.style.height = `${height}px`;

      if (canvas.getContext) {
        this.canvasContext = canvas.getContext('2d');
        if (!this.canvasContext) return;
        this.canvasContext.scale(dpr, dpr);
      }
    }
  }
}
