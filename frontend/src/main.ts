// main.ts
// Boots the Angular app. We use the new "standalone" component API (Angular 14+)
// instead of NgModules. It's less ceremony and the way new Angular code is written.

import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { AppComponent } from './app/app.component';

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient() // makes HttpClient available everywhere via DI
  ]
}).catch(err => console.error(err));
