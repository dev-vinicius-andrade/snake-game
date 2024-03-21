import { ThemeService } from '@services/theme/theme.service';
import { CommonModule, DOCUMENT } from '@angular/common';
import { Component, Inject, OnInit, Renderer2 } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Theme } from '@app/types/enums';
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, CommonModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent implements OnInit {
  title = 'SnakeGame';
  themeService: ThemeService;
  private document: Document;
  private renderer: Renderer2;
  constructor(
    themeService: ThemeService,
    @Inject(DOCUMENT) document: Document,
    renderer: Renderer2
  ) {
    this.themeService = themeService;

    this.document = document;
    this.renderer = renderer;
  }
  ngOnInit(): void {
    this.switchTheme(this.themeService.current);
    this.themeService.subscribe((theme) => {
      this.switchTheme(theme);
      return theme;
    });
  }
  private switchTheme(theme: Theme) {
    this.document.body.classList.remove('dark', 'light');
    switch (theme) {
      case Theme.Light:
        this.document.body.classList.add('light');
        break;
      default:
        this.document.body.classList.add('dark');
        break;
    }
  }
}
