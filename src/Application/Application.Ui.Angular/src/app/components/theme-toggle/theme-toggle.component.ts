import { Component } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { ThemeService } from '@services/theme/theme.service';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { ThemePalette } from '@angular/material/core';
@Component({
  selector: 'app-theme-toggle',
  standalone: true,
  imports: [MatIcon, MatSlideToggleModule],
  templateUrl: './theme-toggle.component.html',
  styleUrl: './theme-toggle.component.scss',
})
export class ThemeToggleComponent {
  themeService: ThemeService;
  color: ThemePalette;
  classList?: string;
  protected checked = false;

  constructor(themeService: ThemeService) {
    this.themeService = themeService;
    this.color = 'accent';
  }
  toggleTheme() {
    this.themeService.toogle();
    this.checked = !this.checked;
  }
}
