// app.component.ts
// The root component. We compose the dashboard, transactions, portfolio, and news
// components onto a single page with a magazine-style masthead at the top.

import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { TransactionsComponent } from './components/transactions/transactions.component';
import { PortfolioComponent } from './components/portfolio/portfolio.component';
import { NewsComponent } from './components/news/news.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, DashboardComponent, TransactionsComponent, PortfolioComponent, NewsComponent],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent {
  today = new Date();
}
