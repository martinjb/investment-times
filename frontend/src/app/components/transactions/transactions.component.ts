// components/transactions/transactions.component.ts
// Form for adding a buy or sell, plus a ledger of recent transactions.

import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { AssetType, TransactionType, Transaction, CreateTransaction } from '../../models/models';

@Component({
  selector: 'app-transactions',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './transactions.component.html',
  styleUrls: ['./transactions.component.css']
})
export class TransactionsComponent implements OnInit, OnDestroy {
  // Two-way bound form fields. Defaults chosen so the form is "always valid-ish" by default.
  form: CreateTransaction = {
    symbol: '',
    assetType: AssetType.Stock,
    type: TransactionType.Buy,
    quantity: 0,
    pricePerUnit: 0
  };

  transactions: Transaction[] = [];
  errorMessage = '';
  successMessage = '';
  AssetType = AssetType;
  TransactionType = TransactionType;
  private sub?: Subscription;

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.loadTransactions();
    this.sub = this.api.refresh$.subscribe(() => this.loadTransactions());
  }

  ngOnDestroy() { this.sub?.unsubscribe(); }

  submit() {
    this.errorMessage = '';
    this.successMessage = '';

    if (!this.form.symbol.trim()) { this.errorMessage = 'Symbol is required.'; return; }
    if (this.form.quantity <= 0)   { this.errorMessage = 'Quantity must be greater than 0.'; return; }
    if (this.form.pricePerUnit <= 0) { this.errorMessage = 'Price must be greater than 0.'; return; }

    this.api.addTransaction(this.form).subscribe({
      next: () => {
        this.successMessage = `Recorded ${this.typeLabel(this.form.type)} of ${this.form.symbol.toUpperCase()}.`;
        // Reset just the data fields - keep the user's chosen asset type for convenience.
        this.form = { ...this.form, symbol: '', quantity: 0, pricePerUnit: 0 };
        setTimeout(() => this.successMessage = '', 4000);
      },
      error: err => { this.errorMessage = err?.error?.title ?? 'Failed to save transaction.'; }
    });
  }

  delete(id: number) {
    this.api.deleteTransaction(id).subscribe();
  }

  private loadTransactions() {
    this.api.getTransactions().subscribe(data => this.transactions = data);
  }

  typeLabel(t: TransactionType): string { return t === TransactionType.Buy ? 'BUY' : 'SELL'; }
  assetLabel(a: AssetType): string { return a === AssetType.Stock ? 'STOCK' : 'CRYPTO'; }
}
