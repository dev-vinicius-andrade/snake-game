import { Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home.component';
import { BasicComponent } from '@app/layouts/basic/basic.component';
import { MainComponent } from '@app/layouts/main/main.component';
import { RoomComponent } from '@app/pages/room/room/room.component';

export const routes: Routes = [
  {
    path: '',
    component: MainComponent,
    children: [
      {
        path: '',
        component: BasicComponent,
        children: [{ path: '', component: HomeComponent }],
      },
      {
        path: 'room',
        component: BasicComponent,
        children: [{ path: ':token', component: RoomComponent }],
      },
    ],
  },
];
