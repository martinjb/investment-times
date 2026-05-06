// components/portfolio/portfolio.component.ts
// Lists each currently-held asset with cost basis, current value, and P/L.

import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { Holding, AssetType } from '../../models/models';

@Component({
  selector: 'app-portfolio',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './portfolio.component.html',
  styleUrls: ['./portfolio.component.css']
})
export class PortfolioComponent implements OnInit, OnDestroy {
  holdings: Holding[] = [];
  loading = true;
  AssetType = AssetType;
  private sub?: Subscription;

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.load();
    this.sub = this.api.refresh$.subscribe(() => this.load());
  }

  ngOnDestroy() { this.sub?.unsubscribe(); }

  private load() {
    this.api.getHoldings().subscribe({
      next: data => { this.holdings = data; this.loading = false; },
      error: () => this.loading = false
    });
  }
}
