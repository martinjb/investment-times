// components/dashboard/dashboard.component.ts
// The "landing page" - shows the four market tickers and a summary of the user's portfolio.

import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { MarketGroup, PortfolioSummary } from '../../models/models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit, OnDestroy {
  groups: MarketGroup[] = [];
  summary: PortfolioSummary | null = null;
  loading = true;
  private sub?: Subscription;

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.loadAll();
    this.sub = this.api.refresh$.subscribe(() => this.loadSummary());
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  private loadAll() {
    this.api.getMarketGroups().subscribe({
      next: data => { this.groups = data; this.loading = false; },
      error: () => { this.loading = false; }
    });
    this.loadSummary();
  }

  private loadSummary() {
    this.api.getSummary().subscribe({ next: s => this.summary = s });
  }
}
