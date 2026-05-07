// components/dashboard/dashboard.component.ts
// The "landing page" - shows the four market tickers and a summary of the user's portfolio.

import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { MarketIndicator, PortfolioSummary, PriceTracker } from '../../models/models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule], // brings in *ngFor, *ngIf, pipes, etc.
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit, OnDestroy {
  indicators: MarketIndicator[] = [];
  summary: PortfolioSummary | null = null;
  loading = true;
  trackers: PriceTracker[] = [];
  private sub?: Subscription;

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.loadAll();
    // When a transaction is added or deleted somewhere else in the app, ApiService emits on refresh$.
    this.sub = this.api.refresh$.subscribe(() => this.loadSummary());
  }

  ngOnDestroy(): void {
    // Always unsubscribe to avoid memory leaks when the component is destroyed.
    this.sub?.unsubscribe();
  }

private loadAll() {
  this.api.getIndicators().subscribe({
    next: data => { this.indicators = data; this.loading = false; },
    error: () => { this.loading = false; }
  });
  this.api.getPriceTrackers().subscribe({
    next: data => { this.trackers = data; }
  });
  this.loadSummary();
}

  private loadSummary() {
    this.api.getSummary().subscribe({ next: s => this.summary = s });
  }
}
