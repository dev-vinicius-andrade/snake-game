import { ThemeService } from '@services/theme/theme.service';
import { Component, OnInit, Signal, computed } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeToggleComponent } from '@app/components/theme-toggle/theme-toggle.component';
import { MenuItem } from 'primeng/api';
import { MenubarModule } from 'primeng/menubar';
// import darkIcon from '@assets/icons/dark/dark.svg';
// import light from '@assets/icons/light/light.svg';
@Component({
  selector: 'app-main',
  standalone: true,
  imports: [RouterOutlet, MenubarModule, ThemeToggleComponent],
  templateUrl: './main.component.html',
  styleUrl: './main.component.scss',
})
export class MainComponent implements OnInit {
  protected items: MenuItem[];
  private themeService: ThemeService;
  constructor(themeService: ThemeService) {
    this.themeService = themeService;
    this.items = this.getMenuItems();
  }
  ngOnInit(): void {
    this.themeService.subscribe((theme) => {
      this.items = this.getMenuItems();
      return theme;
    });
  }
  icon() {
    return this.themeService.isDarkMode()
      ? '/assets/icons/dark/dark.svg'
      : '/assets/icons/light/light.svg';
  }
  private getMenuItems(): MenuItem[] {
    return [
      {
        label: 'SnakeGame',
        styleClass: `ml-2 text-app-text ${
          this.themeService.isDarkMode() ? 'dark' : 'light'
        }`,
        url: '',
        replaceUrl: true,
      },
      {
        icon: 'pi pi-code',

        styleClass: `ml-2 text-app-text ${
          this.themeService.isDarkMode() ? 'dark' : 'light'
        }`,
        iconClass: `text-app-text ${
          this.themeService.isDarkMode() ? 'dark' : 'light'
        }`,
        url: 'https://github.com/dev-vinicius-andrade/snake-game',
        replaceUrl: true,
      },
    ];
  }
}
