// app/services/api.service.ts
// All HTTP calls live in ONE service. Components don't talk to HttpClient directly.
//
// Why this matters:
//   - One place to change the base URL.
//   - One place to add auth headers, error handling, retry logic, etc.
//   - Easy to mock in tests: just provide a fake ApiService.
//
// This is the "Service Layer" pattern in Angular - the same idea as our C# services.

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import { tap } from 'rxjs/operators';
import {
  Transaction, CreateTransaction, Holding, PortfolioSummary,
  MarketIndicator, NewsItem
} from '../models/models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  // The .NET API runs on http://localhost:5000 in dev. Configurable via environment files in a real app.
  private readonly baseUrl = 'http://localhost:5000/api';

  // BehaviorSubject is RxJS's "hold the latest value and broadcast it to subscribers".
  // Components that want fresh holdings after a transaction is added can just listen here.
  private readonly _refresh$ = new BehaviorSubject<number>(0);
  readonly refresh$ = this._refresh$.asObservable();

  constructor(private http: HttpClient) {}

  // ---- Market ----
  getIndicators(): Observable<MarketIndicator[]> {
    return this.http.get<MarketIndicator[]>(`${this.baseUrl}/market/indicators`);
  }

  // ---- Portfolio ----
  getSummary(): Observable<PortfolioSummary> {
    return this.http.get<PortfolioSummary>(`${this.baseUrl}/portfolio/summary`);
  }

  getHoldings(): Observable<Holding[]> {
    return this.http.get<Holding[]>(`${this.baseUrl}/portfolio/holdings`);
  }

  getTransactions(): Observable<Transaction[]> {
    return this.http.get<Transaction[]>(`${this.baseUrl}/portfolio/transactions`);
  }

  addTransaction(tx: CreateTransaction): Observable<Transaction> {
    return this.http.post<Transaction>(`${this.baseUrl}/portfolio/transactions`, tx)
      .pipe(tap(() => this.triggerRefresh()));
  }

  deleteTransaction(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/portfolio/transactions/${id}`)
      .pipe(tap(() => this.triggerRefresh()));
  }

  // ---- News ----
  getNews(): Observable<NewsItem[]> {
    return this.http.get<NewsItem[]>(`${this.baseUrl}/news`);
  }

  // Tells anyone listening "data has changed; refetch."
  triggerRefresh() { this._refresh$.next(Date.now()); }
}
